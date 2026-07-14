using FinalProject_PRN222_Group7.BLL.Services;
using FinalProject_PRN222_Group7.DAL.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FinalProject_PRN222_Group7.Pages.Payments
{
    public class IndexModel : PageModel
    {
        private readonly IPaymentService _paymentService;
        private readonly UserManager<AppUser> _userManager;

        public IndexModel(IPaymentService paymentService, UserManager<AppUser> userManager)
        {
            _paymentService = paymentService;
            _userManager = userManager;
        }

        public IList<Payment> Payments { get; set; } = new List<Payment>();
        public bool IsAdmin { get; set; }

        public async Task OnGetAsync()
        {
            IsAdmin = User.IsInRole("Admin");

            if (IsAdmin)
            {
                Payments = (await _paymentService.GetAllPaymentsAsync()).ToList();
            }
            else if (User.Identity?.IsAuthenticated == true)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user != null)
                {
                    Payments = (await _paymentService.GetUserPaymentsAsync(user.Id)).ToList();
                }
            }
        }
    }
}
