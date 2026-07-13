using FinalProject_PRN222_Group7.BLL.Services;
using FinalProject_PRN222_Group7.DAL.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FinalProject_PRN222_Group7.Pages.Quiz
{
    public class TakeModel : PageModel
    {
        private readonly IQuizService _quizService;
        private readonly UserManager<AppUser> _userManager;

        public TakeModel(IQuizService quizService, UserManager<AppUser> userManager)
        {
            _quizService = quizService;
            _userManager = userManager;
        }

        public DAL.Entities.Quiz? Quiz { get; set; }
        public int AttemptId { get; set; }

        public async Task OnGetAsync(int quizId)
        {
            Quiz = await _quizService.GetQuizWithQuestionsAsync(quizId);
            if (Quiz != null)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user != null)
                {
                    var attempt = await _quizService.StartAttemptAsync(quizId, user.Id);
                    AttemptId = attempt.Id;
                }
            }
        }
    }
}
