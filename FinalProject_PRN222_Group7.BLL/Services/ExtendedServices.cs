using FinalProject_PRN222_Group7.DAL.Data;
using FinalProject_PRN222_Group7.DAL.Entities;
using FinalProject_PRN222_Group7.DAL.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PayOS;
using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;
using System.Net.Http.Json;
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
        Task<IEnumerable<int>> GetUserAttemptedQuizIdsAsync(string userId);
        Task<IEnumerable<QuizAttempt>> GetUserCompletedAttemptsAsync(string userId);
        Task<QuizGenerationResult> GenerateQuestionsFromDocumentAsync(int documentId, int courseId, int numQuestions, IEnumerable<string> existingQuestionContents);
        Task<QuizGenerationResult> GenerateQuestionsFromCourseAsync(int courseId, int numQuestions, IEnumerable<string> existingQuestionContents);
    }

    public record QuizGenerationResult(List<Question> Questions, int TokensUsed, string? ModelName);

    public class QuizService : IQuizService
    {
        private readonly IQuizRepository _repo;
        private readonly AppDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        private static int _keyIndex = 0;
        private static readonly object _keyLock = new();

        public QuizService(IQuizRepository repo, AppDbContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _repo = repo;
            _context = context;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        private string GetNextApiKey(List<string> keys)
        {
            if (keys == null || !keys.Any()) return string.Empty;
            lock (_keyLock)
            {
                if (_keyIndex >= keys.Count) _keyIndex = 0;
                var key = keys[_keyIndex];
                _keyIndex = (_keyIndex + 1) % keys.Count;
                return key;
            }
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

        public async Task<IEnumerable<int>> GetUserAttemptedQuizIdsAsync(string userId)
            => await _context.QuizAttempts
                .Where(a => a.UserId == userId)
                .Select(a => a.QuizId)
                .Distinct()
                .ToListAsync();

        public async Task<IEnumerable<QuizAttempt>> GetUserCompletedAttemptsAsync(string userId)
            => await _context.QuizAttempts
                .Where(a => a.UserId == userId && a.IsCompleted)
                .OrderByDescending(a => a.CompletedAt)
                .ToListAsync();

        public async Task<QuizGenerationResult> GenerateQuestionsFromDocumentAsync(int documentId, int courseId, int numQuestions, IEnumerable<string> existingQuestionContents)
        {
            var chunks = await _context.DocumentChunks
                .Where(c => c.DocumentId == documentId)
                .OrderBy(c => c.ChunkIndex)
                .Select(c => c.Content)
                .ToListAsync();
            var contextText = string.Join("\n", chunks);

            var existingList = existingQuestionContents.ToList();

            var geminiSection = _configuration.GetSection("Gemini");
            var apiKeys = geminiSection.GetSection("ApiKeys").Get<List<string>>() ?? new List<string>();
            var model = geminiSection.GetValue<string>("Model") ?? "gemini-2.0-flash";

            if (!apiKeys.Any())
                return new QuizGenerationResult(GenerateMockQuestions(numQuestions), Math.Max(1, contextText.Length / 4), "mock-fallback");

            var client = _httpClientFactory.CreateClient();
            var retries = 0;
            var lastError = string.Empty;

            while (retries < apiKeys.Count)
            {
                var apiKey = GetNextApiKey(apiKeys);
                if (string.IsNullOrEmpty(apiKey) || apiKey.Contains("YOUR-")) { retries++; continue; }

                var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
                var prompt = $"Dựa vào nội dung tài liệu học tập sau đây:\n\n{contextText}\n\n" +
                             $"Hãy tạo ra đúng {numQuestions} câu hỏi trắc nghiệm khách quan mới để kiểm tra kiến thức.\n" +
                             $"Mỗi câu hỏi phải có 4 phương án lựa chọn A, B, C, D và có đáp án đúng kèm theo lời giải thích ngắn gọn.\n\n" +
                             $"RÀNG BUỘC: Không tạo câu hỏi trùng với danh sách sau:\n" +
                             $"{(existingList.Any() ? string.Join("\n- ", existingList.Take(150)) : "(Chưa có câu hỏi nào)")}\n\n" +
                             $"Trả về JSON Array với các trường: content, optionA, optionB, optionC, optionD, correctAnswer (ký tự A/B/C/D), explanation.\n" +
                             $"Chỉ trả về JSON thô, không bọc trong ```json.";

                var payload = new
                {
                    contents = new[] { new { parts = new[] { new { text = prompt } } } },
                    generationConfig = new { responseMimeType = "application/json" }
                };

                try
                {
                    var response = await client.PostAsJsonAsync(url, payload);
                    if (response.IsSuccessStatusCode)
                    {
                        var result = await response.Content.ReadFromJsonAsync<GeminiGenerateResponse>();
                        var jsonText = result?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
                        if (!string.IsNullOrEmpty(jsonText))
                        {
                            var questions = JsonSerializer.Deserialize<List<Question>>(jsonText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<Question>();
                            return new QuizGenerationResult(questions, Math.Max(1, contextText.Length / 4 + jsonText.Length / 4), model);
                        }
                    }
                    lastError = $"HTTP {response.StatusCode}";
                    retries++;
                }
                catch (Exception ex) { lastError = ex.Message; retries++; }
            }

            return new QuizGenerationResult(GenerateMockQuestions(numQuestions), Math.Max(1, contextText.Length / 4), "mock-fallback");
        }

        public async Task<QuizGenerationResult> GenerateQuestionsFromCourseAsync(int courseId, int numQuestions, IEnumerable<string> existingQuestionContents)
        {
            var chunks = await _context.DocumentChunks
                .Where(c => c.Document.CourseId == courseId && c.Document.Status == DocumentStatus.Indexed)
                .OrderBy(c => c.DocumentId)
                .ThenBy(c => c.ChunkIndex)
                .Select(c => c.Content)
                .Take(50)
                .ToListAsync();

            var contextText = string.Join("\n", chunks);
            if (string.IsNullOrWhiteSpace(contextText))
            {
                return new QuizGenerationResult(GenerateMockQuestions(numQuestions), 100, "mock-fallback");
            }

            var existingList = existingQuestionContents.ToList();

            var geminiSection = _configuration.GetSection("Gemini");
            var apiKeys = geminiSection.GetSection("ApiKeys").Get<List<string>>() ?? new List<string>();
            var model = geminiSection.GetValue<string>("Model") ?? "gemini-2.0-flash";

            if (!apiKeys.Any())
                return new QuizGenerationResult(GenerateMockQuestions(numQuestions), Math.Max(1, contextText.Length / 4), "mock-fallback");

            var client = _httpClientFactory.CreateClient();
            var retries = 0;
            var lastError = string.Empty;

            while (retries < apiKeys.Count)
            {
                var apiKey = GetNextApiKey(apiKeys);
                if (string.IsNullOrEmpty(apiKey) || apiKey.Contains("YOUR-")) { retries++; continue; }

                var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
                var prompt = $"Dựa vào tổng hợp kho tài liệu học tập của môn học sau đây:\n\n{contextText}\n\n" +
                             $"Hãy tạo ra đúng {numQuestions} câu hỏi trắc nghiệm khách quan mới để kiểm tra kiến thức.\n" +
                             $"Mỗi câu hỏi phải có 4 phương án lựa chọn A, B, C, D và có đáp án đúng kèm theo lời giải thích ngắn gọn.\n\n" +
                             $"RÀNG BUỘC: Không tạo câu hỏi trùng với danh sách sau:\n" +
                             $"{(existingList.Any() ? string.Join("\n- ", existingList.Take(150)) : "(Chưa có câu hỏi nào)")}\n\n" +
                             $"Trả về JSON Array với các trường: content, optionA, optionB, optionC, optionD, correctAnswer (ký tự A/B/C/D), explanation.\n" +
                             $"Chỉ trả về JSON thô, không bọc trong ```json.";

                var payload = new
                {
                    contents = new[] { new { parts = new[] { new { text = prompt } } } },
                    generationConfig = new { responseMimeType = "application/json" }
                };

                try
                {
                    var response = await client.PostAsJsonAsync(url, payload);
                    if (response.IsSuccessStatusCode)
                    {
                        var result = await response.Content.ReadFromJsonAsync<GeminiGenerateResponse>();
                        var jsonText = result?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
                        if (!string.IsNullOrEmpty(jsonText))
                        {
                            var questions = JsonSerializer.Deserialize<List<Question>>(jsonText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<Question>();
                            return new QuizGenerationResult(questions, Math.Max(1, contextText.Length / 4 + jsonText.Length / 4), model);
                        }
                    }
                    lastError = $"HTTP {response.StatusCode}";
                    retries++;
                }
                catch (Exception ex) { lastError = ex.Message; retries++; }
            }

            return new QuizGenerationResult(GenerateMockQuestions(numQuestions), Math.Max(1, contextText.Length / 4), "mock-fallback");
        }

        private static List<Question> GenerateMockQuestions(int count)
        {
            var questions = new List<Question>();
            for (int i = 0; i < count; i++)
                questions.Add(new Question
                {
                    Content = $"Câu hỏi mẫu số {i + 1}: Đây là câu hỏi về nội dung tài liệu học tập?",
                    OptionA = "Đáp án A (đúng)", OptionB = "Đáp án B",
                    OptionC = "Đáp án C", OptionD = "Đáp án D",
                    CorrectAnswer = 'A',
                    Explanation = "Giải thích: Đây là câu trả lời đúng vì nó phù hợp với nội dung tài liệu."
                });
            return questions;
        }

        private class GeminiGenerateResponse
        {
            public List<GeminiCandidate>? Candidates { get; set; }
        }
        private class GeminiCandidate { public GeminiContent? Content { get; set; } }
        private class GeminiContent { public List<GeminiPart>? Parts { get; set; } }
        private class GeminiPart { public string? Text { get; set; } }
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
        Task<Payment> ProcessSuccessRedirectAsync(int paymentId);
        Task<IEnumerable<Payment>> GetUserPaymentsAsync(string userId);
        Task<IEnumerable<Payment>> GetAllPaymentsAsync();
        Task UpdatePackagePriceAsync(int packageId, decimal newPrice);
    }

    public class PaymentService : IPaymentService
    {
        private readonly AppDbContext _context;
        private readonly IPaymentRepository _paymentRepo;
        private readonly PayOSClient _payOS;
        private readonly ISubscriptionService _subscriptionService;
        private readonly ICreditWalletService _walletService;
        private readonly IEmailService _emailService;

        public PaymentService(
            AppDbContext context,
            IPaymentRepository paymentRepo,
            PayOSClient payOS,
            ISubscriptionService subscriptionService,
            ICreditWalletService walletService,
            IEmailService emailService)
        {
            _context = context;
            _paymentRepo = paymentRepo;
            _payOS = payOS;
            _subscriptionService = subscriptionService;
            _walletService = walletService;
            _emailService = emailService;
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
                ExpiredAt = DateTime.UtcNow.AddMinutes(10)
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
                ExpiredAt = DateTime.UtcNow.AddMinutes(10)
            };
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            return await CreateCheckoutAsync(payment, $"Nap {creditPackage.Credits} credit", baseUrl);
        }

        public async Task<Payment?> GetPaymentForUserAsync(int paymentId, string userId)
        {
            await AutoCancelExpiredPaymentsAsync();
            return await _context.Payments
                .Include(p => p.Package)
                .Include(p => p.CreditPackage)
                .FirstOrDefaultAsync(p => p.Id == paymentId && p.UserId == userId);
        }

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
                    .Include(p => p.User)
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

                // Send invoice email asynchronously to user
                _ = Task.Run(() => SendInvoiceEmailAsync(payment));

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
        {
            await AutoCancelExpiredPaymentsAsync();
            return await _paymentRepo.GetUserPaymentsAsync(userId);
        }

        public async Task<IEnumerable<Payment>> GetAllPaymentsAsync()
        {
            await AutoCancelExpiredPaymentsAsync();
            return await _paymentRepo.GetAllWithUsersAsync();
        }

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
                .Include(p => p.User)
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

        public async Task UpdatePackagePriceAsync(int packageId, decimal newPrice)
        {
            var package = await _context.Packages.FirstOrDefaultAsync(p => p.Id == packageId);
            if (package != null)
            {
                package.Price = newPrice;
                package.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        private async Task SendInvoiceEmailAsync(Payment payment)
        {
            if (payment.User == null || string.IsNullOrEmpty(payment.User.Email)) return;

            var itemName = payment.Package?.Name ?? payment.CreditPackage?.Name ?? "Nạp số dư credit";
            var subject = $"[LMS AI] Hóa đơn thanh toán thành công - {payment.InvoiceNumber}";
            var body = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e2e8f0; border-radius: 8px;'>
                    <div style='text-align: center; border-bottom: 2px solid #3b82f6; padding-bottom: 20px; margin-bottom: 20px;'>
                        <h2 style='color: #3b82f6; margin: 0;'>LMS AI PLATFORM</h2>
                        <p style='color: #64748b; margin: 5px 0 0 0;'>Cảm ơn bạn đã đăng ký dịch vụ của chúng tôi!</p>
                    </div>
                    <p>Xin chào <strong>{payment.User.FullName}</strong>,</p>
                    <p>Giao dịch thanh toán của bạn đã được xác nhận thành công. Dưới đây là thông tin chi tiết hóa đơn dịch vụ:</p>
                    
                    <div style='background-color: #f8fafc; padding: 20px; border-radius: 6px; margin: 20px 0;'>
                        <table style='width: 100%; border-collapse: collapse; font-size: 0.9em;'>
                            <tr>
                                <td style='padding: 6px 0; color: #64748b;'>Mã hóa đơn:</td>
                                <td style='padding: 6px 0; font-weight: bold; text-align: right; font-family: monospace;'>{payment.InvoiceNumber}</td>
                            </tr>
                            <tr>
                                <td style='padding: 6px 0; color: #64748b;'>Mã giao dịch PayOS:</td>
                                <td style='padding: 6px 0; font-weight: bold; text-align: right; font-family: monospace;'>{payment.TransactionId}</td>
                            </tr>
                            <tr>
                                <td style='padding: 6px 0; color: #64748b;'>Ngày thanh toán:</td>
                                <td style='padding: 6px 0; text-align: right;'>{payment.PaidAt?.ToString("dd/MM/yyyy HH:mm:ss") ?? DateTime.UtcNow.ToString("dd/MM/yyyy HH:mm:ss")}</td>
                            </tr>
                            <tr>
                                <td style='padding: 6px 0; color: #64748b;'>Phương thức:</td>
                                <td style='padding: 6px 0; text-align: right;'>Cổng thanh toán PayOS</td>
                            </tr>
                            <tr style='border-top: 1px solid #e2e8f0;'>
                                <td style='padding: 12px 0 6px 0; font-weight: bold; color: #1e293b;'>Sản phẩm đăng ký:</td>
                                <td style='padding: 12px 0 6px 0; font-weight: bold; text-align: right; color: #3b82f6;'>{itemName}</td>
                            </tr>
                            <tr style='border-top: 2px solid #3b82f6;'>
                                <td style='padding: 12px 0 0 0; font-size: 1.1em; font-weight: bold; color: #1e293b;'>Tổng cộng:</td>
                                <td style='padding: 12px 0 0 0; font-size: 1.1em; font-weight: bold; text-align: right; color: #10b981;'>{payment.Amount.ToString("N0")} đ</td>
                            </tr>
                        </table>
                    </div>

                    <p style='font-size: 0.9em; color: #64748b;'>Ví tài khoản và các quyền lợi đi kèm gói dịch vụ đã được kích hoạt tự động trên hệ thống học tập.</p>
                    <p style='text-align: center; margin-top: 30px;'>
                        <a href='https://localhost:5034/' style='display: inline-block; padding: 10px 20px; background-color: #3b82f6; color: #fff; text-decoration: none; border-radius: 6px; font-weight: bold;'>Đi tới bảng điều khiển</a>
                    </p>
                    
                    <div style='margin-top: 40px; border-top: 1px solid #e2e8f0; padding-top: 15px; text-align: center; font-size: 0.8em; color: #94a3b8;'>
                        Đây là email tự động từ hệ thống LMS AI. Vui lòng không trả lời trực tiếp email này.
                    </div>
                </div>";

            try
            {
                await _emailService.SendEmailAsync(payment.User.Email, subject, body);
            }
            catch (Exception)
            {
                // Silent catch to not break payment callback if SMTP temporarily fails
            }
        }

        public async Task<Payment> ProcessSuccessRedirectAsync(int paymentId)
        {
            await AutoCancelExpiredPaymentsAsync();
            var payment = await _context.Payments
                .Include(p => p.Package)
                .Include(p => p.CreditPackage)
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == paymentId);

            if (payment == null)
            {
                throw new InvalidOperationException("Payment not found");
            }

            if (payment.Status == PaymentStatus.Completed)
            {
                return payment;
            }

            if (long.TryParse(payment.GatewayOrderCode, out var orderCode))
            {
                try
                {
                    var paymentLinkInfo = await _payOS.PaymentRequests.GetAsync(orderCode);
                    if (paymentLinkInfo != null && paymentLinkInfo.Status == PayOS.Models.V2.PaymentRequests.PaymentLinkStatus.Paid)
                    {
                        payment.Status = PaymentStatus.Processing;
                        payment.UpdatedAt = DateTime.UtcNow;
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

                        payment.Status = PaymentStatus.Completed;
                        payment.PaidAt = DateTime.UtcNow;
                        payment.UpdatedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();

                        // Send invoice email
                        _ = Task.Run(() => SendInvoiceEmailAsync(payment));
                    }
                }
                catch (Exception)
                {
                    // Fallback or log error
                }
            }

            return payment;
        }

        private async Task AutoCancelExpiredPaymentsAsync()
        {
            var expiredPayments = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Pending && p.ExpiredAt < DateTime.UtcNow)
                .ToListAsync();

            if (expiredPayments.Any())
            {
                foreach (var p in expiredPayments)
                {
                    p.Status = PaymentStatus.Expired;
                    p.UpdatedAt = DateTime.UtcNow;
                }
                await _context.SaveChangesAsync();
            }
        }

        private static string BuildInvoiceNumber(string prefix)
            => $"{prefix}-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}";
    }

    public interface IReportService
    {
        Task<DashboardStats> GetDashboardStatsAsync(string? userId = null, string? lecturerId = null);
        Task<IEnumerable<DailyQueryStat>> GetDailyQueryStatsAsync(int days = 30, string? userId = null, string? lecturerId = null);
        Task<IEnumerable<CourseQuizStat>> GetCourseQuizStatsAsync(string? userId = null, string? lecturerId = null);
        Task<IEnumerable<AppUser>> GetRecentUsersAsync(int count = 20);
        Task<int[]> GetQuizScoreDistributionAsync(string? userId = null, string? lecturerId = null);
        Task<IEnumerable<PackageRevenueDto>> GetRevenueByPackageAsync();
    }

    public record DashboardStats(int TotalUsers, int TotalDocuments, int TotalSessions, int TotalQuizAttempts, decimal TotalRevenue, int IndexedDocuments);
    public record DailyQueryStat(DateTime Date, int Count);
    public record CourseQuizStat(string CourseName, double AverageScore, int AttemptCount);
    public record PackageRevenueDto(string PackageName, int Count, decimal Total);

    public class ReportService : IReportService
    {
        private readonly AppDbContext _context;
        public ReportService(AppDbContext context) { _context = context; }

        public async Task<DashboardStats> GetDashboardStatsAsync(string? userId = null, string? lecturerId = null)
        {
            if (userId != null)
            {
                var studentCourseIds = await _context.ChatSessions
                    .Where(s => s.UserId == userId && s.CourseId.HasValue)
                    .Select(s => s.CourseId!.Value)
                    .Union(_context.QuizAttempts
                        .Where(a => a.UserId == userId)
                        .Select(a => a.Quiz.CourseId))
                    .Distinct()
                    .ToListAsync();

                var userDocs = await _context.Documents.CountAsync(d => d.Status == DocumentStatus.Indexed && studentCourseIds.Contains(d.CourseId));
                var userSessions = await _context.ChatSessions.CountAsync(s => s.UserId == userId);
                var userAttempts = await _context.QuizAttempts.CountAsync(a => a.UserId == userId && a.IsCompleted);
                var userQueries = await _context.ChatMessages
                    .CountAsync(m => m.ChatSession.UserId == userId && m.Role == MessageRole.User);

                return new DashboardStats(userQueries, userDocs, userSessions, userAttempts, 0, userDocs);
            }
            else if (lecturerId != null)
            {
                var lecturerStudents = await _context.QuizAttempts
                    .Where(a => a.Quiz.Course.LecturerId == lecturerId)
                    .Select(a => a.UserId)
                    .Distinct()
                    .CountAsync();
                var lecturerDocs = await _context.Documents.CountAsync(d => d.Course.LecturerId == lecturerId);
                var indexedDocs = await _context.Documents.CountAsync(d => d.Status == DocumentStatus.Indexed && d.Course.LecturerId == lecturerId);
                var lecturerSessions = await _context.ChatSessions.CountAsync(s => s.Course.LecturerId == lecturerId);
                var lecturerAttempts = await _context.QuizAttempts.CountAsync(a => a.IsCompleted && a.Quiz.Course.LecturerId == lecturerId);

                return new DashboardStats(lecturerStudents, lecturerDocs, lecturerSessions, lecturerAttempts, 0, indexedDocs);
            }
            else
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
        }

        public async Task<IEnumerable<DailyQueryStat>> GetDailyQueryStatsAsync(int days = 30, string? userId = null, string? lecturerId = null)
        {
            var from = DateTime.UtcNow.AddDays(-days);
            var query = _context.ChatMessages
                .Where(m => m.Role == MessageRole.User && m.CreatedAt >= from);

            if (userId != null)
            {
                query = query.Where(m => m.ChatSession.UserId == userId);
            }
            else if (lecturerId != null)
            {
                query = query.Where(m => m.ChatSession.Course.LecturerId == lecturerId);
            }

            var data = await query
                .GroupBy(m => m.CreatedAt.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .OrderBy(g => g.Date)
                .ToListAsync();

            return data.Select(d => new DailyQueryStat(d.Date, d.Count));
        }

        public async Task<IEnumerable<CourseQuizStat>> GetCourseQuizStatsAsync(string? userId = null, string? lecturerId = null)
        {
            var query = _context.QuizAttempts
                .Where(a => a.IsCompleted);

            if (userId != null)
            {
                query = query.Where(a => a.UserId == userId);
            }
            else if (lecturerId != null)
            {
                query = query.Where(a => a.Quiz.Course.LecturerId == lecturerId);
            }

            var data = await query
                .Include(a => a.Quiz).ThenInclude(q => q.Course)
                .GroupBy(a => a.Quiz.Course.Name)
                .Select(g => new { CourseName = g.Key, AvgScore = g.Average(a => a.Score), Count = g.Count() })
                .ToListAsync();

            return data.Select(d => new CourseQuizStat(d.CourseName, d.AvgScore, d.Count));
        }

        public async Task<IEnumerable<AppUser>> GetRecentUsersAsync(int count = 20)
            => await _context.Users
                .Include(u => u.CreditWallet)
                .OrderByDescending(u => u.CreatedAt)
                .Take(count)
                .ToListAsync();

        public async Task<int[]> GetQuizScoreDistributionAsync(string? userId = null, string? lecturerId = null)
        {
            var query = _context.QuizAttempts.Where(a => a.IsCompleted);
            if (userId != null)
            {
                query = query.Where(a => a.UserId == userId);
            }
            else if (lecturerId != null)
            {
                query = query.Where(a => a.Quiz.Course.LecturerId == lecturerId);
            }

            var attempts = await query.ToListAsync();
            return new[]
            {
                attempts.Count(a => a.Score >= 90),
                attempts.Count(a => a.Score >= 70 && a.Score < 90),
                attempts.Count(a => a.Score >= 50 && a.Score < 70),
                attempts.Count(a => a.Score < 50)
            };
        }

        public async Task<IEnumerable<PackageRevenueDto>> GetRevenueByPackageAsync()
        {
            return await _context.Payments
                .Where(p => p.Status == PaymentStatus.Completed)
                .Include(p => p.Package)
                .GroupBy(p => p.Package.Name)
                .Select(g => new PackageRevenueDto(g.Key, g.Count(), g.Sum(p => p.Amount)))
                .ToListAsync();
        }
    }

    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string body);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            var emailSettings = _configuration.GetSection("EmailSettings");
            var smtpServer = emailSettings.GetValue<string>("SmtpServer") ?? "smtp.gmail.com";
            var port = emailSettings.GetValue<int>("Port", 587);
            var senderEmail = emailSettings.GetValue<string>("SenderEmail");
            var senderName = emailSettings.GetValue<string>("SenderName") ?? "LMS AI System";
            var password = emailSettings.GetValue<string>("Password");
            var enableSsl = emailSettings.GetValue<bool>("EnableSsl", true);

            if (string.IsNullOrEmpty(senderEmail) || string.IsNullOrEmpty(password))
            {
                throw new InvalidOperationException("Email settings (SenderEmail/Password) are not configured in appsettings.json.");
            }

            using var message = new System.Net.Mail.MailMessage();
            message.From = new System.Net.Mail.MailAddress(senderEmail, senderName);
            message.To.Add(new System.Net.Mail.MailAddress(toEmail));
            message.Subject = subject;
            message.Body = body;
            message.IsBodyHtml = true;

            using var smtp = new System.Net.Mail.SmtpClient(smtpServer, port);
            smtp.Credentials = new System.Net.NetworkCredential(senderEmail, password);
            smtp.EnableSsl = enableSsl;
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
        Task DeleteBatchQuestionsAsync(int courseId, DateTime batchCreatedAt);
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

        public async Task DeleteBatchQuestionsAsync(int courseId, DateTime batchCreatedAt)
        {
            var minTime = batchCreatedAt.AddSeconds(-5);
            var maxTime = batchCreatedAt.AddSeconds(5);
            var items = await _context.QuestionBankItems
                .Where(q => q.CourseId == courseId && q.CreatedAt >= minTime && q.CreatedAt <= maxTime)
                .ToListAsync();

            if (items.Any())
            {
                _context.QuestionBankItems.RemoveRange(items);
                await _context.SaveChangesAsync();
            }
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

    public interface IBenchmarkService
    {
        Task<IEnumerable<BenchmarkRun>> GetBenchmarkRunsAsync();
        Task CreateBenchmarkRunAsync(BenchmarkRun run);
    }

    public class BenchmarkService : IBenchmarkService
    {
        private readonly AppDbContext _context;

        public BenchmarkService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<BenchmarkRun>> GetBenchmarkRunsAsync()
            => await _context.BenchmarkRuns.OrderByDescending(r => r.RunAt).ToListAsync();

        public async Task CreateBenchmarkRunAsync(BenchmarkRun run)
        {
            _context.BenchmarkRuns.Add(run);
            await _context.SaveChangesAsync();
        }
    }

    public interface IUserService
    {
        Task<IdentityResult> CreateSingleUserAsync(string fullName, string email, string role, string loginBaseUrl);
        Task<UserBulkImportResult> CreateBulkUsersAsync(IEnumerable<UserBulkRow> rows, string loginBaseUrl);
        Task<IdentityResult> EditUserAsync(string currentUserId, string editUserId, bool isActive);
    }

    public record UserBulkRow(string FullName, string Email, string Role);
    public record UserBulkImportResult(int SuccessCount, List<string> Errors);

    public class UserService : IUserService
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly ICreditWalletService _walletService;
        private readonly ISubscriptionService _subscriptionService;
        private readonly IEmailService _emailService;

        public UserService(
            UserManager<AppUser> userManager,
            ICreditWalletService walletService,
            ISubscriptionService subscriptionService,
            IEmailService emailService)
        {
            _userManager = userManager;
            _walletService = walletService;
            _subscriptionService = subscriptionService;
            _emailService = emailService;
        }

        public async Task<IdentityResult> CreateSingleUserAsync(string fullName, string email, string role, string loginBaseUrl)
        {
            var password = GenerateRandomPassword();
            var user = new AppUser
            {
                FullName = fullName,
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                var targetRole = new[] { "Student", "Lecturer", "Admin" }.Contains(role) ? role : "Student";
                await _userManager.AddToRoleAsync(user, targetRole);

                if (targetRole == "Student")
                {
                    await _walletService.EnsureWalletAsync(user.Id, ["Student"]);
                    await _subscriptionService.ActivatePackageAsync(user.Id, 10);
                }
                else
                {
                    await _walletService.EnsureWalletAsync(user.Id, [targetRole]);
                }

                // Send email notification
                await SendAccountEmailAsync(fullName, email, password, loginBaseUrl);
            }
            return result;
        }

        public async Task<UserBulkImportResult> CreateBulkUsersAsync(IEnumerable<UserBulkRow> rows, string loginBaseUrl)
        {
            int successCount = 0;
            var errors = new List<string>();

            foreach (var row in rows)
            {
                var targetRole = new[] { "Student", "Lecturer", "Admin" }.Contains(row.Role) ? row.Role : "Student";
                var password = GenerateRandomPassword();
                var user = new AppUser
                {
                    FullName = row.FullName,
                    UserName = row.Email,
                    Email = row.Email,
                    EmailConfirmed = true
                };

                var result = await _userManager.CreateAsync(user, password);
                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, targetRole);

                    if (targetRole == "Student")
                    {
                        await _walletService.EnsureWalletAsync(user.Id, ["Student"]);
                        await _subscriptionService.ActivatePackageAsync(user.Id, 10);
                    }
                    else
                    {
                        await _walletService.EnsureWalletAsync(user.Id, [targetRole]);
                    }

                    try
                    {
                        await SendAccountEmailAsync(row.FullName, row.Email, password, loginBaseUrl);
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Đã tạo {row.Email} nhưng gửi mail lỗi: {ex.Message}");
                    }

                    successCount++;
                }
                else
                {
                    errors.Add($"Lỗi tạo {row.Email}: {string.Join("; ", result.Errors.Select(e => e.Description))}");
                }
            }

            return new UserBulkImportResult(successCount, errors);
        }

        public async Task<IdentityResult> EditUserAsync(string currentUserId, string editUserId, bool isActive)
        {
            if (currentUserId == editUserId)
            {
                return IdentityResult.Failed(new IdentityError { Description = "Bạn không thể tự thay đổi trạng thái của chính mình." });
            }

            var user = await _userManager.FindByIdAsync(editUserId);
            if (user == null)
            {
                return IdentityResult.Failed(new IdentityError { Description = "Người dùng không tồn tại." });
            }

            var targetRoles = await _userManager.GetRolesAsync(user);
            if (targetRoles.Contains("Admin"))
            {
                return IdentityResult.Failed(new IdentityError { Description = "Bạn không thể thay đổi thông tin của tài khoản quản trị viên khác." });
            }

            user.IsActive = isActive;
            return await _userManager.UpdateAsync(user);
        }

        private string GenerateRandomPassword()
        {
            return "P@ss" + Guid.NewGuid().ToString("N")[..8];
        }

        private async Task SendAccountEmailAsync(string fullName, string email, string password, string loginBaseUrl)
        {
            var subject = "Thông tin tài khoản hệ thống LMS AI của bạn";
            var body = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e2e8f0; border-radius: 8px;'>
                    <h2 style='color: #3b82f6;'>LMS AI Platform</h2>
                    <p>Xin chào <strong>{fullName}</strong>,</p>
                    <p>Tài khoản học tập và giảng dạy của bạn trên hệ thống LMS AI đã được khởi tạo bởi Quản trị viên.</p>
                    <div style='background-color: #f8fafc; padding: 15px; border-radius: 6px; margin: 20px 0; border-left: 4px solid #3b82f6;'>
                        <p style='margin: 4px 0;'><strong>Tài khoản đăng nhập:</strong> <span style='font-family: monospace;'>{email}</span></p>
                        <p style='margin: 4px 0;'><strong>Mật khẩu đăng nhập:</strong> <span style='font-family: monospace;'>{password}</span></p>
                    </div>
                    <p>Vui lòng nhấp vào liên kết bên dưới để đăng nhập ngay:</p>
                    <p style='text-align: center;'>
                        <a href='{loginBaseUrl}' style='display: inline-block; padding: 10px 20px; background-color: #3b82f6; color: #fff; text-decoration: none; border-radius: 6px; font-weight: bold;'>Đăng Nhập Ngay</a>
                    </p>
                    <p style='color: #64748b; font-size: 0.85em; margin-top: 30px;'>
                        * Lưu ý bảo mật: Hãy thay đổi mật khẩu ngay sau lần đăng nhập đầu tiên để đảm bảo an toàn cho tài khoản của bạn.
                    </p>
                </div>";

            await _emailService.SendEmailAsync(email, subject, body);
        }
    }
}
