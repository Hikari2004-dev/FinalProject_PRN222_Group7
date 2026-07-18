using FinalProject_PRN222_Group7.DAL.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace FinalProject_PRN222_Group7.Pages.Auth
{
    public class ResetPasswordModel : PageModel
    {
        private readonly UserManager<AppUser> _userManager;

        public ResetPasswordModel(UserManager<AppUser> userManager)
        {
            _userManager = userManager;
        }

        [BindProperty]
        public string Email { get; set; } = string.Empty;

        [BindProperty]
        public string Token { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Vui lòng nhập Mật khẩu mới.")]
        [MinLength(6, ErrorMessage = "Mật khẩu phải dài tối thiểu 6 ký tự.")]
        public string NewPassword { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Vui lòng Xác nhận mật khẩu mới.")]
        [Compare("NewPassword", ErrorMessage = "Mật khẩu xác nhận không khớp.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }

        public void OnGet(string email, string token)
        {
            Email = email;
            Token = token;
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
                ErrorMessage = "Yêu cầu đặt lại mật khẩu không hợp lệ hoặc tài khoản không tồn tại.";
                return Page();
            }

            var result = await _userManager.ResetPasswordAsync(user, Token, NewPassword);
            if (result.Succeeded)
            {
                SuccessMessage = "Đặt lại mật khẩu thành công! Bạn đang được chuyển hướng về trang Đăng nhập...";
                return Page();
            }

            foreach (var error in result.Errors)
            {
                ErrorMessage = error.Description;
                break;
            }

            return Page();
        }
    }
}
