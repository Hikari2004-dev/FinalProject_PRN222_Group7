using FinalProject_PRN222_Group7.DAL.Data;
using FinalProject_PRN222_Group7.DAL.Entities;
using FinalProject_PRN222_Group7.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using PayOS;
using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;
using System.Text.Json;

namespace FinalProject_PRN222_Group7.BLL.Services
{
    public interface IQuizService
    {
        Task<IEnumerable<Quiz>> GetByCourseAsync(int courseId);
        Task<Quiz?> GetQuizWithQuestionsAsync(int id);
        Task<Quiz> CreateQuizAsync(Quiz quiz, IEnumerable<Question> questions);
        Task<QuizAttempt> StartAttemptAsync(int quizId, string userId);
        Task<QuizAttempt> SubmitAttemptAsync(int attemptId, Dictionary<int, char> answers);
        Task<QuizAttempt?> GetAttemptAsync(int id);
        Task DeleteQuizAsync(int id);
    }

    public class QuizService : IQuizService
    {
        private readonly IQuizRepository _repo;
        private readonly AppDbContext _context;

        public QuizService(IQuizRepository repo, AppDbContext context)
        {
            _repo = repo;
            _context = context;
        }

        public async Task<IEnumerable<Quiz>> GetByCourseAsync(int courseId)
            => await _repo.GetByCourseAsync(courseId);

        public async Task<Quiz?> GetQuizWithQuestionsAsync(int id)
            => await _repo.GetWithQuestionsAsync(id);

        public async Task<Quiz> CreateQuizAsync(Quiz quiz, IEnumerable<Question> questions)
        {
            quiz.TotalQuestions = questions.Count();
            _context.Quizzes.Add(quiz);
            await _context.SaveChangesAsync();

            var index = 0;
            foreach (var q in questions)
            {
                q.QuizId = quiz.Id;
                q.OrderIndex = index++;
                _context.Questions.Add(q);
            }
            await _context.SaveChangesAsync();
            return quiz;
        }

        public async Task<QuizAttempt> StartAttemptAsync(int quizId, string userId)
        {
            var attempt = new QuizAttempt { QuizId = quizId, UserId = userId, StartedAt = DateTime.UtcNow };
            _context.QuizAttempts.Add(attempt);
            await _context.SaveChangesAsync();
            return attempt;
        }

        public async Task<QuizAttempt> SubmitAttemptAsync(int attemptId, Dictionary<int, char> answers)
        {
            var attempt = await _context.QuizAttempts
                .Include(a => a.Quiz).ThenInclude(q => q.Questions)
                .FirstOrDefaultAsync(a => a.Id == attemptId)
                ?? throw new InvalidOperationException("Attempt not found");

            var correct = 0;
            foreach (var q in attempt.Quiz.Questions)
            {
                if (answers.TryGetValue(q.Id, out var selected) && selected == q.CorrectAnswer)
                {
                    correct++;
                }
            }

            attempt.CorrectAnswers = correct;
            attempt.TotalQuestions = attempt.Quiz.Questions.Count;
            attempt.Score = (int)Math.Round((double)correct / attempt.TotalQuestions * 100);
            attempt.CompletedAt = DateTime.UtcNow;
            attempt.IsCompleted = true;
            attempt.AnswersJson = JsonSerializer.Serialize(answers);

            await _context.SaveChangesAsync();
            return attempt;
        }

        public async Task<QuizAttempt?> GetAttemptAsync(int id)
            => await _context.QuizAttempts
                .Include(a => a.Quiz).ThenInclude(q => q.Questions)
                .FirstOrDefaultAsync(a => a.Id == id);

        public async Task DeleteQuizAsync(int id)
        {
            var quiz = await _context.Quizzes
                .Include(q => q.Questions)
                .Include(q => q.Attempts)
                .FirstOrDefaultAsync(q => q.Id == id);
            if (quiz == null)
            {
                return;
            }

            _context.Questions.RemoveRange(quiz.Questions);
            _context.QuizAttempts.RemoveRange(quiz.Attempts);
            _context.Quizzes.Remove(quiz);
            await _context.SaveChangesAsync();
        }
    }

    public record PaymentCheckoutResult(Payment Payment, string CheckoutUrl);

    public interface IPaymentService
    {
        Task<IEnumerable<Package>> GetPackagesAsync();
        Task<IEnumerable<CreditPackage>> GetCreditPackagesAsync();
        Task<UserPackage?> GetUserPackageAsync(string userId);
        Task<UserSubscription?> GetUserSubscriptionAsync(string userId);
        Task<WalletBalanceSummary> GetWalletBalanceAsync(string userId, IEnumerable<string>? roles = null);
        Task<PaymentCheckoutResult> CreatePaymentAsync(string userId, int packageId, string baseUrl);
        Task<PaymentCheckoutResult> CreateCreditTopUpAsync(string userId, int creditPackageId, string baseUrl);
        Task<Payment?> GetPaymentForUserAsync(int paymentId, string userId);
        Task<Payment?> GetPaymentByGatewayOrderCodeAsync(string gatewayOrderCode);
        Task<Payment> CompletePaymentAsync(int paymentId, string userId);
        Task<Payment> CancelPaymentAsync(int paymentId, string userId);
        Task<Payment> ConfirmPaymentAsync(Webhook webhook);
        Task RegisterWebhookAsync(string webhookUrl);
        Task<IEnumerable<Payment>> GetUserPaymentsAsync(string userId);
        Task<IEnumerable<Payment>> GetAllPaymentsAsync();
    }

    public class PaymentService : IPaymentService
    {
        private readonly AppDbContext _context;
        private readonly IPaymentRepository _paymentRepo;
        private readonly PayOSClient _payOS;
        private readonly ISubscriptionService _subscriptionService;
        private readonly ICreditWalletService _walletService;

        public PaymentService(
            AppDbContext context,
            IPaymentRepository paymentRepo,
            PayOSClient payOS,
            ISubscriptionService subscriptionService,
            ICreditWalletService walletService)
        {
            _context = context;
            _paymentRepo = paymentRepo;
            _payOS = payOS;
            _subscriptionService = subscriptionService;
            _walletService = walletService;
        }

        public async Task<IEnumerable<Package>> GetPackagesAsync()
            => await _context.Packages
                .Where(p => p.IsActive && !p.IsFree)
                .Include(p => p.Features.OrderBy(f => f.DisplayOrder))
                .OrderBy(p => p.DisplayOrder)
                .ToListAsync();

        public async Task<IEnumerable<CreditPackage>> GetCreditPackagesAsync()
            => await _context.CreditPackages
                .Where(p => p.IsActive)
                .OrderBy(p => p.DisplayOrder)
                .ToListAsync();

        public Task<UserPackage?> GetUserPackageAsync(string userId)
            => _subscriptionService.GetLegacyCompatiblePackageAsync(userId);

        public Task<UserSubscription?> GetUserSubscriptionAsync(string userId)
            => _subscriptionService.GetActiveSubscriptionAsync(userId);

        public Task<WalletBalanceSummary> GetWalletBalanceAsync(string userId, IEnumerable<string>? roles = null)
            => _walletService.GetBalanceAsync(userId, roles);

        public async Task<PaymentCheckoutResult> CreatePaymentAsync(string userId, int packageId, string baseUrl)
        {
            var package = await _context.Packages.FirstOrDefaultAsync(p => p.Id == packageId && p.IsActive)
                ?? throw new InvalidOperationException("Package not found");

            var payment = new Payment
            {
                UserId = userId,
                PackageId = package.Id,
                Amount = package.Price,
                PurchaseType = PaymentPurchaseType.Subscription,
                PaymentMethod = package.Price <= 0 ? PaymentMethod.Internal : PaymentMethod.PayOS,
                InvoiceNumber = BuildInvoiceNumber("SUB"),
                Status = package.Price <= 0 ? PaymentStatus.Completed : PaymentStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                ExpiredAt = DateTime.UtcNow.AddMinutes(30)
            };
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            if (package.Price <= 0)
            {
                await GrantSubscriptionAsync(payment);
                return new PaymentCheckoutResult(payment, $"{baseUrl}/Packages/Success?paymentId={payment.Id}");
            }

            return await CreateCheckoutAsync(payment, $"Mua goi {package.Name}", baseUrl);
        }

        public async Task<PaymentCheckoutResult> CreateCreditTopUpAsync(string userId, int creditPackageId, string baseUrl)
        {
            var creditPackage = await _context.CreditPackages.FirstOrDefaultAsync(p => p.Id == creditPackageId && p.IsActive)
                ?? throw new InvalidOperationException("Credit package not found");

            var payment = new Payment
            {
                UserId = userId,
                CreditPackageId = creditPackage.Id,
                Amount = creditPackage.Price,
                PurchaseType = PaymentPurchaseType.CreditTopUp,
                PaymentMethod = PaymentMethod.PayOS,
                InvoiceNumber = BuildInvoiceNumber("TOPUP"),
                Status = PaymentStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                ExpiredAt = DateTime.UtcNow.AddMinutes(30)
            };
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            return await CreateCheckoutAsync(payment, $"Nap {creditPackage.Credits} credit", baseUrl);
        }

        public async Task<Payment?> GetPaymentForUserAsync(int paymentId, string userId)
            => await _context.Payments
                .Include(p => p.Package)
                .Include(p => p.CreditPackage)
                .FirstOrDefaultAsync(p => p.Id == paymentId && p.UserId == userId);

        public async Task<Payment?> GetPaymentByGatewayOrderCodeAsync(string gatewayOrderCode)
            => await _context.Payments
                .Include(p => p.Package)
                .Include(p => p.CreditPackage)
                .FirstOrDefaultAsync(p => p.GatewayOrderCode == gatewayOrderCode);

        public async Task<Payment> CompletePaymentAsync(int paymentId, string userId)
        {
            var payment = await GetPaymentForUserAsync(paymentId, userId)
                ?? throw new InvalidOperationException("Payment not found");

            return payment;
        }

        public async Task<Payment> CancelPaymentAsync(int paymentId, string userId)
        {
            var payment = await GetPaymentForUserAsync(paymentId, userId)
                ?? throw new InvalidOperationException("Payment not found");

            if (payment.Status == PaymentStatus.Pending || payment.Status == PaymentStatus.Processing)
            {
                payment.Status = PaymentStatus.Cancelled;
                payment.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return payment;
        }

        public async Task<Payment> ConfirmPaymentAsync(Webhook webhook)
        {
            var rawPayload = JsonSerializer.Serialize(webhook);
            var payment = await TryResolvePaymentFromWebhookAsync(webhook);
            var callbackLog = new PaymentCallbackLog
            {
                PaymentId = payment?.Id,
                GatewayProvider = "PayOS",
                GatewayOrderCode = ResolveOrderCode(webhook),
                Signature = ResolveSignature(webhook),
                RawPayload = rawPayload,
                CreatedAt = DateTime.UtcNow
            };
            _context.PaymentCallbackLogs.Add(callbackLog);
            await _context.SaveChangesAsync();

            try
            {
                var verified = await _payOS.Webhooks.VerifyAsync(webhook);
                callbackLog.IsSignatureValid = true;

                var gatewayOrderCode = verified.OrderCode.ToString();
                payment ??= await _context.Payments
                    .Include(p => p.Package)
                    .Include(p => p.CreditPackage)
                    .FirstOrDefaultAsync(p => p.GatewayOrderCode == gatewayOrderCode);

                if (payment == null)
                {
                    callbackLog.ErrorMessage = "Payment not found for webhook order code.";
                    await _context.SaveChangesAsync();
                    throw new InvalidOperationException("Payment not found");
                }

                if (payment.Status == PaymentStatus.Completed)
                {
                    callbackLog.IsProcessed = true;
                    callbackLog.ProcessedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    return payment;
                }

                payment.Status = PaymentStatus.Processing;
                payment.UpdatedAt = DateTime.UtcNow;
                payment.TransactionId = verified.Reference;
                payment.GatewayOrderCode = gatewayOrderCode;
                payment.MetadataJson = JsonSerializer.Serialize(verified);
                await _context.SaveChangesAsync();

                switch (payment.PurchaseType)
                {
                    case PaymentPurchaseType.Subscription:
                        await GrantSubscriptionAsync(payment);
                        break;
                    case PaymentPurchaseType.CreditTopUp:
                        await GrantPurchasedCreditsAsync(payment);
                        break;
                }

                callbackLog.IsProcessed = true;
                callbackLog.ProcessedAt = DateTime.UtcNow;
                payment.Status = PaymentStatus.Completed;
                payment.PaidAt = DateTime.UtcNow;
                payment.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return payment;
            }
            catch (Exception ex)
            {
                callbackLog.ErrorMessage = ex.Message;
                payment?.Let(p =>
                {
                    if (p.Status == PaymentStatus.Pending || p.Status == PaymentStatus.Processing)
                    {
                        p.Status = PaymentStatus.Failed;
                        p.UpdatedAt = DateTime.UtcNow;
                    }
                });
                await _context.SaveChangesAsync();
                throw;
            }
        }

        public async Task RegisterWebhookAsync(string webhookUrl)
        {
            await _payOS.Webhooks.ConfirmAsync(webhookUrl);
        }

        public async Task<IEnumerable<Payment>> GetUserPaymentsAsync(string userId)
            => await _paymentRepo.GetUserPaymentsAsync(userId);

        public async Task<IEnumerable<Payment>> GetAllPaymentsAsync()
            => await _paymentRepo.GetAllWithUsersAsync();

        private async Task<PaymentCheckoutResult> CreateCheckoutAsync(Payment payment, string description, string baseUrl)
        {
            var orderCode = long.Parse($"{payment.Id}{DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 100000}");
            payment.GatewayOrderCode = orderCode.ToString();
            payment.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var request = new CreatePaymentLinkRequest
            {
                OrderCode = orderCode,
                Amount = (int)payment.Amount,
                Description = description,
                ReturnUrl = $"{baseUrl}/Packages/Success?paymentId={payment.Id}",
                CancelUrl = $"{baseUrl}/Packages/Cancel?paymentId={payment.Id}"
            };

            var createResult = await _payOS.PaymentRequests.CreateAsync(request);
            payment.Notes = createResult.CheckoutUrl;
            payment.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return new PaymentCheckoutResult(payment, createResult.CheckoutUrl);
        }

        private async Task GrantSubscriptionAsync(Payment payment)
        {
            if (payment.PackageId == null)
            {
                throw new InvalidOperationException("Package not found for subscription payment");
            }

            var package = payment.Package ?? await _context.Packages.FirstAsync(p => p.Id == payment.PackageId.Value);
            var subscription = await _subscriptionService.ActivatePackageAsync(payment.UserId, package.Id);
            payment.UserSubscriptionId = subscription.Id;
            payment.PackageId = package.Id;
            payment.PaymentMethod = payment.PaymentMethod == PaymentMethod.Internal && package.Price <= 0 ? PaymentMethod.Internal : PaymentMethod.PayOS;
        }

        private async Task GrantPurchasedCreditsAsync(Payment payment)
        {
            if (payment.CreditPackageId == null)
            {
                throw new InvalidOperationException("Credit package not found for top-up payment");
            }

            var creditPackage = payment.CreditPackage ?? await _context.CreditPackages.FirstAsync(cp => cp.Id == payment.CreditPackageId.Value);
            await _walletService.GrantAsync(
                payment.UserId,
                CreditSourceType.Purchased,
                creditPackage.Credits,
                $"payment:{payment.Id}:topup",
                $"Top-up from payment {payment.InvoiceNumber}");
        }

        private async Task<Payment?> TryResolvePaymentFromWebhookAsync(Webhook webhook)
        {
            var orderCode = ResolveOrderCode(webhook);
            if (string.IsNullOrWhiteSpace(orderCode))
            {
                return null;
            }

            return await _context.Payments
                .Include(p => p.Package)
                .Include(p => p.CreditPackage)
                .FirstOrDefaultAsync(p => p.GatewayOrderCode == orderCode);
        }

        private static string ResolveOrderCode(Webhook webhook)
        {
            var orderCodeProperty = webhook.GetType().GetProperty("Data")?.PropertyType.GetProperty("OrderCode");
            var data = webhook.GetType().GetProperty("Data")?.GetValue(webhook);
            var value = orderCodeProperty?.GetValue(data);
            return value?.ToString() ?? string.Empty;
        }

        private static string ResolveSignature(Webhook webhook)
            => webhook.GetType().GetProperty("Signature")?.GetValue(webhook)?.ToString() ?? string.Empty;

        private static string BuildInvoiceNumber(string prefix)
            => $"{prefix}-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}";
    }

    public interface IReportService
    {
        Task<DashboardStats> GetDashboardStatsAsync();
        Task<IEnumerable<DailyQueryStat>> GetDailyQueryStatsAsync(int days = 30);
        Task<IEnumerable<CourseQuizStat>> GetCourseQuizStatsAsync();
    }

    public record DashboardStats(int TotalUsers, int TotalDocuments, int TotalSessions, int TotalQuizAttempts, decimal TotalRevenue, int IndexedDocuments);
    public record DailyQueryStat(DateTime Date, int Count);
    public record CourseQuizStat(string CourseName, double AverageScore, int AttemptCount);

    public class ReportService : IReportService
    {
        private readonly AppDbContext _context;
        public ReportService(AppDbContext context) { _context = context; }

        public async Task<DashboardStats> GetDashboardStatsAsync()
        {
            var totalUsers = await _context.Users.CountAsync();
            var totalDocs = await _context.Documents.CountAsync();
            var indexedDocs = await _context.Documents.CountAsync(d => d.Status == DocumentStatus.Indexed);
            var totalSessions = await _context.ChatSessions.CountAsync();
            var totalAttempts = await _context.QuizAttempts.CountAsync(a => a.IsCompleted);
            var totalRevenue = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Completed)
                .SumAsync(p => p.Amount);

            return new DashboardStats(totalUsers, totalDocs, totalSessions, totalAttempts, totalRevenue, indexedDocs);
        }

        public async Task<IEnumerable<DailyQueryStat>> GetDailyQueryStatsAsync(int days = 30)
        {
            var from = DateTime.UtcNow.AddDays(-days);
            var data = await _context.ChatMessages
                .Where(m => m.Role == MessageRole.User && m.CreatedAt >= from)
                .GroupBy(m => m.CreatedAt.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .OrderBy(g => g.Date)
                .ToListAsync();

            return data.Select(d => new DailyQueryStat(d.Date, d.Count));
        }

        public async Task<IEnumerable<CourseQuizStat>> GetCourseQuizStatsAsync()
        {
            var data = await _context.QuizAttempts
                .Where(a => a.IsCompleted)
                .Include(a => a.Quiz).ThenInclude(q => q.Course)
                .GroupBy(a => a.Quiz.Course.Name)
                .Select(g => new { CourseName = g.Key, AvgScore = g.Average(a => a.Score), Count = g.Count() })
                .ToListAsync();

            return data.Select(d => new CourseQuizStat(d.CourseName, d.AvgScore, d.Count));
        }
    }

    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string body);
    }

    public class EmailService : IEmailService
    {
        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            using var message = new System.Net.Mail.MailMessage();
            message.From = new System.Net.Mail.MailAddress("vinhmtse180031@fpt.edu.vn", "LMS AI System");
            message.To.Add(new System.Net.Mail.MailAddress(toEmail));
            message.Subject = subject;
            message.Body = body;
            message.IsBodyHtml = true;

            using var smtp = new System.Net.Mail.SmtpClient("smtp.gmail.com", 587);
            smtp.Credentials = new System.Net.NetworkCredential("vinhmtse180031@fpt.edu.vn", "xwhb ekqi mmhe ceas");
            smtp.EnableSsl = true;
            await smtp.SendMailAsync(message);
        }
    }

    public interface IQuestionBankService
    {
        Task<IEnumerable<QuestionBankItem>> GetQuestionsByCourseAsync(int courseId, int? chapterId = null);
        Task<QuestionBankItem?> GetQuestionAsync(int id);
        Task<IEnumerable<QuestionBankItem>> SaveQuestionsToBankAsync(int courseId, int? chapterId, int? documentId, IEnumerable<QuestionBankItem> items);
        Task UpdateQuestionAsync(QuestionBankItem item);
        Task DeleteQuestionAsync(int id);
        Task<Quiz> GenerateRandomQuizAsync(int courseId, int? chapterId, int numQuestions, string title);
        Task<Quiz> GenerateRandomQuizFromMultipleChaptersAsync(int courseId, List<int>? chapterIds, int numQuestions, string title);
    }

    public class QuestionBankService : IQuestionBankService
    {
        private readonly AppDbContext _context;
        public QuestionBankService(AppDbContext context) { _context = context; }

        public async Task<IEnumerable<QuestionBankItem>> GetQuestionsByCourseAsync(int courseId, int? chapterId = null)
        {
            var query = _context.QuestionBankItems.Where(q => q.CourseId == courseId);
            if (chapterId.HasValue)
            {
                query = query.Where(q => q.ChapterId == chapterId);
            }
            return await query.OrderByDescending(q => q.CreatedAt).ToListAsync();
        }

        public async Task<QuestionBankItem?> GetQuestionAsync(int id)
            => await _context.QuestionBankItems.FindAsync(id);

        public async Task<IEnumerable<QuestionBankItem>> SaveQuestionsToBankAsync(int courseId, int? chapterId, int? documentId, IEnumerable<QuestionBankItem> items)
        {
            var addedItems = new List<QuestionBankItem>();
            var existingContents = await _context.QuestionBankItems
                .Where(q => q.CourseId == courseId)
                .Select(q => q.Content.Trim().ToLower())
                .ToListAsync();

            foreach (var item in items)
            {
                var normalizedContent = item.Content.Trim().ToLower();
                if (existingContents.Contains(normalizedContent))
                {
                    continue;
                }

                item.CourseId = courseId;
                item.ChapterId = chapterId;
                item.DocumentId = documentId;
                item.CreatedAt = DateTime.UtcNow;

                _context.QuestionBankItems.Add(item);
                addedItems.Add(item);
                existingContents.Add(normalizedContent);
            }
            await _context.SaveChangesAsync();
            return addedItems;
        }

        public async Task UpdateQuestionAsync(QuestionBankItem item)
        {
            _context.QuestionBankItems.Update(item);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteQuestionAsync(int id)
        {
            var item = await _context.QuestionBankItems.FindAsync(id);
            if (item == null)
            {
                return;
            }

            _context.QuestionBankItems.Remove(item);
            await _context.SaveChangesAsync();
        }

        public async Task<Quiz> GenerateRandomQuizAsync(int courseId, int? chapterId, int numQuestions, string title)
        {
            var query = _context.QuestionBankItems.Where(q => q.CourseId == courseId);
            if (chapterId.HasValue)
            {
                query = query.Where(q => q.ChapterId == chapterId);
            }

            var bankItems = await query.ToListAsync();
            var random = new Random();
            var chosenItems = bankItems.OrderBy(_ => random.Next()).Take(numQuestions).ToList();

            var quiz = new Quiz
            {
                Title = title,
                CourseId = courseId,
                IsAiGenerated = false,
                TotalQuestions = chosenItems.Count,
                TimeLimit = chosenItems.Count * 2,
                CreatedAt = DateTime.UtcNow
            };

            _context.Quizzes.Add(quiz);
            await _context.SaveChangesAsync();

            var idx = 0;
            foreach (var item in chosenItems)
            {
                _context.Questions.Add(new Question
                {
                    QuizId = quiz.Id,
                    Content = item.Content,
                    OptionA = item.OptionA,
                    OptionB = item.OptionB,
                    OptionC = item.OptionC,
                    OptionD = item.OptionD,
                    CorrectAnswer = item.CorrectAnswer,
                    Explanation = item.Explanation,
                    OrderIndex = idx++
                });
            }
            await _context.SaveChangesAsync();

            return quiz;
        }

        public async Task<Quiz> GenerateRandomQuizFromMultipleChaptersAsync(int courseId, List<int>? chapterIds, int numQuestions, string title)
        {
            var query = _context.QuestionBankItems.Where(q => q.CourseId == courseId);
            if (chapterIds != null && chapterIds.Count > 0)
            {
                query = query.Where(q => chapterIds.Contains(q.ChapterId ?? -1));
            }

            var bankItems = await query.ToListAsync();
            var random = new Random();
            var chosenItems = bankItems.OrderBy(_ => random.Next()).Take(numQuestions).ToList();

            var quiz = new Quiz
            {
                Title = title,
                CourseId = courseId,
                IsAiGenerated = false,
                TotalQuestions = chosenItems.Count,
                TimeLimit = chosenItems.Count * 2,
                CreatedAt = DateTime.UtcNow
            };

            _context.Quizzes.Add(quiz);
            await _context.SaveChangesAsync();

            var idx = 0;
            foreach (var item in chosenItems)
            {
                _context.Questions.Add(new Question
                {
                    QuizId = quiz.Id,
                    Content = item.Content,
                    OptionA = item.OptionA,
                    OptionB = item.OptionB,
                    OptionC = item.OptionC,
                    OptionD = item.OptionD,
                    CorrectAnswer = item.CorrectAnswer,
                    Explanation = item.Explanation,
                    OrderIndex = idx++
                });
            }
            await _context.SaveChangesAsync();

            return quiz;
        }
    }

    internal static class PaymentServiceExtensions
    {
        public static void Let<T>(this T? obj, Action<T> action) where T : class
        {
            if (obj != null)
            {
                action(obj);
            }
        }
    }
}
