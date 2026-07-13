using FinalProject_PRN222_Group7.BLL.Services;
using FinalProject_PRN222_Group7.DAL.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FinalProject_PRN222_Group7.Pages.Reports
{
    public class IndexModel : PageModel
    {
        private readonly IReportService _reportService;
        private readonly AppDbContext _context;

        public IndexModel(IReportService reportService, AppDbContext context)
        {
            _reportService = reportService;
            _context = context;
        }

        public DashboardStats Stats { get; set; } = null!;
        public IEnumerable<DailyQueryStat> DailyStats { get; set; } = new List<DailyQueryStat>();
        public IEnumerable<CourseQuizStat> CourseStats { get; set; } = new List<CourseQuizStat>();
        public int[] ScoreDistribution { get; set; } = new int[4];
        public List<PackageRevenue> RevenueByPackage { get; set; } = new();

        public record PackageRevenue(string PackageName, int Count, decimal Total);

        public async Task OnGetAsync()
        {
            Stats = await _reportService.GetDashboardStatsAsync();
            DailyStats = await _reportService.GetDailyQueryStatsAsync(30);
            CourseStats = await _reportService.GetCourseQuizStatsAsync();

            // Score distribution
            var attempts = await _context.QuizAttempts.Where(a => a.IsCompleted).ToListAsync();
            ScoreDistribution = new[]
            {
                attempts.Count(a => a.Score >= 90),
                attempts.Count(a => a.Score >= 70 && a.Score < 90),
                attempts.Count(a => a.Score >= 50 && a.Score < 70),
                attempts.Count(a => a.Score < 50)
            };

            // Revenue by package
            RevenueByPackage = await _context.Payments
                .Where(p => p.Status == DAL.Entities.PaymentStatus.Completed)
                .Include(p => p.Package)
                .GroupBy(p => p.Package.Name)
                .Select(g => new PackageRevenue(g.Key, g.Count(), g.Sum(p => p.Amount)))
                .ToListAsync();
        }
    }
}
