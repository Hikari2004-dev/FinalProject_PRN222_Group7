using FinalProject_PRN222_Group7.BLL.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FinalProject_PRN222_Group7.Pages.Reports
{
    public class IndexModel : PageModel
    {
        private readonly IReportService _reportService;

        public IndexModel(IReportService reportService)
        {
            _reportService = reportService;
        }

        public DashboardStats Stats { get; set; } = null!;
        public IEnumerable<DailyQueryStat> DailyStats { get; set; } = new List<DailyQueryStat>();
        public IEnumerable<CourseQuizStat> CourseStats { get; set; } = new List<CourseQuizStat>();
        public int[] ScoreDistribution { get; set; } = new int[4];
        public List<PackageRevenueDto> RevenueByPackage { get; set; } = new();

        public async Task OnGetAsync()
        {
            Stats = await _reportService.GetDashboardStatsAsync();
            DailyStats = await _reportService.GetDailyQueryStatsAsync(30);
            CourseStats = await _reportService.GetCourseQuizStatsAsync();

            // Score distribution
            var attempts = (await _reportService.GetCompletedQuizAttemptsAsync()).ToList();
            ScoreDistribution = new[]
            {
                attempts.Count(a => a.Score >= 90),
                attempts.Count(a => a.Score >= 70 && a.Score < 90),
                attempts.Count(a => a.Score >= 50 && a.Score < 70),
                attempts.Count(a => a.Score < 50)
            };

            // Revenue by package
            RevenueByPackage = (await _reportService.GetPackageRevenuesAsync()).ToList();
        }
    }
}
