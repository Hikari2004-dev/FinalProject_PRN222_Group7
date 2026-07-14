using FinalProject_PRN222_Group7.BLL.Services;
using FinalProject_PRN222_Group7.DAL.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

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
        public string? CurrentUserId { get; set; }
        public bool CanBuy { get; set; }

        public async Task OnGetAsync()
        {
            Packages = (await _paymentService.GetPackagesAsync()).ToList();

            if (User.Identity?.IsAuthenticated == true)
            {
                var user = await _userManager.GetUserAsync(User);
                CurrentUserId = user?.Id;

                var isStudent = User.IsInRole("Student");
                var isLecturer = User.IsInRole("Lecturer");
                CanBuy = isStudent || isLecturer;
            }
            else
            {
                CanBuy = false;
            }
        }

        public async Task<IActionResult> OnPostBuyAsync(int packageId)
        {
            if (!User.Identity?.IsAuthenticated ?? true)
                return RedirectToPage("/Auth/Login");

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Auth/Login");

            var isStudent = User.IsInRole("Student");
            var isLecturer = User.IsInRole("Lecturer");
            if (!isStudent && !isLecturer)
                return Forbid();

            var packages = await _paymentService.GetPackagesAsync();
            var selectedPackage = packages.FirstOrDefault(p => p.Id == packageId);
            if (selectedPackage == null)
                return NotFound();

            if (selectedPackage.Price <= 0)
            {
                TempData["Success"] = $"Bạn đã kích hoạt gói '{selectedPackage.Name}'.";
                return RedirectToPage();
            }

            var payment = await _paymentService.CreatePaymentAsync(user.Id, packageId);
            TempData["Success"] = $"Đã tạo yêu cầu mua gói '{selectedPackage.Name}'. Mã hóa đơn: {payment.InvoiceNumber}";
            return RedirectToPage();
        }
    }
}
