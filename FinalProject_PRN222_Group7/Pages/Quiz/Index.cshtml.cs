using FinalProject_PRN222_Group7.BLL.Services;
using FinalProject_PRN222_Group7.DAL.Data;
using FinalProject_PRN222_Group7.DAL.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace FinalProject_PRN222_Group7.Pages.Quiz
{
    public class IndexModel : PageModel
    {
        private readonly IQuizService _quizService;
        private readonly IDocumentService _docService;
        private readonly ICourseService _courseService;
        private readonly IQuestionBankService _questionBankService;
        private readonly UserManager<AppUser> _userManager;
        private readonly AppDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public IndexModel(
            IQuizService quizService,
            IDocumentService docService,
            ICourseService courseService,
            IQuestionBankService questionBankService,
            UserManager<AppUser> userManager,
            AppDbContext context,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _quizService = quizService;
            _docService = docService;
            _courseService = courseService;
            _questionBankService = questionBankService;
            _userManager = userManager;
            _context = context;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        public IEnumerable<DAL.Entities.Quiz> Quizzes { get; set; } = new List<DAL.Entities.Quiz>();
        public IEnumerable<QuizAttempt> UserAttempts { get; set; } = new List<QuizAttempt>();
        public IEnumerable<Document> IndexedDocuments { get; set; } = new List<Document>();
        public IEnumerable<Course> Courses { get; set; } = new List<Course>();

        private static int _keyIndex = 0;
        private static readonly object _keyLock = new object();

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

        public string CurrentUserId { get; set; } = string.Empty;

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            var courses = await _courseService.GetAllCoursesAsync();
            Courses = courses;
            var allQuizzes = new List<DAL.Entities.Quiz>();

            foreach (var course in courses)
            {
                var quizzes = await _quizService.GetByCourseAsync(course.Id);
                allQuizzes.AddRange(quizzes);
            }

            Quizzes = allQuizzes.OrderByDescending(q => q.CreatedAt);

            if (user != null)
            {
                CurrentUserId = user.Id;
                UserAttempts = await _context.QuizAttempts
                    .Where(a => a.UserId == user.Id && a.IsCompleted)
                    .OrderByDescending(a => a.CompletedAt)
                    .ToListAsync();

                var isLecturer = User.IsInRole("Lecturer");
                if (isLecturer)
                {
                    IndexedDocuments = await _context.Documents
                        .Include(d => d.Course)
                        .Where(d => d.Status == DocumentStatus.Indexed && d.Course.LecturerId == user.Id)
                        .ToListAsync();
                }
                else
                {
                    IndexedDocuments = await _context.Documents
                        .Include(d => d.Course)
                        .Where(d => d.Status == DocumentStatus.Indexed)
                        .ToListAsync();
                }
            }
        }

        public async Task<IActionResult> OnPostGenerateQuizAsync(int documentId, int numQuestions, string title)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return new JsonResult(new { success = false, error = "Vui lòng đăng nhập lại." });

            var isLecturer = User.IsInRole("Lecturer");
            if (!isLecturer)
            {
                return new JsonResult(new { success = false, error = "Bạn không có quyền thực hiện chức năng này." });
            }

            var doc = await _context.Documents
                .Include(d => d.Course)
                .FirstOrDefaultAsync(d => d.Id == documentId);

            if (doc == null || doc.Course.LecturerId != user.Id)
                return new JsonResult(new { success = false, error = "Tài liệu không tồn tại hoặc bạn không có quyền sở hữu môn học này." });

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
                string lastErrorMsg = "";

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
                            var geminiResult = await response.Content.ReadFromJsonAsync<GeminiGenerateResponse>();
                            var jsonText = geminiResult?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
                            if (!string.IsNullOrEmpty(jsonText))
                            {
                                questions = JsonSerializer.Deserialize<List<Question>>(jsonText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<Question>();
                                callSuccess = true;
                            }
                            else
                            {
                                lastErrorMsg = "Gemini API trả về nội dung rỗng.";
                                retries++;
                            }
                        }
                        else
                        {
                            var errBody = await response.Content.ReadAsStringAsync();
                            lastErrorMsg = $"HTTP {response.StatusCode} - {errBody}";
                            retries++;
                        }
                    }
                    catch (Exception ex)
                    {
                        lastErrorMsg = ex.Message;
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

            // Save to Question Bank (filtering duplicates inside BLL)
            var bankItems = questions.Select(q => new QuestionBankItem
            {
                Content = q.Content,
                OptionA = q.OptionA,
                OptionB = q.OptionB,
                OptionC = q.OptionC,
                OptionD = q.OptionD,
                CorrectAnswer = q.CorrectAnswer,
                Explanation = q.Explanation
            }).ToList();

            await _questionBankService.SaveQuestionsToBankAsync(
                doc.CourseId,
                doc.ChapterId,
                documentId,
                bankItems
            );

            // Save to Quiz
            var quiz = new DAL.Entities.Quiz
            {
                Title = title,
                CourseId = doc.CourseId,
                DocumentId = documentId,
                IsAiGenerated = true,
                TimeLimit = numQuestions * 2,
                TotalQuestions = questions.Count,
                CreatedAt = DateTime.UtcNow
            };

            await _quizService.CreateQuizAsync(quiz, questions);

            return new JsonResult(new { success = true });
        }

        private static List<Question> GenerateMockQuestions(int count)
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

        private class GeminiGenerateResponse
        {
            public List<Candidate>? Candidates { get; set; }
        }
        private class Candidate
        {
            public ContentObj? Content { get; set; }
        }
        private class ContentObj
        {
            public List<PartObj>? Parts { get; set; }
        }
        private class PartObj
        {
            public string? Text { get; set; }
        }

        // ============ ADDITIONAL HANDLERS FOR QUESTION BANK & RANDOM QUIZ ============
        public async Task<IActionResult> OnGetBankQuestionsAsync(int courseId, int? chapterId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return new JsonResult(new { success = false, error = "Vui lòng đăng nhập lại." });

            var course = await _courseService.GetCourseAsync(courseId);
            if (course == null) return new JsonResult(new { success = false, error = "Môn học không tồn tại." });

            var isAdmin = User.IsInRole("Admin");
            if (!isAdmin && course.LecturerId != user.Id)
                return new JsonResult(new { success = false, error = "Bạn không có quyền quản lý kho câu hỏi của môn học này." });

            var questions = await _questionBankService.GetQuestionsByCourseAsync(courseId, chapterId);
            var result = questions.Select(q => new
            {
                id = q.Id,
                content = q.Content,
                optionA = q.OptionA,
                optionB = q.OptionB,
                optionC = q.OptionC,
                optionD = q.OptionD,
                correctAnswer = q.CorrectAnswer.ToString(),
                explanation = q.Explanation
            });

            return new JsonResult(new { success = true, questions = result });
        }

        public async Task<IActionResult> OnPostEditBankQuestionAsync(int id, string content, string optionA, string optionB, string optionC, string optionD, string correctAnswer, string? explanation)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return new JsonResult(new { success = false, error = "Vui lòng đăng nhập lại." });

            var q = await _questionBankService.GetQuestionAsync(id);
            if (q == null) return new JsonResult(new { success = false, error = "Câu hỏi không tồn tại." });

            var course = await _courseService.GetCourseAsync(q.CourseId);
            var isAdmin = User.IsInRole("Admin");
            if (!isAdmin && (course == null || course.LecturerId != user.Id))
                return new JsonResult(new { success = false, error = "Bạn không có quyền sửa câu hỏi này." });

            if (string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(optionA) || string.IsNullOrWhiteSpace(optionB) || string.IsNullOrWhiteSpace(optionC) || string.IsNullOrWhiteSpace(optionD) || string.IsNullOrWhiteSpace(correctAnswer))
            {
                return new JsonResult(new { success = false, error = "Vui lòng điền đầy đủ thông tin bắt buộc." });
            }

            q.Content = content.Trim();
            q.OptionA = optionA.Trim();
            q.OptionB = optionB.Trim();
            q.OptionC = optionC.Trim();
            q.OptionD = optionD.Trim();
            q.CorrectAnswer = correctAnswer.Trim()[0];
            q.Explanation = explanation?.Trim();

            await _questionBankService.UpdateQuestionAsync(q);
            return new JsonResult(new { success = true });
        }

        public async Task<IActionResult> OnPostDeleteBankQuestionAsync(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return new JsonResult(new { success = false, error = "Vui lòng đăng nhập lại." });

            var q = await _questionBankService.GetQuestionAsync(id);
            if (q == null) return new JsonResult(new { success = false, error = "Câu hỏi không tồn tại." });

            var course = await _courseService.GetCourseAsync(q.CourseId);
            var isAdmin = User.IsInRole("Admin");
            if (!isAdmin && (course == null || course.LecturerId != user.Id))
                return new JsonResult(new { success = false, error = "Bạn không có quyền xóa câu hỏi này." });

            await _questionBankService.DeleteQuestionAsync(id);
            return new JsonResult(new { success = true });
        }

        public async Task<IActionResult> OnPostGeneratePracticeQuizAsync(int courseId, [FromForm] List<int>? chapterIds, int numQuestions, string title)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return new JsonResult(new { success = false, error = "Vui lòng đăng nhập lại." });

            var course = await _courseService.GetCourseAsync(courseId);
            if (course == null) return new JsonResult(new { success = false, error = "Môn học không tồn tại." });

            // Normalize: null/empty chapterIds = all chapters
            var hasChapterFilter = chapterIds != null && chapterIds.Count > 0;

            var count = await _context.QuestionBankItems.CountAsync(q =>
                q.CourseId == courseId &&
                (!hasChapterFilter || chapterIds!.Contains(q.ChapterId ?? -1)));

            if (count == 0)
            {
                return new JsonResult(new { success = false, error = "Kho câu hỏi của môn/chương học này hiện đang trống. Giảng viên cần tạo câu hỏi trước!" });
            }

            if (numQuestions <= 0)
            {
                return new JsonResult(new { success = false, error = "Số lượng câu hỏi phải lớn hơn 0." });
            }

            var quiz = await _questionBankService.GenerateRandomQuizFromMultipleChaptersAsync(courseId, hasChapterFilter ? chapterIds : null, numQuestions, title);
            return new JsonResult(new { success = true, quizId = quiz.Id });
        }

        public async Task<IActionResult> OnPostDeleteQuizAsync(int quizId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return new JsonResult(new { success = false, error = "Vui lòng đăng nhập lại." });

            var quiz = await _quizService.GetQuizWithQuestionsAsync(quizId);
            if (quiz == null) return new JsonResult(new { success = false, error = "Quiz không tồn tại." });

            var course = await _courseService.GetCourseAsync(quiz.CourseId);
            var isAdmin = User.IsInRole("Admin");
            if (!isAdmin && (course == null || course.LecturerId != user.Id))
            {
                return new JsonResult(new { success = false, error = "Bạn không có quyền xóa bài thi này." });
            }

            await _quizService.DeleteQuizAsync(quizId);
            return new JsonResult(new { success = true });
        }
    }
}
