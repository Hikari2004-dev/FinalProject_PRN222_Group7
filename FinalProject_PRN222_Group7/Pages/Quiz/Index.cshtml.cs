using FinalProject_PRN222_Group7.BLL.Services;
using FinalProject_PRN222_Group7.DAL.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Linq;
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

        public IndexModel(
            IQuizService quizService,
            IDocumentService docService,
            ICourseService courseService,
            IQuestionBankService questionBankService,
            UserManager<AppUser> userManager)
        {
            _quizService = quizService;
            _docService = docService;
            _courseService = courseService;
            _questionBankService = questionBankService;
            _userManager = userManager;
        }

        public IEnumerable<DAL.Entities.Quiz> Quizzes { get; set; } = new List<DAL.Entities.Quiz>();
        public IEnumerable<Course> Courses { get; set; } = new List<Course>();
        public IEnumerable<Document> IndexedDocuments { get; set; } = new List<Document>();
        public IEnumerable<QuizAttempt> UserAttempts { get; set; } = new List<QuizAttempt>();
        public string CurrentUserId { get; set; } = "";

        public async Task OnGetAsync()
        {
            Courses = await _courseService.GetAllCoursesAsync();

            var user = await _userManager.GetUserAsync(User);
            var allQuizzes = await _quizService.GetByCourseAsync(0); // 0 means get all across courses
            Quizzes = allQuizzes.OrderByDescending(q => q.CreatedAt);

            if (user != null)
            {
                CurrentUserId = user.Id;
                UserAttempts = await _quizService.GetUserAttemptsAsync(user.Id);

                var isLecturer = User.IsInRole("Lecturer");
                if (isLecturer)
                {
                    IndexedDocuments = await _quizService.GetIndexedDocumentsForLecturerAsync(user.Id);
                }
                else
                {
                    IndexedDocuments = await _quizService.GetIndexedDocumentsAsync();
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

            var success = await _quizService.GenerateQuizFromDocumentAsync(documentId, numQuestions, title, user.Id);
            if (!success)
            {
                return new JsonResult(new { success = false, error = "Tài liệu không tồn tại hoặc bạn không có quyền sở hữu môn học này." });
            }

            return new JsonResult(new { success = true });
        }

        public async Task<IActionResult> OnGetBankQuestionsAsync(int courseId, int? chapterId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return new JsonResult(new { success = false, error = "Vui lòng đăng nhập lại." });

            var course = await _courseService.GetCourseAsync(courseId);
            if (course == null) return new JsonResult(new { success = false, error = "Môn học không tồn tại." });

            var isAdmin = User.IsInRole("Admin");
            if (!isAdmin && course.LecturerId != user.Id)
            {
                return new JsonResult(new { success = false, error = "Bạn không có quyền xem ngân hàng câu hỏi của môn này." });
            }

            var questions = await _questionBankService.GetQuestionsByCourseAsync(courseId, chapterId);
            var list = questions.Select(q => new {
                id = q.Id,
                content = q.Content,
                optionA = q.OptionA,
                optionB = q.OptionB,
                optionC = q.OptionC,
                optionD = q.OptionD,
                correctAnswer = q.CorrectAnswer.ToString(),
                explanation = q.Explanation
            });

            return new JsonResult(list);
        }

        public async Task<IActionResult> OnPostEditBankQuestionAsync(
            int questionId, string content, string optionA, string optionB, string optionC, string optionD, string correctAnswer, string explanation)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return new JsonResult(new { success = false, error = "Vui lòng đăng nhập lại." });

            var q = await _questionBankService.GetQuestionAsync(questionId);
            if (q == null) return new JsonResult(new { success = false, error = "Câu hỏi không tồn tại." });

            var course = await _courseService.GetCourseAsync(q.CourseId);
            var isAdmin = User.IsInRole("Admin");
            if (!isAdmin && (course == null || course.LecturerId != user.Id))
            {
                return new JsonResult(new { success = false, error = "Bạn không có quyền sửa câu hỏi này." });
            }

            if (string.IsNullOrEmpty(correctAnswer) || correctAnswer.Length != 1)
            {
                return new JsonResult(new { success = false, error = "Đáp án đúng không hợp lệ." });
            }

            q.Content = content;
            q.OptionA = optionA;
            q.OptionB = optionB;
            q.OptionC = optionC;
            q.OptionD = optionD;
            q.CorrectAnswer = correctAnswer[0];
            q.Explanation = explanation;

            await _questionBankService.UpdateQuestionAsync(q);
            return new JsonResult(new { success = true });
        }

        public async Task<IActionResult> OnPostDeleteBankQuestionAsync(int questionId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return new JsonResult(new { success = false, error = "Vui lòng đăng nhập lại." });

            var q = await _questionBankService.GetQuestionAsync(questionId);
            if (q == null) return new JsonResult(new { success = false, error = "Câu hỏi không tồn tại." });

            var course = await _courseService.GetCourseAsync(q.CourseId);
            var isAdmin = User.IsInRole("Admin");
            if (!isAdmin && (course == null || course.LecturerId != user.Id))
            {
                return new JsonResult(new { success = false, error = "Bạn không có quyền xóa câu hỏi này." });
            }

            await _questionBankService.DeleteQuestionAsync(questionId);
            return new JsonResult(new { success = true });
        }

        public async Task<IActionResult> OnPostGeneratePracticeQuizAsync(int courseId, [FromForm] List<int>? chapterIds, int numQuestions, string title)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return new JsonResult(new { success = false, error = "Vui lòng đăng nhập lại." });

            var course = await _courseService.GetCourseAsync(courseId);
            if (course == null) return new JsonResult(new { success = false, error = "Môn học không tồn tại." });

            var hasChapterFilter = chapterIds != null && chapterIds.Count > 0;

            var bankQuestions = await _questionBankService.GetQuestionsByCourseAsync(courseId);
            if (hasChapterFilter)
            {
                bankQuestions = bankQuestions.Where(q => chapterIds!.Contains(q.ChapterId ?? -1));
            }

            var count = bankQuestions.Count();
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
