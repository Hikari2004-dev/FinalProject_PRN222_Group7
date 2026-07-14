using FinalProject_PRN222_Group7.BLL.Services;
using FinalProject_PRN222_Group7.DAL.Data;
using FinalProject_PRN222_Group7.DAL.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FinalProject_PRN222_Group7.Pages.Dashboard
{
    public class IndexModel : PageModel
    {
        private readonly IReportService _reportService;
        private readonly IDocumentService _docService;
        private readonly IChatService _chatService;
        private readonly ICreditWalletService _walletService;
        private readonly UserManager<AppUser> _userManager;
        private readonly AppDbContext _context;

        public IndexModel(
            IReportService reportService,
            IDocumentService docService,
            IChatService chatService,
            ICreditWalletService walletService,
            UserManager<AppUser> userManager,
            AppDbContext context)
        {
            _reportService = reportService;
            _docService = docService;
            _chatService = chatService;
            _walletService = walletService;
            _userManager = userManager;
            _context = context;
        }

        public string UserName { get; set; } = string.Empty;
        public DashboardStats Stats { get; set; } = null!;
        public int MyDocumentCount { get; set; }
        public int MyChatSessions { get; set; }
        public int MyQuizAttempts { get; set; }
        public int MyAvgScore { get; set; }
        public int RemainingQueries { get; set; }
        public List<Document> RecentDocuments { get; set; } = new();
        public List<ChatSession> RecentSessions { get; set; } = new();
        public List<string> ChartLabels { get; set; } = new();
        public List<int> ChartData { get; set; } = new();

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            UserName = user?.FullName ?? user?.UserName ?? "Bạn";

            Stats = await _reportService.GetDashboardStatsAsync();
            var dailyStats = (await _reportService.GetDailyQueryStatsAsync(30)).ToList();
            ChartLabels = dailyStats.Select(s => s.Date.ToString("dd/MM")).ToList();
            ChartData = dailyStats.Select(s => s.Count).ToList();

            if (user == null)
            {
                return;
            }

            var allDocs = await _docService.GetAllDocumentsAsync();
            if (User.IsInRole("Lecturer"))
            {
                var myDocs = allDocs.Where(d => d.UploadedById == user.Id).ToList();
                MyDocumentCount = myDocs.Count;
                RecentDocuments = myDocs.Take(5).ToList();
            }
            else
            {
                RecentDocuments = allDocs.Take(5).ToList();
            }

            var sessions = await _chatService.GetUserSessionsAsync(user.Id);
            RecentSessions = sessions.Take(5).ToList();
            MyChatSessions = sessions.Count();

            var attempts = await _context.QuizAttempts
                .Where(a => a.UserId == user.Id && a.IsCompleted)
                .ToListAsync();
            MyQuizAttempts = attempts.Count;
            MyAvgScore = attempts.Any() ? (int)attempts.Average(a => a.Score) : 0;

            var roles = await _userManager.GetRolesAsync(user);
            RemainingQueries = await _walletService.GetAvailableCreditsAsync(user.Id, roles);
        }
    }
}
