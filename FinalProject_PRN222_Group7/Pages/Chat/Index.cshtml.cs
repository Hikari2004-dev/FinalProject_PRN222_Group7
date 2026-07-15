using FinalProject_PRN222_Group7.BLL.Services;
using FinalProject_PRN222_Group7.DAL.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FinalProject_PRN222_Group7.Pages.Chat
{
    public class IndexModel : PageModel
    {
        private readonly IChatService _chatService;
        private readonly ICourseService _courseService;
        private readonly ICreditWalletService _walletService;
        private readonly UserManager<AppUser> _userManager;

        public IndexModel(
            IChatService chatService,
            ICourseService courseService,
            ICreditWalletService walletService,
            UserManager<AppUser> userManager)
        {
            _chatService = chatService;
            _courseService = courseService;
            _walletService = walletService;
            _userManager = userManager;
        }

        public IEnumerable<ChatSession> Sessions { get; set; } = new List<ChatSession>();
        public ChatSession? ActiveSession { get; set; }
        public int? ActiveSessionId { get; set; }
        public IEnumerable<Course> Courses { get; set; } = new List<Course>();
        public string UserName { get; set; } = string.Empty;
        public string UserInitials { get; set; } = "U";
        public string UserRole { get; set; } = "Student";
        public int RemainingQueries { get; set; }

        public async Task<IActionResult> OnGetAsync(int? sessionId = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToPage("/Auth/Login");
            }

            var roles = await _userManager.GetRolesAsync(user);
            UserName = user.FullName;
            UserRole = roles.FirstOrDefault() ?? "Student";
            UserInitials = string.Join("", (user.FullName ?? "U").Split(' ').Select(n => n[0]).Take(2)).ToUpper();

            // Danh sách phiên chat ở sidebar luôn chỉ hiển thị các phiên chat của chính người dùng đăng nhập
            Sessions = await _chatService.GetUserSessionsAsync(user.Id);

            Courses = await _courseService.GetAllCoursesAsync();

            if (sessionId.HasValue)
            {
                var session = await _chatService.GetSessionAsync(sessionId.Value);
                if (session != null)
                {
                    var isOwner = session.UserId == user.Id;
                    var isAdmin = User.IsInRole("Admin");

                    if (isOwner || isAdmin)
                    {
                        ActiveSession = session;
                        ActiveSessionId = sessionId;
                    }
                    else
                    {
                        return RedirectToPage("/Chat/Index");
                    }
                }
                else
                {
                    return RedirectToPage("/Chat/Index");
                }
            }

            RemainingQueries = await _walletService.GetAvailableCreditsAsync(user.Id, roles);
            return Page();
        }

        public async Task<IActionResult> OnPostDeleteSessionAsync(int sessionId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return new JsonResult(new { success = false, error = "Vui lòng đăng nhập lại." });
            }

            var session = await _chatService.GetSessionAsync(sessionId);
            if (session == null)
            {
                return new JsonResult(new { success = false, error = "Phiên chat không tồn tại." });
            }

            if (session.UserId != user.Id)
            {
                return new JsonResult(new { success = false, error = "Bạn không có quyền xóa phiên chat này." });
            }

            await _chatService.DeleteSessionAsync(sessionId);
            return new JsonResult(new { success = true });
        }
    }
}
