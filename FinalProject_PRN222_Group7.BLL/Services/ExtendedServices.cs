using FinalProject_PRN222_Group7.DAL.Data;
using FinalProject_PRN222_Group7.DAL.Entities;
using FinalProject_PRN222_Group7.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace FinalProject_PRN222_Group7.BLL.Services
{
    // ============ QUIZ SERVICE ============
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

            int index = 0;
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

            int correct = 0;
            foreach (var q in attempt.Quiz.Questions)
            {
                if (answers.TryGetValue(q.Id, out char selected) && selected == q.CorrectAnswer)
                    correct++;
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
            if (quiz != null)
            {
                _context.Questions.RemoveRange(quiz.Questions);
                _context.QuizAttempts.RemoveRange(quiz.Attempts);
                _context.Quizzes.Remove(quiz);
                await _context.SaveChangesAsync();
            }
        }
    }

    // ============ PAYMENT SERVICE ============
    public interface IPaymentService
    {
        Task<IEnumerable<Package>> GetPackagesAsync();
        Task<UserPackage?> GetUserPackageAsync(string userId);
        Task<Payment> CreatePaymentAsync(string userId, int packageId);
        Task<Payment> CompletePaymentAsync(int paymentId);
        Task<IEnumerable<Payment>> GetUserPaymentsAsync(string userId);
        Task<IEnumerable<Payment>> GetAllPaymentsAsync();
    }

    public class PaymentService : IPaymentService
    {
        private readonly AppDbContext _context;
        private readonly IPaymentRepository _paymentRepo;

        public PaymentService(AppDbContext context, IPaymentRepository paymentRepo)
        {
            _context = context;
            _paymentRepo = paymentRepo;
        }

        public async Task<IEnumerable<Package>> GetPackagesAsync()
            => await _context.Packages.Where(p => p.IsActive).OrderBy(p => p.Price).ToListAsync();

        public async Task<UserPackage?> GetUserPackageAsync(string userId)
            => await _context.UserPackages
                .Include(up => up.Package)
                .FirstOrDefaultAsync(up => up.UserId == userId && up.IsActive && up.EndDate > DateTime.UtcNow);

        public async Task<Payment> CreatePaymentAsync(string userId, int packageId)
        {
            var pkg = await _context.Packages.FindAsync(packageId)
                ?? throw new InvalidOperationException("Package not found");

            var invoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}";

            var payment = new Payment
            {
                UserId = userId,
                PackageId = packageId,
                Amount = pkg.Price,
                InvoiceNumber = invoiceNumber,
                Status = PaymentStatus.Pending
            };
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();
            return payment;
        }

        public async Task<Payment> CompletePaymentAsync(int paymentId)
        {
            var payment = await _context.Payments.Include(p => p.Package)
                .FirstOrDefaultAsync(p => p.Id == paymentId)
                ?? throw new InvalidOperationException("Payment not found");

            payment.Status = PaymentStatus.Completed;
            payment.PaidAt = DateTime.UtcNow;
            payment.TransactionId = $"TXN-{Guid.NewGuid().ToString()[..12].ToUpper()}";

            // Deactivate old package
            var oldPkg = await _context.UserPackages
                .FirstOrDefaultAsync(up => up.UserId == payment.UserId && up.IsActive);
            if (oldPkg != null) { oldPkg.IsActive = false; }

            // Create new UserPackage
            var userPkg = new UserPackage
            {
                UserId = payment.UserId,
                PackageId = payment.PackageId,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(1),
                RemainingQueries = payment.Package.MonthlyAiQueries == -1 ? int.MaxValue : payment.Package.MonthlyAiQueries,
                IsActive = true
            };
            _context.UserPackages.Add(userPkg);
            await _context.SaveChangesAsync();
            return payment;
        }

        public async Task<IEnumerable<Payment>> GetUserPaymentsAsync(string userId)
            => await _paymentRepo.GetUserPaymentsAsync(userId);

        public async Task<IEnumerable<Payment>> GetAllPaymentsAsync()
            => await _paymentRepo.GetAllWithUsersAsync();
    }

    // ============ REPORT SERVICE ============
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

    // ============ EMAIL SERVICE ============
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string body);
    }

    public class EmailService : IEmailService
    {
        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            using (var message = new System.Net.Mail.MailMessage())
            {
                message.From = new System.Net.Mail.MailAddress("vinhmtse180031@fpt.edu.vn", "LMS AI System");
                message.To.Add(new System.Net.Mail.MailAddress(toEmail));
                message.Subject = subject;
                message.Body = body;
                message.IsBodyHtml = true;

                using (var smtp = new System.Net.Mail.SmtpClient("smtp.gmail.com", 587))
                {
                    smtp.Credentials = new System.Net.NetworkCredential("vinhmtse180031@fpt.edu.vn", "xwhb ekqi mmhe ceas");
                    smtp.EnableSsl = true;
                    await smtp.SendMailAsync(message);
                }
            }
        }
    }

    // ============ QUESTION BANK SERVICE ============
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
                if (!existingContents.Contains(normalizedContent))
                {
                    item.CourseId = courseId;
                    item.ChapterId = chapterId;
                    item.DocumentId = documentId;
                    item.CreatedAt = DateTime.UtcNow;

                    _context.QuestionBankItems.Add(item);
                    addedItems.Add(item);
                    existingContents.Add(normalizedContent);
                }
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
            if (item != null)
            {
                _context.QuestionBankItems.Remove(item);
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
            var chosenItems = bankItems.OrderBy(x => random.Next()).Take(numQuestions).ToList();

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

            int idx = 0;
            foreach (var item in chosenItems)
            {
                var q = new Question
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
                };
                _context.Questions.Add(q);
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
            var chosenItems = bankItems.OrderBy(x => random.Next()).Take(numQuestions).ToList();

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

            int idx = 0;
            foreach (var item in chosenItems)
            {
                var q = new Question
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
                };
                _context.Questions.Add(q);
            }
            await _context.SaveChangesAsync();

            return quiz;
        }
    }
}
