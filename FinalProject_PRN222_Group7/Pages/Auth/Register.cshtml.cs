using FinalProject_PRN222_Group7.BLL.DTOs;
using FinalProject_PRN222_Group7.BLL.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FinalProject_PRN222_Group7.Pages.Auth;

public class RegisterModel : PageModel
{
    private readonly IAuthService _authService;

    public RegisterModel(IAuthService authService)
    {
        _authService = authService;
    }

    [BindProperty]
    public RegisterDto Input { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var (success, error) = await _authService.RegisterAsync(Input);
        if (!success)
        {
            ErrorMessage = error;
            return Page();
        }

        return RedirectToPage("/Auth/Login");
    }
}
