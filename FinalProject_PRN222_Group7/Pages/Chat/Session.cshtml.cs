using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FinalProject_PRN222_Group7.Pages.Chat
{
    public class SessionModel : PageModel
    {
        public int SessionId { get; set; }
        public void OnGet(int sessionId) { SessionId = sessionId; }
    }
}
