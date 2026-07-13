using FinalProject_PRN222_Group7.DAL.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace FinalProject_PRN222_Group7.Pages.Auth
{
    public class LoginModel : PageModel
    {
        private readonly SignInManager<AppUser> _signInManager;
        private readonly UserManager<AppUser> _userManager;

        public LoginModel(SignInManager<AppUser> signInManager, UserManager<AppUser> userManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
        }

        [BindProperty] public string Email { get; set; } = "";
        [BindProperty] public string Password { get; set; } = "";
        [BindProperty] public bool RememberMe { get; set; }
        public string? ErrorMessage { get; set; }

        public IActionResult OnGet()
        {
            if (User.Identity?.IsAuthenticated == true) return RedirectToPage("/Dashboard/Index");
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var trimmedEmail = Email.Trim();
            var user = await _userManager.FindByEmailAsync(trimmedEmail);
            if (user == null)
            {
                ErrorMessage = "Email hoặc mật khẩu không đúng. Vui lòng thử lại.";
                return Page();
            }

            if (!user.IsActive)
            {
                ErrorMessage = "Tài khoản của bạn đã bị vô hiệu hóa. Vui lòng liên hệ Admin.";
                return Page();
            }

            var result = await _signInManager.PasswordSignInAsync(user.UserName!, Password, RememberMe, lockoutOnFailure: false);

            if (result.Succeeded)
            {
                return RedirectToPage("/Dashboard/Index");
            }

            ErrorMessage = "Email hoặc mật khẩu không đúng. Vui lòng thử lại.";
            return Page();
        }
    }
}
