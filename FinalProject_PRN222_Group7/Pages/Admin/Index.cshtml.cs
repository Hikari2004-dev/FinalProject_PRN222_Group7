using FinalProject_PRN222_Group7.BLL.Services;
using FinalProject_PRN222_Group7.DAL.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using System.IO;

namespace FinalProject_PRN222_Group7.Pages.Admin
{
    public class IndexModel : PageModel
    {
        private readonly IReportService _reportService;
        private readonly IPaymentService _paymentService;
        private readonly UserManager<AppUser> _userManager;
        private readonly IUserService _userService;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _webHostEnvironment;
 
        public IndexModel(
            IReportService reportService,
            IPaymentService paymentService,
            UserManager<AppUser> userManager,
            IUserService userService,
            IConfiguration configuration,
            IWebHostEnvironment webHostEnvironment)
        {
            _reportService = reportService;
            _paymentService = paymentService;
            _userManager = userManager;
            _userService = userService;
            _configuration = configuration;
            _webHostEnvironment = webHostEnvironment;
        }
 
        public DashboardStats Stats { get; set; } = null!;
        public IEnumerable<AppUser> RecentUsers { get; set; } = new List<AppUser>();
        public Dictionary<string, string> UserRoles { get; set; } = new();
        public Dictionary<string, string> UserPackages { get; set; } = new();
        public Dictionary<string, int> UserCredits { get; set; } = new();
        public IEnumerable<Payment> Payments { get; set; } = new List<Payment>();
        public int ChunkSize { get; set; }
 
        public async Task OnGetAsync()
        {
            Stats = await _reportService.GetDashboardStatsAsync();
            Payments = await _paymentService.GetAllPaymentsAsync();
            ChunkSize = _configuration.GetValue<int>("Gemini:ChunkSize", 500);
 
            RecentUsers = await _reportService.GetRecentUsersAsync(20);

            foreach (var u in RecentUsers)
            {
                var roles = await _userManager.GetRolesAsync(u);
                UserRoles[u.Id] = roles.FirstOrDefault() ?? "Student";

                var pkg = await _paymentService.GetUserPackageAsync(u.Id);
                UserPackages[u.Id] = pkg?.Package?.Name ?? "Free";

                UserCredits[u.Id] = u.CreditWallet != null ? (u.CreditWallet.SubscriptionCreditBalance + u.CreditWallet.PurchasedCreditBalance) : 0;
            }
        }

        public async Task<IActionResult> OnPostCreateSingleAsync(string fullName, string email, string role)
        {
            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email))
            {
                TempData["Error"] = "Vui lòng điền đầy đủ thông tin.";
                return RedirectToPage();
            }

            var loginBaseUrl = $"{Request.Scheme}://{Request.Host}/Auth/Login";
            var result = await _userService.CreateSingleUserAsync(fullName, email, role, loginBaseUrl);

            if (result.Succeeded)
            {
                TempData["Success"] = $"Tạo thành công tài khoản cho {fullName} ({email}) và đã gửi mail thông báo mật khẩu ứng dụng.";
            }
            else
            {
                TempData["Error"] = $"Không thể tạo tài khoản: {string.Join(", ", result.Errors.Select(e => e.Description))}";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostCreateBulkAsync(string bulkData, string bulkRole)
        {
            if (string.IsNullOrWhiteSpace(bulkData))
            {
                TempData["Error"] = "Vui lòng nhập danh sách tài khoản.";
                return RedirectToPage();
            }

            var allowedRoles = new[] { "Student", "Lecturer", "Admin" };
            var targetRole = allowedRoles.Contains(bulkRole) ? bulkRole : "Student";

            var lines = bulkData.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            var rows = new List<UserBulkRow>();
            var errors = new List<string>();
            var lineNo = 1;

            foreach (var line in lines)
            {
                var parts = line.Split(',', StringSplitOptions.TrimEntries);
                if (parts.Length < 2)
                {
                    errors.Add($"[Dòng {lineNo}]: Sai định dạng (yêu cầu HoTen, Email). Nội dung: '{line}'");
                    lineNo++;
                    continue;
                }

                var fullName = parts[0];
                var email = parts[1];

                if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email))
                {
                    errors.Add($"[Dòng {lineNo}]: Họ tên hoặc Email không được để trống.");
                    lineNo++;
                    continue;
                }

                if (!email.Contains("@") || !email.Contains("."))
                {
                    errors.Add($"[Dòng {lineNo}]: Email '{email}' không đúng định dạng.");
                    lineNo++;
                    continue;
                }

                rows.Add(new UserBulkRow(fullName, email, targetRole));
                lineNo++;
            }

            var loginBaseUrl = $"{Request.Scheme}://{Request.Host}/Auth/Login";
            var result = await _userService.CreateBulkUsersAsync(rows, loginBaseUrl);

            if (result.SuccessCount > 0)
            {
                TempData["Success"] = $"Đã tạo thành công {result.SuccessCount} tài khoản.";
            }

            var allErrors = errors.Concat(result.Errors).ToList();
            if (allErrors.Any())
            {
                TempData["Error"] = $"Có lỗi xảy ra:\n" + string.Join("\n", allErrors);
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostEditUserAsync(string editUserId, string role, bool isActive)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            var result = await _userService.EditUserAsync(currentUser.Id, editUserId, role, isActive);

            if (result.Succeeded)
            {
                TempData["Success"] = $"Đã cập nhật vai trò {role} cho người dùng thành công.";
            }
            else
            {
                TempData["Error"] = "Cập nhật thất bại: " + string.Join(", ", result.Errors.Select(e => e.Description));
            }

            return RedirectToPage();
        }

        // ── Excel Import ──────────────────────────────────────────────────────────
        public async Task<IActionResult> OnPostImportExcelAsync(IFormFile excelFile)
        {
            if (excelFile == null || excelFile.Length == 0)
            {
                TempData["Error"] = "Vui lòng chọn file Excel (.csv) để import.";
                return RedirectToPage();
            }

            var ext = Path.GetExtension(excelFile.FileName).ToLower();
            if (ext != ".csv")
            {
                TempData["Error"] = "Chỉ hỗ trợ file CSV. Vui lòng tải file mẫu và điền theo định dạng.";
                return RedirectToPage();
            }

            var lines = new List<string>();
            using (var reader = new System.IO.StreamReader(excelFile.OpenReadStream(), Encoding.UTF8))
            {
                string? line;
                var isHeader = true;
                var lineNum = 1;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (isHeader) { isHeader = false; lineNum++; continue; } // skip header row
                    if (!string.IsNullOrWhiteSpace(line)) lines.Add($"{lineNum}:{line}");
                    lineNum++;
                }
            }

            if (!lines.Any())
            {
                TempData["Error"] = "File không có dữ liệu (ngoài dòng tiêu đề).";
                return RedirectToPage();
            }

            var rows = new List<UserBulkRow>();
            var errors = new List<string>();

            foreach (var lineWithNum in lines)
            {
                var idx = lineWithNum.IndexOf(':');
                var lineNo = lineWithNum[..idx];
                var line = lineWithNum[(idx + 1)..];

                var parts = line.Split(',', StringSplitOptions.TrimEntries);
                if (parts.Length < 3)
                {
                    errors.Add($"[Dòng {lineNo}]: Sai định dạng (yêu cầu đủ 3 cột HoTen, Email, VaiTro). Nội dung: '{line}'");
                    continue;
                }

                var fullName = parts[0];
                var email = parts[1];
                var role = parts[2];

                if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email))
                {
                    errors.Add($"[Dòng {lineNo}]: Họ tên hoặc Email không được để trống.");
                    continue;
                }

                if (!email.Contains("@") || !email.Contains("."))
                {
                    errors.Add($"[Dòng {lineNo}]: Email '{email}' không đúng định dạng.");
                    continue;
                }

                var allowedRoles = new[] { "Student", "Lecturer", "Admin" };
                if (!allowedRoles.Contains(role))
                {
                    errors.Add($"[Dòng {lineNo}]: Vai trò '{role}' không hợp lệ (chỉ chấp nhận: Student, Lecturer, Admin).");
                    continue;
                }

                rows.Add(new UserBulkRow(fullName, email, role));
            }

            var loginBaseUrl = $"{Request.Scheme}://{Request.Host}/Auth/Login";
            var result = await _userService.CreateBulkUsersAsync(rows, loginBaseUrl);

            if (result.SuccessCount > 0) TempData["Success"] = $"Import thành công {result.SuccessCount} tài khoản.";
            
            var allErrors = errors.Concat(result.Errors).ToList();
            if (allErrors.Any()) TempData["Error"] = "Chi tiết lỗi:\n" + string.Join("\n", allErrors);

            return RedirectToPage();
        }

        // ── Download CSV Template ─────────────────────────────────────────────────
        public IActionResult OnGetDownloadTemplate()
        {
            var csv = new StringBuilder();
            csv.AppendLine("HoTen,Email,VaiTro");
            csv.AppendLine("Nguyễn Sinh Viên,sinhvien@truong.edu.vn,Student");
            csv.AppendLine("Trần Giảng Viên,giangvien@truong.edu.vn,Lecturer");
            csv.AppendLine("Lê Văn Admin,admin2@truong.edu.vn,Admin");

            // Add UTF-8 BOM to prevent Excel display corruption
            var bom = new byte[] { 0xEF, 0xBB, 0xBF };
            var csvBytes = Encoding.UTF8.GetBytes(csv.ToString());
            var bytes = bom.Concat(csvBytes).ToArray();

            return File(bytes, "text/csv; charset=utf-8", "mau_import_tai_khoan.csv");
        }

        public async Task<IActionResult> OnPostUpdateChunkSettingsAsync(int chunkSize)
        {
            if (chunkSize < 100 || chunkSize > 5000)
            {
                TempData["Error"] = "Độ dài phân mảnh phải nằm trong khoảng từ 100 đến 5000 ký tự.";
                return RedirectToPage();
            }

            try
            {
                var filePath = Path.Combine(_webHostEnvironment.ContentRootPath, "appsettings.json");
                if (System.IO.File.Exists(filePath))
                {
                    var jsonString = await System.IO.File.ReadAllTextAsync(filePath);
                    var jsonNode = System.Text.Json.Nodes.JsonNode.Parse(jsonString);
                    if (jsonNode != null)
                    {
                        var gemini = jsonNode["Gemini"];
                        if (gemini == null)
                        {
                            gemini = new System.Text.Json.Nodes.JsonObject();
                            jsonNode["Gemini"] = gemini;
                        }
                        gemini["ChunkSize"] = chunkSize;
                        await System.IO.File.WriteAllTextAsync(filePath, jsonNode.ToString());
                    }
                }
                TempData["Success"] = $"Đã cập nhật cấu hình độ dài phân mảnh tài liệu thành công: {chunkSize} ký tự!";
            }
            catch (System.Exception ex)
            {
                TempData["Error"] = $"Không thể cập nhật cấu hình: {ex.Message}";
            }

            return RedirectToPage();
        }
    }
}

