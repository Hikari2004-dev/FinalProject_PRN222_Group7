using FinalProject_PRN222_Group7.BLL.Services;
using FinalProject_PRN222_Group7.DAL.Entities;
using FinalProject_PRN222_Group7.DAL.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinalProject_PRN222_Group7.Api;

[ApiController]
[Route("api/reports")]
[Authorize]
public class ReportsApiController : ControllerBase
{
    private readonly IReportService _reportService;
    private readonly AppDbContext _context;
    private readonly UserManager<AppUser> _userManager;
    private readonly ILogger<ReportsApiController> _logger;

    public ReportsApiController(IReportService reportService, AppDbContext context, UserManager<AppUser> userManager, ILogger<ReportsApiController> logger)
    {
        _reportService = reportService;
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    public record DashboardStatsDto(int TotalUsers, int TotalDocuments, int TotalSessions, int TotalQuizAttempts, decimal TotalRevenue, int IndexedDocuments, int ActiveUsersToday, int NewUsersThisWeek);

    public record DailyQueryStatDto(DateTime Date, int Count);

    public record CourseQuizStatDto(string CourseName, double AverageScore, int AttemptCount);

    public record DocumentUsageDto(string CourseName, int DocumentCount, long TotalSizeBytes, int IndexedCount, DateTime? LastUploaded);

    public record UserActivityDto(string UserId, string Email, string FullName, string Role, int ChatMessages, int QuizAttempts, int DocumentUploads, DateTime? LastActive);

    public record QuizPerformanceDto(string QuizTitle, string CourseName, int AttemptCount, double AverageScore, int HighestScore, int LowestScore);

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboardStats()
    {
        var stats = await _reportService.GetDashboardStatsAsync();

        var activeToday = await _context.ChatMessages
            .Where(m => m.CreatedAt.Year == DateTime.UtcNow.Year && m.CreatedAt.Month == DateTime.UtcNow.Month && m.CreatedAt.Day == DateTime.UtcNow.Day)
            .Select(m => m.ChatSession.UserId)
            .Distinct()
            .CountAsync();

        var weekStart = DateTime.UtcNow.AddDays(-7).Date;
        var newUsersThisWeek = await _context.Users.CountAsync(u => u.CreatedAt >= weekStart);

        var dto = new DashboardStatsDto(
            stats.TotalUsers,
            stats.TotalDocuments,
            stats.TotalSessions,
            stats.TotalQuizAttempts,
            stats.TotalRevenue,
            stats.IndexedDocuments,
            activeToday,
            newUsersThisWeek
        );
        return Ok(dto);
    }

    [HttpGet("daily-queries")]
    public async Task<IActionResult> GetDailyQueryStats([FromQuery] int days = 30)
    {
        var stats = await _reportService.GetDailyQueryStatsAsync(days);
        var result = stats.Select(s => new DailyQueryStatDto(s.Date, s.Count));
        return Ok(result);
    }

    [HttpGet("course-quiz-stats")]
    public async Task<IActionResult> GetCourseQuizStats()
    {
        var stats = await _reportService.GetCourseQuizStatsAsync();
        var result = stats.Select(s => new CourseQuizStatDto(s.CourseName, s.AverageScore, s.AttemptCount));
        return Ok(result);
    }

    [HttpGet("document-usage")]
    public async Task<IActionResult> GetDocumentUsage()
    {
        var courses = await _context.Courses.ToListAsync();
        var documents = await _context.Documents.ToListAsync();

        var result = new List<DocumentUsageDto>();
        foreach (var course in courses)
        {
            var courseDocs = documents.Where(d => d.CourseId == course.Id).ToList();
            if (!courseDocs.Any()) continue;

            result.Add(new DocumentUsageDto(
                course.Name,
                courseDocs.Count,
                courseDocs.Sum(d => d.FileSizeBytes),
                courseDocs.Count(d => d.Status == DocumentStatus.Indexed),
                courseDocs.Max(d => d.UploadedAt)
            ));
        }

        return Ok(result.OrderByDescending(x => x.DocumentCount));
    }

    [HttpGet("user-activity")]
    [Authorize(Roles = "Admin,Lecturer")]
    public async Task<IActionResult> GetUserActivity()
    {
        var users = await _context.Users.ToListAsync();
        var result = new List<UserActivityDto>();

        foreach (var user in users)
        {
            var chatMessages = await _context.ChatMessages.CountAsync(m => m.ChatSession.UserId == user.Id);
            var quizAttempts = await _context.QuizAttempts.CountAsync(a => a.UserId == user.Id);
            var documentUploads = await _context.Documents.CountAsync(d => d.UploadedById == user.Id);

            var lastMessage = await _context.ChatMessages
                .Where(m => m.ChatSession.UserId == user.Id)
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefaultAsync();

            var roles = await _userManager.GetRolesAsync(user);
            result.Add(new UserActivityDto(
                user.Id,
                user.Email ?? string.Empty,
                user.FullName,
                string.Join(", ", roles),
                chatMessages,
                quizAttempts,
                documentUploads,
                lastMessage?.CreatedAt
            ));
        }

        return Ok(result.OrderByDescending(x => x.LastActive));
    }

    [HttpGet("quiz-performance")]
    [Authorize(Roles = "Admin,Lecturer")]
    public async Task<IActionResult> GetQuizPerformance()
    {
        var quizzes = await _context.Quizzes
            .Include(q => q.Course)
            .Include(q => q.Attempts)
            .Where(q => q.Attempts.Any(a => a.IsCompleted))
            .ToListAsync();

        var result = new List<QuizPerformanceDto>();
        foreach (var q in quizzes)
        {
            var completed = q.Attempts.Where(a => a.IsCompleted).ToList();
            if (!completed.Any()) continue;

            result.Add(new QuizPerformanceDto(
                q.Title,
                q.Course?.Name ?? "Unknown",
                completed.Count,
                completed.Average(a => (double)a.Score),
                completed.Max(a => a.Score),
                completed.Min(a => a.Score)
            ));
        }

        return Ok(result.OrderByDescending(x => x.AttemptCount));
    }

    [HttpGet("revenue-summary")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetRevenueSummary()
    {
        var totalRevenue = await _context.Payments
            .Where(p => p.Status == PaymentStatus.Completed)
            .SumAsync(p => p.Amount);

        var thisMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTime.UtcNow.Kind);
        var monthlyRevenue = await _context.Payments
            .Where(p => p.Status == PaymentStatus.Completed && p.PaidAt >= thisMonth)
            .SumAsync(p => p.Amount);

        var pendingRevenue = await _context.Payments
            .Where(p => p.Status == PaymentStatus.Pending)
            .SumAsync(p => p.Amount);

        var totalInvoices = await _context.Payments.CountAsync();

        return Ok(new
        {
            totalRevenue,
            monthlyRevenue,
            pendingRevenue,
            totalInvoices,
            averageTransaction = totalInvoices > 0 ? totalRevenue / totalInvoices : 0
        });
    }
}
