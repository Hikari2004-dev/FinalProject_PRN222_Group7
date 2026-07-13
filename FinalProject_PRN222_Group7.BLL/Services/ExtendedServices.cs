using FinalProject_PRN222_Group7.DAL.Data;
using FinalProject_PRN222_Group7.DAL.Entities;
using FinalProject_PRN222_Group7.DAL.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Net.Http.Json;
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
        Task<bool> GenerateQuizFromDocumentAsync(int documentId, int numQuestions, string title, string lecturerId);
        Task<IEnumerable<QuizAttempt>> GetUserAttemptsAsync(string userId);
        Task<IEnumerable<Document>> GetIndexedDocumentsForLecturerAsync(string lecturerId);
        Task<IEnumerable<Document>> GetIndexedDocumentsAsync();
    }

    public class QuizService : IQuizService
    {
        private readonly IQuizRepository _repo;
        private readonly AppDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public QuizService(IQuizRepository repo, AppDbContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _repo = repo;
            _context = context;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
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

        public async Task<IEnumerable<QuizAttempt>> GetUserAttemptsAsync(string userId)
        {
            return await _context.QuizAttempts
                .Where(a => a.UserId == userId && a.IsCompleted)
                .OrderByDescending(a => a.CompletedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<Document>> GetIndexedDocumentsForLecturerAsync(string lecturerId)
        {
            return await _context.Documents
                .Include(d => d.Course)
                .Where(d => d.Status == DocumentStatus.Indexed && d.Course.LecturerId == lecturerId)
                .ToListAsync();
        }

        public async Task<IEnumerable<Document>> GetIndexedDocumentsAsync()
        {
            return await _context.Documents
                .Include(d => d.Course)
                .Where(d => d.Status == DocumentStatus.Indexed)
                .ToListAsync();
        }

        private static int _quizKeyIndex = 0;
        private static readonly object _quizKeyLock = new object();

        private string GetNextApiKey(List<string> keys)
        {
            if (keys == null || !keys.Any()) return string.Empty;
            lock (_quizKeyLock)
            {
                if (_quizKeyIndex >= keys.Count) _quizKeyIndex = 0;
                var key = keys[_quizKeyIndex];
                _quizKeyIndex = (_quizKeyIndex + 1) % keys.Count;
                return key;
            }
        }

        public async Task<bool> GenerateQuizFromDocumentAsync(int documentId, int numQuestions, string title, string lecturerId)
        {
            var doc = await _context.Documents
                .Include(d => d.Course)
                .FirstOrDefaultAsync(d => d.Id == documentId);

            if (doc == null || doc.Course.LecturerId != lecturerId)
                return false;

            // 1. Lấy ra tất cả các câu hỏi hiện có trong Kho câu hỏi của môn học này
            var existingQuestions = await _context.QuestionBankItems
                .Where(q => q.CourseId == doc.CourseId)
                .Select(q => q.Content)
                .Distinct()
                .ToListAsync();

            // 2. Lấy nội dung các phân mảnh của tài liệu làm ngữ cảnh (context)
            var chunks = await _context.DocumentChunks
                .Where(c => c.DocumentId == documentId)
                .OrderBy(c => c.ChunkIndex)
                .Select(c => c.Content)
                .ToListAsync();
            var contextText = string.Join("\n", chunks);

            // 3. Cấu hình Gemini Key xoay vòng
            var geminiSection = _configuration.GetSection("Gemini");
            var apiKeys = geminiSection.GetSection("ApiKeys").Get<List<string>>() ?? new List<string>();
            var model = geminiSection.GetValue<string>("Model") ?? "gemini-2.5-flash";

            List<Question> questions = new List<Question>();
            bool callSuccess = false;

            if (apiKeys.Any())
            {
                var client = _httpClientFactory.CreateClient();
                int retries = 0;
                int maxRetries = apiKeys.Count;

                while (!callSuccess && retries < maxRetries)
                {
                    var apiKey = GetNextApiKey(apiKeys);
                    if (string.IsNullOrEmpty(apiKey) || apiKey.Contains("YOUR-"))
                    {
                        retries++;
                        continue;
                    }

                    var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

                    var prompt = $"Dựa vào nội dung tài liệu học tập sau đây:\n\n{contextText}\n\n" +
                                 $"Hãy tạo ra đúng {numQuestions} câu hỏi trắc nghiệm khách quan mới để kiểm tra kiến thức.\n" +
                                 $"Mỗi câu hỏi phải có 4 phương án lựa chọn A, B, C, D và có đáp án đúng kèm theo lời giải thích ngắn gọn.\n\n" +
                                 $"RÀNG BUỘC CỰC KỲ QUAN TRỌNG:\n" +
                                 $"- Bạn KHÔNG ĐƯỢC tạo các câu hỏi trùng lặp hoặc có nội dung tương tự với danh sách các câu hỏi đã có sau đây:\n" +
                                 $"{(existingQuestions.Any() ? string.Join("\n- ", existingQuestions.Take(150)) : "(Chưa có câu hỏi nào trong kho)")}\n\n" +
                                 $"- Định dạng kết quả trả về phải là một mảng JSON (JSON Array) của các đối tượng câu hỏi với các trường chính xác như sau:\n" +
                                 $"[\n" +
                                 $"  {{\n" +
                                 $"    \"content\": \"Nội dung câu hỏi mới\",\n" +
                                 $"    \"optionA\": \"Nội dung phương án A\",\n" +
                                 $"    \"optionB\": \"Nội dung phương án B\",\n" +
                                 $"    \"optionC\": \"Nội dung phương án C\",\n" +
                                 $"    \"optionD\": \"Nội dung phương án D\",\n" +
                                 $"    \"correctAnswer\": \"A\",\n" +
                                 $"    \"explanation\": \"Giải thích tại sao đúng\"\n" +
                                 $"  }}\n" +
                                 $"]\n\n" +
                                 $"Chỉ trả về chuỗi JSON thô hợp lệ, không bọc trong khối code ```json. Không chứa bất kỳ thông tin thừa nào ngoài JSON Array.";

                    var payload = new
                    {
                        contents = new[]
                        {
                            new { parts = new[] { new { text = prompt } } }
                        },
                        generationConfig = new
                        {
                            responseMimeType = "application/json"
                        }
                    };

                    try
                    {
                        var response = await client.PostAsJsonAsync(url, payload);
                        if (response.IsSuccessStatusCode)
                        {
                            var geminiResult = await response.Content.ReadFromJsonAsync<GeminiGenerateResponseDto>();
                            var jsonText = geminiResult?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
                            if (!string.IsNullOrEmpty(jsonText))
                            {
                                questions = JsonSerializer.Deserialize<List<Question>>(jsonText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<Question>();
                                callSuccess = true;
                            }
                            else
                            {
                                retries++;
                            }
                        }
                        else
                        {
                            retries++;
                        }
                    }
                    catch
                    {
                        retries++;
                    }
                }

                if (!callSuccess)
                {
                    questions = GenerateMockQuestions(numQuestions);
                }
            }
            else
            {
                questions = GenerateMockQuestions(numQuestions);
            }

            // Save to Question Bank
            foreach (var q in questions)
            {
                var exists = await _context.QuestionBankItems.AnyAsync(qi => qi.CourseId == doc.CourseId && qi.Content == q.Content);
                if (!exists)
                {
                    _context.QuestionBankItems.Add(new QuestionBankItem
                    {
                        CourseId = doc.CourseId,
                        ChapterId = doc.ChapterId,
                        DocumentId = documentId,
                        Content = q.Content,
                        OptionA = q.OptionA,
                        OptionB = q.OptionB,
                        OptionC = q.OptionC,
                        OptionD = q.OptionD,
                        CorrectAnswer = q.CorrectAnswer,
                        Explanation = q.Explanation,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
            await _context.SaveChangesAsync();

            // Save to Quiz
            var quiz = new Quiz
            {
                Title = title,
                CourseId = doc.CourseId,
                DocumentId = documentId,
                IsAiGenerated = true,
                TimeLimit = numQuestions * 2,
                TotalQuestions = questions.Count,
                CreatedAt = DateTime.UtcNow
            };

            await CreateQuizAsync(quiz, questions);
            return true;
        }

        private List<Question> GenerateMockQuestions(int count)
        {
            var questions = new List<Question>();
            for (int i = 0; i < count; i++)
            {
                questions.Add(new Question
                {
                    Content = $"Câu hỏi mẫu số {i + 1}: Đây là câu hỏi về nội dung tài liệu học tập?",
                    OptionA = "Đáp án A (đúng)",
                    OptionB = "Đáp án B",
                    OptionC = "Đáp án C",
                    OptionD = "Đáp án D",
                    CorrectAnswer = 'A',
                    Explanation = "Giải thích: Đây là câu trả lời đúng vì nó phù hợp với nội dung tài liệu."
                });
            }
            return questions;
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
        Task<IEnumerable<QuizAttempt>> GetCompletedQuizAttemptsAsync();
        Task<Dictionary<string, decimal>> GetRevenueByPackageAsync();
        Task<IEnumerable<PackageRevenueDto>> GetPackageRevenuesAsync();
    }

    public record DashboardStats(int TotalUsers, int TotalDocuments, int TotalSessions, int TotalQuizAttempts, decimal TotalRevenue, int IndexedDocuments);
    public record DailyQueryStat(DateTime Date, int Count);
    public record CourseQuizStat(string CourseName, double AverageScore, int AttemptCount);
    public record PackageRevenueDto(string PackageName, int Count, decimal Total);

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

        public async Task<IEnumerable<QuizAttempt>> GetCompletedQuizAttemptsAsync()
            => await _context.QuizAttempts.Where(a => a.IsCompleted).ToListAsync();

        public async Task<Dictionary<string, decimal>> GetRevenueByPackageAsync()
        {
            return await _context.Payments
                .Include(p => p.Package)
                .GroupBy(p => p.Package.Name)
                .ToDictionaryAsync(g => g.Key, g => g.Sum(p => p.Amount));
        }

        public async Task<IEnumerable<PackageRevenueDto>> GetPackageRevenuesAsync()
        {
            return await _context.Payments
                .Where(p => p.Status == PaymentStatus.Completed)
                .Include(p => p.Package)
                .GroupBy(p => p.Package.Name)
                .Select(g => new PackageRevenueDto(g.Key, g.Count(), g.Sum(p => p.Amount)))
                .ToListAsync();
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



    // ============ BENCHMARK SERVICE ============
    public interface IBenchmarkService
    {
        Task<IEnumerable<BenchmarkRun>> GetAllRunsAsync();
        Task AddRunAsync(BenchmarkRun run);
    }

    public class BenchmarkService : IBenchmarkService
    {
        private readonly AppDbContext _context;
        public BenchmarkService(AppDbContext context) => _context = context;

        public async Task<IEnumerable<BenchmarkRun>> GetAllRunsAsync()
            => await _context.BenchmarkRuns.OrderByDescending(r => r.RunAt).ToListAsync();

        public async Task AddRunAsync(BenchmarkRun run)
        {
            _context.BenchmarkRuns.Add(run);
            await _context.SaveChangesAsync();
        }
    }

    // ============ DASHBOARD SERVICE ============
    public interface IDashboardService
    {
        Task<int> GetCoursesCountAsync();
        Task<int> GetDocumentsCountAsync();
        Task<int> GetQuizzesCountAsync();
        Task<int> GetStudentsCountAsync();
        Task<IEnumerable<QuizAttempt>> GetRecentAttemptsAsync(string userId, string role, int take);
        Task<UserPackage?> GetActiveUserPackageAsync(string userId);
    }

    public class DashboardService : IDashboardService
    {
        private readonly AppDbContext _context;
        public DashboardService(AppDbContext context) => _context = context;

        public async Task<int> GetCoursesCountAsync() => await _context.Courses.CountAsync();
        public async Task<int> GetDocumentsCountAsync() => await _context.Documents.CountAsync();
        public async Task<int> GetQuizzesCountAsync() => await _context.Quizzes.CountAsync();
        public async Task<int> GetStudentsCountAsync() => await _context.Users.CountAsync();

        public async Task<IEnumerable<QuizAttempt>> GetRecentAttemptsAsync(string userId, string role, int take)
        {
            var query = _context.QuizAttempts.Include(a => a.Quiz).ThenInclude(q => q.Course).AsQueryable();
            if (role == "Student")
            {
                query = query.Where(a => a.UserId == userId);
            }
            else if (role == "Lecturer")
            {
                query = query.Where(a => a.Quiz.Course.LecturerId == userId);
            }
            return await query.OrderByDescending(a => a.CompletedAt).Take(take).ToListAsync();
        }

        public async Task<UserPackage?> GetActiveUserPackageAsync(string userId)
        {
            return await _context.UserPackages
                .Include(up => up.Package)
                .FirstOrDefaultAsync(up => up.UserId == userId && up.IsActive);
        }
    }

    // ============ ADMIN USER SERVICE ============
    public interface IAdminUserService
    {
        Task<IEnumerable<AppUser>> GetRecentUsersAsync(int take);
        Task<UserPackage?> GetUserPackageAsync(string userId);
        Task AssignUserPackageAsync(string userId, int packageId);
        Task<bool> CreateUserWithPackageAsync(AppUser user, string password, string role, int packageId);
    }

    public class AdminUserService : IAdminUserService
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public AdminUserService(AppDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IEnumerable<AppUser>> GetRecentUsersAsync(int take)
        {
            return await _context.Users
                .OrderByDescending(u => u.Id)
                .Take(take)
                .ToListAsync();
        }

        public async Task<UserPackage?> GetUserPackageAsync(string userId)
        {
            return await _context.UserPackages
                .Include(up => up.Package)
                .FirstOrDefaultAsync(up => up.UserId == userId && up.IsActive);
        }

        public async Task AssignUserPackageAsync(string userId, int packageId)
        {
            var active = await _context.UserPackages
                .FirstOrDefaultAsync(up => up.UserId == userId && up.IsActive);
            if (active != null)
            {
                active.IsActive = false;
            }

            var pkg = await _context.Packages.FindAsync(packageId);
            int queries = pkg?.MonthlyAiQueries == -1 ? int.MaxValue : pkg?.MonthlyAiQueries ?? 50;

            _context.UserPackages.Add(new UserPackage
            {
                UserId = userId,
                PackageId = packageId,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddYears(1),
                RemainingQueries = queries,
                IsActive = true
            });
            await _context.SaveChangesAsync();
        }

        public async Task<bool> CreateUserWithPackageAsync(AppUser user, string password, string role, int packageId)
        {
            var result = await _userManager.CreateAsync(user, password);
            if (!result.Succeeded) return false;

            await _userManager.AddToRoleAsync(user, role);

            if (role == "Student")
            {
                var pkg = await _context.Packages.FindAsync(packageId);
                int queries = pkg?.MonthlyAiQueries == -1 ? int.MaxValue : pkg?.MonthlyAiQueries ?? 50;

                _context.UserPackages.Add(new UserPackage
                {
                    UserId = user.Id,
                    PackageId = packageId,
                    StartDate = DateTime.UtcNow,
                    EndDate = DateTime.UtcNow.AddYears(1),
                    RemainingQueries = queries,
                    IsActive = true
                });
                await _context.SaveChangesAsync();
            }
            return true;
        }
    }

    public class GeminiGenerateResponseDto
    {
        public List<CandidateDto>? Candidates { get; set; }
    }
    public class CandidateDto
    {
        public ContentDto? Content { get; set; }
    }
    public class ContentDto
    {
        public List<PartDto>? Parts { get; set; }
    }
    public class PartDto
    {
        public string? Text { get; set; }
    }
}
