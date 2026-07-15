using FinalProject_PRN222_Group7.BLL.Services;
using FinalProject_PRN222_Group7.DAL.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace FinalProject_PRN222_Group7.Pages.Quiz
{
    public class IndexModel : PageModel
    {
        private readonly IQuizService _quizService;
        private readonly IDocumentService _docService;
        private readonly ICourseService _courseService;
        private readonly IQuestionBankService _questionBankService;
        private readonly UserManager<AppUser> _userManager;
        private readonly IAiUsageGate _aiUsageGate;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(
            IQuizService quizService,
            IDocumentService docService,
            ICourseService courseService,
            IQuestionBankService questionBankService,
            UserManager<AppUser> userManager,
            IAiUsageGate aiUsageGate,
            ILogger<IndexModel> logger)
        {
            _quizService = quizService;
            _docService = docService;
            _courseService = courseService;
            _questionBankService = questionBankService;
            _userManager = userManager;
            _aiUsageGate = aiUsageGate;
            _logger = logger;
        }

        public IEnumerable<DAL.Entities.Quiz> Quizzes { get; set; } = new List<DAL.Entities.Quiz>();
        public IEnumerable<QuizAttempt> UserAttempts { get; set; } = new List<QuizAttempt>();
        public IEnumerable<Document> IndexedDocuments { get; set; } = new List<Document>();
        public IEnumerable<Course> Courses { get; set; } = new List<Course>();


        public string CurrentUserId { get; set; } = string.Empty;

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            var isLecturer = User.IsInRole("Lecturer");
            var isAdmin = User.IsInRole("Admin");

            // Lecturer chỉ thấy môn mình quản lý; Admin và Student thấy tất cả
            var courses = isLecturer && user != null
                ? await _courseService.GetAllCoursesAsync(lecturerId: user.Id)
                : await _courseService.GetAllCoursesAsync();
            Courses = courses;

            var allQuizzes = new List<DAL.Entities.Quiz>();
            foreach (var course in courses)
            {
                var quizzes = await _quizService.GetByCourseAsync(course.Id);
                allQuizzes.AddRange(quizzes);
            }

            if (isLecturer || isAdmin)
            {
                // Giảng viên & Admin chỉ thấy quiz AI-generated trong phạm vi môn đã lọc
                Quizzes = allQuizzes
                    .Where(q => q.IsAiGenerated)
                    .OrderByDescending(q => q.CreatedAt)
                    .ToList();
            }
            else if (user != null)
            {
                var userQuizIds = await _quizService.GetUserAttemptedQuizIdsAsync(user.Id);
                Quizzes = allQuizzes
                    .Where(q => !q.IsAiGenerated && userQuizIds.Contains(q.Id))
                    .OrderByDescending(q => q.CreatedAt)
                    .ToList();
            }
            else
            {
                Quizzes = new List<DAL.Entities.Quiz>();
            }

            if (user != null)
            {
                CurrentUserId = user.Id;
                UserAttempts = await _quizService.GetUserCompletedAttemptsAsync(user.Id);

                // Tài liệu để tạo quiz: Giảng viên chỉ thấy tài liệu thuộc môn mình quản lý
                IndexedDocuments = await _docService.GetIndexedDocumentsAsync(isLecturer ? user.Id : null);
            }
        }

        public async Task<IActionResult> OnPostGenerateQuizAsync(int documentId, int numQuestions, string title)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return new JsonResult(new { success = false, error = "Vui lòng đăng nhập lại." });

            var roles = await _userManager.GetRolesAsync(user);
            var isLecturer = roles.Contains("Lecturer") || roles.Contains("Admin");
            if (!isLecturer)
            {
                return new JsonResult(new { success = false, error = "Bạn không có quyền thực hiện chức năng này." });
            }

            var doc = await _docService.GetDocumentAsync(documentId);

            if (doc == null || doc.Course.LecturerId != user.Id)
                return new JsonResult(new { success = false, error = "Tài liệu không tồn tại hoặc bạn không có quyền sở hữu môn học này." });

            var usageResult = await _aiUsageGate.ExecuteAsync(
                user.Id,
                roles,
                "quiz.generate",
                async () =>
                {
                    var existingContents = (await _questionBankService.GetQuestionsByCourseAsync(doc.CourseId))
                        .Select(q => q.Content).Distinct();

                    var generated = await _quizService.GenerateQuestionsFromDocumentAsync(documentId, doc.CourseId, numQuestions, existingContents);
                    var questions = generated.Questions;

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
                    return new AiUsageExecutionPayload<bool>(true, generated.TokensUsed, generated.ModelName);
                },
                $"quiz:{documentId}:{numQuestions}:{Guid.NewGuid():N}");

            if (!usageResult.Success)
            {
                _logger.LogWarning("AI quiz generation failed for document {DocumentId}: {Error}", documentId, usageResult.ErrorMessage);
                return new JsonResult(new { success = false, error = usageResult.ErrorMessage ?? "Không thể tạo quiz bằng AI." });
            }

            return new JsonResult(new { success = true });
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

            var bankQuestions = await _questionBankService.GetQuestionsByCourseAsync(courseId);
            var count = bankQuestions.Count(q => !hasChapterFilter || chapterIds!.Contains(q.ChapterId ?? -1));

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
