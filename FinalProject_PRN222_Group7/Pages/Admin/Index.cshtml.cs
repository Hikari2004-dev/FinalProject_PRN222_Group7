using FinalProject_PRN222_Group7.BLL.Services;
using FinalProject_PRN222_Group7.DAL.Data;
using FinalProject_PRN222_Group7.DAL.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FinalProject_PRN222_Group7.Pages.Admin
{
    public class IndexModel : PageModel
    {
        private readonly IReportService _reportService;
        private readonly IPaymentService _paymentService;
        private readonly ISubscriptionService _subscriptionService;
        private readonly ICreditWalletService _walletService;
        private readonly UserManager<AppUser> _userManager;
        private readonly AppDbContext _context;
        private readonly IEmailService _emailService;

        public IndexModel(
            IReportService reportService,
            IPaymentService paymentService,
            ISubscriptionService subscriptionService,
            ICreditWalletService walletService,
            UserManager<AppUser> userManager,
            AppDbContext context,
            IEmailService emailService)
        {
            _reportService = reportService;
            _paymentService = paymentService;
            _subscriptionService = subscriptionService;
            _walletService = walletService;
            _userManager = userManager;
            _context = context;
            _emailService = emailService;
        }

        public DashboardStats Stats { get; set; } = null!;
        public IEnumerable<AppUser> RecentUsers { get; set; } = new List<AppUser>();
        public Dictionary<string, string> UserRoles { get; set; } = new();
        public Dictionary<string, string> UserPackages { get; set; } = new();
        public IEnumerable<Payment> Payments { get; set; } = new List<Payment>();

        public async Task OnGetAsync()
        {
            Stats = await _reportService.GetDashboardStatsAsync();
            Payments = await _paymentService.GetAllPaymentsAsync();

            RecentUsers = await _context.Users
                .OrderByDescending(u => u.CreatedAt)
                .Take(20)
                .ToListAsync();

            foreach (var u in RecentUsers)
            {
                var roles = await _userManager.GetRolesAsync(u);
                UserRoles[u.Id] = roles.FirstOrDefault() ?? "Student";

                var pkg = await _paymentService.GetUserPackageAsync(u.Id);
                UserPackages[u.Id] = pkg?.Package?.Name ?? "Free";
            }
        }

        public async Task<IActionResult> OnPostCreateSingleAsync(string fullName, string email, string role)
        {
            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email))
            {
                TempData["Error"] = "Vui lòng điền đầy đủ thông tin.";
                return RedirectToPage();
            }

            var password = GenerateRandomPassword();
            var user = new AppUser
            {
                FullName = fullName,
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                var targetRole = new[] { "Student", "Lecturer", "Admin" }.Contains(role) ? role : "Student";
                await _userManager.AddToRoleAsync(user, targetRole);

                if (targetRole == "Student")
                {
                    await _walletService.EnsureWalletAsync(user.Id, ["Student"]);
                    await _subscriptionService.ActivatePackageAsync(user.Id, 10);
                }
                else
                {
                    await _walletService.EnsureWalletAsync(user.Id, [targetRole]);
                }

                // Send email notification via SMTP
                try
                {
                    await SendAccountEmailAsync(fullName, email, password);
                    TempData["Success"] = $"Tạo thành công tài khoản cho {fullName} ({email}) và đã gửi mail thông báo mật khẩu ứng dụng.";
                }
                catch (Exception ex)
                {
                    TempData["Success"] = $"Tạo thành công tài khoản cho {fullName} ({email}) nhưng gửi mail thất bại. Lỗi: {ex.Message}";
                }
            }
            else
            {
                TempData["Error"] = $"Không thể tạo tài khoản: {string.Join(", ", result.Errors.Select(e => e.Description))}";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostCreateBulkAsync(string bulkData)
        {
            if (string.IsNullOrWhiteSpace(bulkData))
            {
                TempData["Error"] = "Vui lòng nhập danh sách tài khoản.";
                return RedirectToPage();
            }

            var lines = bulkData.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            int successCount = 0;
            var errors = new List<string>();

            foreach (var line in lines)
            {
                var parts = line.Split(',', StringSplitOptions.TrimEntries);
                if (parts.Length < 2)
                {
                    errors.Add($"Dòng không hợp lệ (thiếu dấu phẩy): '{line}'");
                    continue;
                }

                var fullName = parts[0];
                var email = parts[1];
                var role = parts.Length >= 3 ? parts[2] : "Student";

                if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email))
                {
                    errors.Add($"Dòng bị thiếu thông tin: '{line}'");
                    continue;
                }

                var password = GenerateRandomPassword();
                var user = new AppUser
                {
                    FullName = fullName,
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true
                };

                var result = await _userManager.CreateAsync(user, password);
                if (result.Succeeded)
                {
                    var targetRole = new[] { "Student", "Lecturer", "Admin" }.Contains(role) ? role : "Student";
                    await _userManager.AddToRoleAsync(user, targetRole);

                    if (targetRole == "Student")
                    {
                        await _walletService.EnsureWalletAsync(user.Id, ["Student"]);
                        await _subscriptionService.ActivatePackageAsync(user.Id, 10);
                    }
                    else
                    {
                        await _walletService.EnsureWalletAsync(user.Id, [targetRole]);
                    }

                    // Gửi mail cho từng user
                    try
                    {
                        await SendAccountEmailAsync(fullName, email, password);
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Tạo thành công {email} nhưng gửi mail lỗi: {ex.Message}");
                    }

                    successCount++;
                }
                else
                {
                    errors.Add($"Lỗi tạo {email}: {string.Join("; ", result.Errors.Select(e => e.Description))}");
                }
            }

            if (successCount > 0)
            {
                TempData["Success"] = $"Đã tạo thành công {successCount} tài khoản và gửi mail thông báo.";
            }

            if (errors.Any())
            {
                TempData["Error"] = $"Có lỗi xảy ra:\n" + string.Join("\n", errors);
            }

            return RedirectToPage();
        }

        private string GenerateRandomPassword()
        {
            // Trả về mật khẩu ngẫu nhiên thỏa mãn quy chuẩn mật khẩu (ít nhất 1 số, 1 chữ hoa, 1 chữ thường, 1 ký tự đặc biệt, >=6 ký tự)
            return "P@ss" + Guid.NewGuid().ToString("N")[..8];
        }

        private async Task SendAccountEmailAsync(string fullName, string email, string password)
        {
            var subject = "Thông tin tài khoản hệ thống LMS AI của bạn";
            var loginUrl = $"{Request.Scheme}://{Request.Host}/Auth/Login";
            var body = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e2e8f0; border-radius: 8px;'>
                    <h2 style='color: #3b82f6;'>LMS AI Platform</h2>
                    <p>Xin chào <strong>{fullName}</strong>,</p>
                    <p>Tài khoản học tập và giảng dạy của bạn trên hệ thống LMS AI đã được khởi tạo bởi Quản trị viên.</p>
                    <div style='background-color: #f8fafc; padding: 15px; border-radius: 6px; margin: 20px 0; border-left: 4px solid #3b82f6;'>
                        <p style='margin: 4px 0;'><strong>Tài khoản đăng nhập:</strong> <span style='font-family: monospace;'>{email}</span></p>
                        <p style='margin: 4px 0;'><strong>Mật khẩu đăng nhập:</strong> <span style='font-family: monospace;'>{password}</span></p>
                    </div>
                    <p>Vui lòng nhấp vào liên kết bên dưới để đăng nhập ngay:</p>
                    <p style='text-align: center;'>
                        <a href='{loginUrl}' style='display: inline-block; padding: 10px 20px; background-color: #3b82f6; color: #fff; text-decoration: none; border-radius: 6px; font-weight: bold;'>Đăng Nhập Ngay</a>
                    </p>
                    <p style='color: #64748b; font-size: 0.85em; margin-top: 30px;'>
                        * Lưu ý bảo mật: Hãy thay đổi mật khẩu ngay sau lần đăng nhập đầu tiên để đảm bảo an toàn cho tài khoản của bạn.
                    </p>
                </div>";

            await _emailService.SendEmailAsync(email, subject, body);
        }

        public async Task<IActionResult> OnPostEditUserAsync(string editUserId, string role, bool isActive)
        {
            var user = await _userManager.FindByIdAsync(editUserId);
            if (user == null)
            {
                TempData["Error"] = "Người dùng không tồn tại.";
                return RedirectToPage();
            }

            user.IsActive = isActive;
            var updateResult = await _userManager.UpdateAsync(user);

            if (!updateResult.Succeeded)
            {
                TempData["Error"] = "Cập nhật trạng thái người dùng thất bại: " + string.Join(", ", updateResult.Errors.Select(e => e.Description));
                return RedirectToPage();
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removeResult.Succeeded)
            {
                TempData["Error"] = "Xoá vai trò cũ thất bại.";
                return RedirectToPage();
            }

            var addResult = await _userManager.AddToRoleAsync(user, role);
            if (!addResult.Succeeded)
            {
                TempData["Error"] = "Gán vai trò mới thất bại.";
                return RedirectToPage();
            }

            TempData["Success"] = $"Đã cập nhật trạng thái hoạt động và gán vai trò {role} cho người dùng {user.FullName} thành công.";
            return RedirectToPage();
        }
    }
}
