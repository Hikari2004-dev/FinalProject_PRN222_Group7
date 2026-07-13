using FinalProject_PRN222_Group7.BLL.Services;
using FinalProject_PRN222_Group7.DAL.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
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
        private readonly IAdminUserService _adminUserService;
        private readonly UserManager<AppUser> _userManager;
        private readonly IEmailService _emailService;

        public IndexModel(
            IReportService reportService, 
            IPaymentService paymentService, 
            IAdminUserService adminUserService,
            UserManager<AppUser> userManager, 
            IEmailService emailService)
        {
            _reportService = reportService;
            _paymentService = paymentService;
            _adminUserService = adminUserService;
            _userManager = userManager;
            _emailService = emailService;
        }

        public DashboardStats Stats { get; set; } = null!;
        public IEnumerable<AppUser> RecentUsers { get; set; } = new List<AppUser>();
        public Dictionary<string, string> UserRoles { get; set; } = new();
        public Dictionary<string, string> UserPackages { get; set; } = new();
        public IEnumerable<Payment> Payments { get; set; } = new List<Payment>();
        public string CurrentUserId { get; set; } = "";

        public async Task OnGetAsync()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            CurrentUserId = currentUser?.Id ?? "";

            Stats = await _reportService.GetDashboardStatsAsync();
            Payments = await _paymentService.GetAllPaymentsAsync();

            RecentUsers = await _adminUserService.GetRecentUsersAsync(20);

            foreach (var u in RecentUsers)
            {
                var roles = await _userManager.GetRolesAsync(u);
                UserRoles[u.Id] = roles.FirstOrDefault() ?? "Student";

                var pkg = await _adminUserService.GetUserPackageAsync(u.Id);
                UserPackages[u.Id] = pkg?.Package?.Name ?? "Basic";
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

            var targetRole = new[] { "Student", "Lecturer", "Admin" }.Contains(role) ? role : "Student";
            var success = await _adminUserService.CreateUserWithPackageAsync(user, password, targetRole, 1);
            if (success)
            {
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
                TempData["Error"] = "Không thể tạo tài khoản. Vui lòng kiểm tra lại địa chỉ email hoặc tài khoản đã tồn tại.";
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

                var targetRole = new[] { "Student", "Lecturer", "Admin" }.Contains(role) ? role : "Student";
                var success = await _adminUserService.CreateUserWithPackageAsync(user, password, targetRole, 1);
                if (success)
                {
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
                    errors.Add($"Lỗi tạo {email}: Tài khoản đã tồn tại hoặc dữ liệu không hợp lệ.");
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

            // Guard: Admin không thể chỉnh sửa chính mình hoặc Admin khác
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.Id == editUserId)
            {
                TempData["Error"] = "Bạn không thể thay đổi quyền của chính mình.";
                return RedirectToPage();
            }
            var targetIsAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            if (targetIsAdmin)
            {
                TempData["Error"] = "Không thể thay đổi quyền của tài khoản Admin khác.";
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
