using FinalProject_PRN222_Group7.BLL.Services;
using FinalProject_PRN222_Group7.DAL.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FinalProject_PRN222_Group7.Pages.Reports
{
    public class IndexModel : PageModel
    {
        private readonly IReportService _reportService;
        private readonly UserManager<AppUser> _userManager;

        public IndexModel(IReportService reportService, UserManager<AppUser> userManager)
        {
            _reportService = reportService;
            _userManager = userManager;
        }

        public DashboardStats Stats { get; set; } = null!;
        public IEnumerable<DailyQueryStat> DailyStats { get; set; } = new List<DailyQueryStat>();
        public IEnumerable<CourseQuizStat> CourseStats { get; set; } = new List<CourseQuizStat>();
        public int[] ScoreDistribution { get; set; } = new int[4];
        public List<PackageRevenue> RevenueByPackage { get; set; } = new();

        public record PackageRevenue(string PackageName, int Count, decimal Total);

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            var isLecturer = User.IsInRole("Lecturer");
            var isAdmin = User.IsInRole("Admin");
            
            string? reportUserId = (isLecturer || isAdmin) ? null : user?.Id;
            string? reportLecturerId = isLecturer ? user?.Id : null;

            Stats = await _reportService.GetDashboardStatsAsync(reportUserId, reportLecturerId);
            DailyStats = await _reportService.GetDailyQueryStatsAsync(30, reportUserId, reportLecturerId);
            CourseStats = await _reportService.GetCourseQuizStatsAsync(reportUserId, reportLecturerId);

            ScoreDistribution = await _reportService.GetQuizScoreDistributionAsync(reportUserId, reportLecturerId);
            var bllRev = await _reportService.GetRevenueByPackageAsync();
            RevenueByPackage = bllRev.Select(r => new PackageRevenue(r.PackageName, r.Count, r.Total)).ToList();
        }
    }
}
