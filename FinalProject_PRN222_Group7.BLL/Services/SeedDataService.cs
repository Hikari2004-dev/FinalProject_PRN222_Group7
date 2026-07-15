using FinalProject_PRN222_Group7.BLL.Abstractions;
using FinalProject_PRN222_Group7.DAL.Data;
using FinalProject_PRN222_Group7.DAL.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FinalProject_PRN222_Group7.BLL.Services
{
    public class SeedDataService : ISeedDataService
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<AppUser> _userManager;
        private readonly AppDbContext _db;
        private readonly ISubscriptionService _subscriptionService;
        private readonly ICreditWalletService _walletService;

        public SeedDataService(
            RoleManager<IdentityRole> roleManager,
            UserManager<AppUser> userManager,
            AppDbContext db,
            ISubscriptionService subscriptionService,
            ICreditWalletService walletService)
        {
            _roleManager = roleManager;
            _userManager = userManager;
            _db = db;
            _subscriptionService = subscriptionService;
            _walletService = walletService;
        }

        public async Task SeedAsync()
        {
            await _db.Database.MigrateAsync();

            string[] roles = ["Admin", "Lecturer", "Student"];
            foreach (var role in roles)
            {
                if (!await _roleManager.RoleExistsAsync(role))
                    await _roleManager.CreateAsync(new IdentityRole(role));
            }

            if (await _userManager.FindByEmailAsync("admin@lms.edu.vn") == null)
            {
                var admin = new AppUser { UserName = "admin@lms.edu.vn", Email = "admin@lms.edu.vn", FullName = "System Admin", EmailConfirmed = true };
                var result = await _userManager.CreateAsync(admin, "Admin@123");
                if (result.Succeeded) await _userManager.AddToRoleAsync(admin, "Admin");
            }

            if (await _userManager.FindByEmailAsync("lecturer@lms.edu.vn") == null)
            {
                var lecturer = new AppUser { UserName = "lecturer@lms.edu.vn", Email = "lecturer@lms.edu.vn", FullName = "Nguyễn Văn Thầy", EmailConfirmed = true };
                var result = await _userManager.CreateAsync(lecturer, "Lecturer@123");
                if (result.Succeeded) await _userManager.AddToRoleAsync(lecturer, "Lecturer");
            }

            if (await _userManager.FindByEmailAsync("student@lms.edu.vn") == null)
            {
                var student = new AppUser { UserName = "student@lms.edu.vn", Email = "student@lms.edu.vn", FullName = "Trần Thị Sinh", EmailConfirmed = true };
                var result = await _userManager.CreateAsync(student, "Student@123");
                if (result.Succeeded) await _userManager.AddToRoleAsync(student, "Student");
            }

            if (!_db.Courses.Any())
            {
                var lecturer = await _userManager.FindByEmailAsync("lecturer@lms.edu.vn");
                if (lecturer != null)
                {
                    _db.Courses.AddRange(
                        new Course { Name = "Lập trình .NET", Code = "PRN222", Description = "Lập trình web với ASP.NET Core", LecturerId = lecturer.Id },
                        new Course { Name = "Trí tuệ nhân tạo", Code = "AI301", Description = "Nhập môn AI và Machine Learning", LecturerId = lecturer.Id },
                        new Course { Name = "Cơ sở dữ liệu", Code = "DBI202", Description = "SQL Server và Entity Framework", LecturerId = lecturer.Id }
                    );
                    await _db.SaveChangesAsync();
                }
            }

            var adminUser = await _userManager.FindByEmailAsync("admin@lms.edu.vn");
            var lecturerUser = await _userManager.FindByEmailAsync("lecturer@lms.edu.vn");
            var studentUser = await _userManager.FindByEmailAsync("student@lms.edu.vn");

            if (adminUser != null)
            {
                await _walletService.EnsureWalletAsync(adminUser.Id, ["Admin"]);
            }

            if (lecturerUser != null)
            {
                await _walletService.EnsureWalletAsync(lecturerUser.Id, ["Lecturer"]);
            }

            if (studentUser != null)
            {
                await _walletService.EnsureWalletAsync(studentUser.Id, ["Student"]);
                await _subscriptionService.EnsureCompatibilityAsync(studentUser.Id);

                var activeSubscription = await _subscriptionService.GetActiveSubscriptionAsync(studentUser.Id);
                if (activeSubscription == null)
                {
                    await _subscriptionService.ActivatePackageAsync(studentUser.Id, 10);
                }
            }
        }
    }
}
