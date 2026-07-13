using FinalProject_PRN222_Group7.DAL.Data;
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
        private readonly AppDbContext _context;

        public RegisterModel(UserManager<AppUser> userManager, SignInManager<AppUser> signInManager, AppDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
        }

        [BindProperty] public string FullName { get; set; } = "";
        [BindProperty] public string Email { get; set; } = "";
        [BindProperty] public string Password { get; set; } = "";
        [BindProperty] public string ConfirmPassword { get; set; } = "";
        [BindProperty] public string Role { get; set; } = "Student";

        public List<string> ErrorMessages { get; set; } = new();

        public IActionResult OnGet()
        {
            return RedirectToPage("/Auth/Login");
        }

        public IActionResult OnPost()
        {
            return RedirectToPage("/Auth/Login");
        }
    }
}
