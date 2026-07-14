using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FinalProject_PRN222_Group7.Pages.Auth
{
    public class AccessDeniedModel : PageModel
    {
        public IActionResult OnGet()
        {
            return Page();
        }
    }
}
