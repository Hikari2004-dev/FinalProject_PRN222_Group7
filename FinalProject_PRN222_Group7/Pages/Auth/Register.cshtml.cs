using FinalProject_PRN222_Group7.DAL.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FinalProject_PRN222_Group7.Pages.Auth
{
    public class RegisterModel : PageModel
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;

        public RegisterModel(UserManager<AppUser> userManager, SignInManager<AppUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [BindProperty] public string FullName { get; set; } = "";
        [BindProperty] public string Email { get; set; } = "";
        [BindProperty] public string Password { get; set; } = "";
        [BindProperty] public string ConfirmPassword { get; set; } = "";
        [BindProperty] public string Role { get; set; } = "Student";

        public List<string> ErrorMessages { get; set; } = new();

        public IActionResult OnGet()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToPage("/Dashboard/Index");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToPage("/Dashboard/Index");
            }

            ErrorMessages.Clear();

            if (string.IsNullOrWhiteSpace(FullName) ||
                string.IsNullOrWhiteSpace(Email) ||
                string.IsNullOrWhiteSpace(Password) ||
                string.IsNullOrWhiteSpace(ConfirmPassword))
            {
                ErrorMessages.Add("Vui lòng điền đầy đủ thông tin.");
                return Page();
            }

            if (string.IsNullOrWhiteSpace(Role))
            {
                Role = "Student";
            }

            if (!new[] { "Student", "Lecturer" }.Contains(Role))
            {
                ErrorMessages.Add("Vai trò không hợp lệ.");
                return Page();
            }

            if (Password.Length < 6)
            {
                ErrorMessages.Add("Mật khẩu cần có ít nhất 6 ký tự.");
                return Page();
            }

            if (Password != ConfirmPassword)
            {
                ErrorMessages.Add("Mật khẩu xác nhận không khớp.");
                return Page();
            }

            var existingUser = await _userManager.FindByEmailAsync(Email.Trim());
            if (existingUser != null)
            {
                ErrorMessages.Add("Email này đã được đăng ký.");
                return Page();
            }

            var user = new AppUser
            {
                UserName = Email.Trim(),
                Email = Email.Trim(),
                FullName = FullName.Trim(),
                EmailConfirmed = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var createResult = await _userManager.CreateAsync(user, Password);
            if (!createResult.Succeeded)
            {
                foreach (var err in createResult.Errors)
                {
                    ErrorMessages.Add(err.Description);
                }
                return Page();
            }

            var roleResult = await _userManager.AddToRoleAsync(user, Role);
            if (!roleResult.Succeeded)
            {
                foreach (var err in roleResult.Errors)
                {
                    ErrorMessages.Add(err.Description);
                }
                return Page();
            }

            await _signInManager.SignInAsync(user, isPersistent: false);
            return RedirectToPage("/Dashboard/Index");
        }
    }
}
