using FinalProject_PRN222_Group7.BLL.Services;
using FinalProject_PRN222_Group7.DAL.Data;
using FinalProject_PRN222_Group7.DAL.Entities;
using FinalProject_PRN222_Group7.DAL.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinalProject_PRN222_Group7.Api;

[ApiController]
[Route("api/payments")]
[Authorize]
public class PaymentsApiController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly IPaymentRepository _paymentRepository;
    private readonly UserManager<AppUser> _userManager;
    private readonly AppDbContext _context;
    private readonly ILogger<PaymentsApiController> _logger;

    public PaymentsApiController(
        IPaymentService paymentService,
        IPaymentRepository paymentRepository,
        UserManager<AppUser> userManager,
        AppDbContext context,
        ILogger<PaymentsApiController> logger)
    {
        _paymentService = paymentService;
        _paymentRepository = paymentRepository;
        _userManager = userManager;
        _context = context;
        _logger = logger;
    }

    public record PaymentDto(
        int Id,
        string InvoiceNumber,
        string? TransactionId,
        decimal Amount,
        PaymentStatus Status,
        int PackageId,
        string PackageName,
        string UserEmail,
        string? UserFullName,
        DateTime CreatedAt,
        DateTime? PaidAt,
        string? Notes
    );

    public record PackageDto(int Id, string Name, string Tier, decimal Price, int MonthlyAiQueries, int MaxDocuments, bool HasQuizGeneration, bool HasBenchmark, string? Description);

    public record CreatePaymentRequest(int PackageId);

    public record PaymentStatistics(decimal TotalRevenue, int TotalPayments, int CompletedPayments, int PendingPayments, int FailedPayments, decimal AverageAmount);

    [HttpGet("packages")]
    public async Task<IActionResult> GetPackages()
    {
        var packages = await _paymentService.GetPackagesAsync();
        var result = packages.Select(p => new PackageDto(p.Id, p.Name, p.Tier.ToString(), p.Price, p.MonthlyAiQueries, p.MaxDocuments, p.HasQuizGeneration, p.HasBenchmark, p.Description));
        return Ok(result);
    }

    [HttpGet("user/package")]
    public async Task<IActionResult> GetUserCurrentPackage()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var userPackage = await _paymentService.GetUserPackageAsync(user.Id);
        if (userPackage == null) return NotFound(new { message = "No active package found" });

        var dto = new
        {
            userPackage.Id,
            PackageId = userPackage.PackageId,
            PackageName = userPackage.Package?.Name ?? "Unknown",
            userPackage.StartDate,
            userPackage.EndDate,
            userPackage.RemainingQueries,
            userPackage.IsActive,
            Tier = userPackage.Package?.Tier.ToString() ?? "Basic"
        };
        return Ok(dto);
    }

    [HttpPost("create")]
    public async Task<IActionResult> CreatePayment([FromBody] CreatePaymentRequest req)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        try
        {
            var payment = await _paymentService.CreatePaymentAsync(user.Id, req.PackageId);
            var dto = new
            {
                payment.Id,
                payment.InvoiceNumber,
                payment.Amount,
                payment.Status,
                PackageName = payment.Package?.Name ?? "Unknown"
            };
            return Ok(dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{paymentId}/complete")]
    public async Task<IActionResult> CompletePayment(int paymentId)
    {
        try
        {
            var payment = await _paymentService.CompletePaymentAsync(paymentId);
            var dto = new
            {
                payment.Id,
                payment.InvoiceNumber,
                payment.Status,
                payment.TransactionId,
                payment.PaidAt,
                PackageName = payment.Package?.Name ?? "Unknown"
            };
            return Ok(dto);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetUserPaymentHistory()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var payments = await _paymentService.GetUserPaymentsAsync(user.Id);
        var result = payments.Select(p => new PaymentDto(
            p.Id,
            p.InvoiceNumber,
            p.TransactionId,
            p.Amount,
            p.Status,
            p.PackageId,
            p.Package?.Name ?? "Unknown",
            p.User.Email ?? string.Empty,
            p.User.FullName,
            p.CreatedAt,
            p.PaidAt,
            p.Notes
        ));
        return Ok(result);
    }

    [HttpGet("stats")]
    [Authorize(Roles = "Admin,Lecturer")]
    public async Task<IActionResult> GetStatistics()
    {
        var totalRevenue = await _context.Payments
            .Where(p => p.Status == PaymentStatus.Completed)
            .SumAsync(p => p.Amount);

        var totalPayments = await _context.Payments.CountAsync();
        var completed = await _context.Payments.CountAsync(p => p.Status == PaymentStatus.Completed);
        var pending = await _context.Payments.CountAsync(p => p.Status == PaymentStatus.Pending);
        var failed = await _context.Payments.CountAsync(p => p.Status == PaymentStatus.Failed);

        var avgAmount = completed > 0
            ? await _context.Payments.Where(p => p.Status == PaymentStatus.Completed).AverageAsync(p => p.Amount)
            : 0;

        var stats = new PaymentStatistics(totalRevenue, totalPayments, completed, pending, failed, Math.Round(avgAmount, 2));
        return Ok(stats);
    }

    [HttpGet("revenue/monthly")]
    [Authorize(Roles = "Admin,Lecturer")]
    public async Task<IActionResult> GetMonthlyRevenue([FromQuery] int months = 6)
    {
        var from = DateTime.UtcNow.AddMonths(-months);
        var payments = await _context.Payments
            .Where(p => p.Status == PaymentStatus.Completed && p.PaidAt >= from)
            .ToListAsync();

        var monthly = payments
            .GroupBy(p => new { p.PaidAt!.Value.Year, p.PaidAt.Value.Month })
            .Select(g => new
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                Revenue = g.Sum(p => p.Amount),
                Count = g.Count()
            })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToList();

        return Ok(monthly);
    }

    [HttpGet("recent")]
    [Authorize(Roles = "Admin,Lecturer")]
    public async Task<IActionResult> GetRecentPayments([FromQuery] int take = 20)
    {
        var payments = await _paymentRepository.GetAllWithUsersAsync();
        var recent = payments.Take(take);

        var result = recent.Select(p => new PaymentDto(
            p.Id,
            p.InvoiceNumber,
            p.TransactionId,
            p.Amount,
            p.Status,
            p.PackageId,
            p.Package?.Name ?? "Unknown",
            p.User.Email ?? string.Empty,
            p.User.FullName,
            p.CreatedAt,
            p.PaidAt,
            p.Notes
        ));
        return Ok(result);
    }

    [HttpGet("package/{packageId}/users")]
    [Authorize(Roles = "Admin,Lecturer")]
    public async Task<IActionResult> GetPackageUsers(int packageId)
    {
        var users = await _context.UserPackages
            .Include(up => up.User)
            .Include(up => up.Package)
            .Where(up => up.PackageId == packageId)
            .OrderByDescending(up => up.StartDate)
            .ToListAsync();

        var result = users.Select(up => new
        {
            up.Id,
            UserId = up.UserId,
            Email = up.User?.Email ?? string.Empty,
            FullName = up.User?.FullName,
            PackageName = up.Package?.Name ?? "Unknown",
            up.StartDate,
            up.EndDate,
            up.RemainingQueries,
            up.IsActive
        });
        return Ok(result);
    }
}
