using FinalProject_PRN222_Group7.BLL.Services;
using FinalProject_PRN222_Group7.DAL.Entities;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FinalProject_PRN222_Group7.Pages.Quiz
{
    public class ResultModel : PageModel
    {
        private readonly IQuizService _quizService;
        public ResultModel(IQuizService quizService) => _quizService = quizService;

        public QuizAttempt? Attempt { get; set; }

        public async Task OnGetAsync(int attemptId)
        {
            Attempt = await _quizService.GetAttemptAsync(attemptId);
        }
    }
}
