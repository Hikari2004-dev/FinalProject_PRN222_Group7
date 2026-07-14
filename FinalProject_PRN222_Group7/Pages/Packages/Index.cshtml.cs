using FinalProject_PRN222_Group7.BLL.Services;
using FinalProject_PRN222_Group7.DAL.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Threading.Tasks;

namespace FinalProject_PRN222_Group7.Pages.Packages
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

        public IList<Package> Packages { get; set; } = new List<Package>();
        public UserPackage? UserCurrentPackage { get; set; }
        public int RemainingQueries { get; set; }
        public bool IsStudent { get; set; }

        public async Task OnGetAsync()
        {
            Packages = (await _paymentService.GetPackagesAsync()).ToList();
            IsStudent = User.IsInRole("Student");

            if (User.Identity?.IsAuthenticated == true && IsStudent)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user != null)
                {
                    UserCurrentPackage = await _paymentService.GetUserPackageAsync(user.Id);
                    if (UserCurrentPackage != null)
                    {
                        RemainingQueries = UserCurrentPackage.RemainingQueries;
                    }
                }
            }
        }

        public async Task<IActionResult> OnPostSubscribeAsync(int packageId)
        {
            if (!User.Identity?.IsAuthenticated ?? true)
            {
                return RedirectToPage("/Auth/Login");
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Auth/Login");

            var payment = await _paymentService.CreatePaymentAsync(user.Id, packageId);
            TempData["Success"] = "Đã tạo yêu cầu đăng ký gói. Vui lòng liên hệ quản trị viên để hoàn tất thanh toán.";
            return RedirectToPage("/Payments");
        }
    }
}
