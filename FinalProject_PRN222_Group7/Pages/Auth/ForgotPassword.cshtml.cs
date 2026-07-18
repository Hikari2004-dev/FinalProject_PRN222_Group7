using FinalProject_PRN222_Group7.BLL.Services;
using FinalProject_PRN222_Group7.DAL.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace FinalProject_PRN222_Group7.Pages.Auth
{
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly IEmailService _emailService;

        public ForgotPasswordModel(UserManager<AppUser> userManager, IEmailService emailService)
        {
            _userManager = userManager;
            _emailService = emailService;
        }

        [BindProperty]
        [Required(ErrorMessage = "Vui lòng nhập Email.")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
        public string Email { get; set; } = string.Empty;

        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user = await _userManager.FindByEmailAsync(Email);
            if (user == null)
            {
                // Tránh lộ tài khoản tồn tại hay không, vẫn báo thành công chung
                SuccessMessage = "Nếu tài khoản tồn tại trong hệ thống, hướng dẫn đặt lại mật khẩu đã được gửi về email của bạn.";
                return Page();
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetLink = $"{Request.Scheme}://{Request.Host}/Auth/ResetPassword?email={System.Web.HttpUtility.UrlEncode(Email)}&token={System.Web.HttpUtility.UrlEncode(token)}";

            var subject = "Đặt lại mật khẩu - LMS AI Platform";
            var body = $@"
                <div style='font-family: sans-serif; max-width: 500px; margin: 0 auto; padding: 20px; border: 1px solid #eaeaea; border-radius: 8px;'>
                    <h2 style='color: #2563eb;'>Yêu cầu đặt lại mật khẩu</h2>
                    <p>Chào {user.FullName},</p>
                    <p>Bạn nhận được email này vì hệ thống nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn trên <strong>LMS AI Platform</strong>.</p>
                    <p style='margin: 24px 0;'>
                        <a href='{resetLink}' style='background-color: #2563eb; color: white; padding: 12px 24px; text-decoration: none; border-radius: 6px; font-weight: bold; display: inline-block;'>Đặt lại mật khẩu</a>
                    </p>
                    <p>Nếu nút trên không hoạt động, vui lòng copy liên kết dưới đây vào trình duyệt:</p>
                    <p style='word-break: break-all; color: #6b7280; font-size: 0.875rem;'>{resetLink}</p>
                    <hr style='border: none; border-top: 1px solid #eaeaea; margin: 24px 0;'>
                    <p style='font-size: 0.75rem; color: #9ca3af;'>Nếu bạn không yêu cầu đặt lại mật khẩu, bạn có thể bỏ qua email này.</p>
                </div>";

            try
            {
                await _emailService.SendEmailAsync(Email, subject, body);
                SuccessMessage = "Nếu tài khoản tồn tại trong hệ thống, hướng dẫn đặt lại mật khẩu đã được gửi về email của bạn.";
            }
            catch (System.Exception ex)
            {
                ErrorMessage = $"Đã xảy ra lỗi khi gửi email: {ex.Message}";
            }

            return Page();
        }
    }
}
