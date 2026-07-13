using FinalProject_PRN222_Group7.BLL.Services;
using FinalProject_PRN222_Group7.DAL.Data;
using FinalProject_PRN222_Group7.DAL.Entities;
using FinalProject_PRN222_Group7.DAL.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FinalProject_PRN222_Group7
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // ── Database ──────────────────────────────────────────────────────
            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            // ── Identity ──────────────────────────────────────────────────────
            builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequiredLength = 6;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

            // ── Auth cookie ───────────────────────────────────────────────────
            builder.Services.ConfigureApplicationCookie(options =>
            {
                options.LoginPath = "/Auth/Login";
                options.LogoutPath = "/Auth/Logout";
                options.AccessDeniedPath = "/Auth/AccessDenied";
                options.ExpireTimeSpan = TimeSpan.FromDays(7);
                options.SlidingExpiration = true;
            });

            // ── Repositories ──────────────────────────────────────────────────
            builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
            builder.Services.AddScoped<IChatRepository, ChatRepository>();
            builder.Services.AddScoped<IQuizRepository, QuizRepository>();
            builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();

            // ── BLL Services ──────────────────────────────────────────────────
            builder.Services.AddScoped<IDocumentService, DocumentService>();
            builder.Services.AddScoped<ICourseService, CourseService>();
            builder.Services.AddScoped<IChapterService, ChapterService>();
            builder.Services.AddScoped<IChatService, ChatService>();
            builder.Services.AddScoped<IQuizService, QuizService>();
            builder.Services.AddScoped<IQuestionBankService, QuestionBankService>();
            builder.Services.AddScoped<IPaymentService, PaymentService>();
            builder.Services.AddScoped<IReportService, ReportService>();
            builder.Services.AddScoped<IEmailService, EmailService>();

            // ── SignalR ───────────────────────────────────────────────────────
            builder.Services.AddSignalR();

            // ── Controllers (for API endpoints) ──────────────────────────────
            builder.Services.AddControllers();

            // ── Razor Pages ───────────────────────────────────────────────────
            builder.Services.AddRazorPages(options =>
            {
                options.Conventions.AuthorizeFolder("/Dashboard");
                options.Conventions.AuthorizeFolder("/Documents");
                options.Conventions.AuthorizeFolder("/Chat");
                options.Conventions.AuthorizeFolder("/Quiz");
                options.Conventions.AuthorizeFolder("/Reports");
                options.Conventions.AuthorizeFolder("/Packages");
                options.Conventions.AuthorizeFolder("/Payments");
                options.Conventions.AuthorizeFolder("/Benchmark");
                options.Conventions.AuthorizeFolder("/Courses");
                options.Conventions.AuthorizeFolder("/Admin", "AdminOnly");
            });

            // ── Session ───────────────────────────────────────────────────────
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromHours(2);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });
            builder.Services.AddDistributedMemoryCache();

            // ── HTTP Client (for RAG Python API) ─────────────────────────────
            builder.Services.AddHttpClient("RAGService", client =>
            {
                client.BaseAddress = new Uri(builder.Configuration["RAGService:BaseUrl"] ?? "http://localhost:8000");
                client.Timeout = TimeSpan.FromSeconds(60);
            });
            // ── Authorization Policies ────────────────────────────────────────
            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
            });

            var app = builder.Build();

            // ── Middleware ────────────────────────────────────────────────────
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseSession();
            app.MapStaticAssets();
            app.MapRazorPages().WithStaticAssets();
            app.MapControllers();
            app.MapHub<ChatHub>("/chatHub");
            app.MapHub<FinalProject_PRN222_Group7.Hubs.LmsHub>("/lmsHub");

            // ── Seed Data ─────────────────────────────────────────────────────
            using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                await SeedDataAsync(services);
            }

            app.Run();
        }

        private static async Task SeedDataAsync(IServiceProvider services)
        {
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = services.GetRequiredService<UserManager<AppUser>>();
            var db = services.GetRequiredService<AppDbContext>();

            await db.Database.MigrateAsync();

            // Seed Roles
            string[] roles = ["Admin", "Lecturer", "Student"];
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
            }

            // Seed Admin
            if (await userManager.FindByEmailAsync("admin@lms.edu.vn") == null)
            {
                var admin = new AppUser { UserName = "admin@lms.edu.vn", Email = "admin@lms.edu.vn", FullName = "System Admin", EmailConfirmed = true };
                var result = await userManager.CreateAsync(admin, "Admin@123");
                if (result.Succeeded) await userManager.AddToRoleAsync(admin, "Admin");
            }

            // Seed Lecturer
            if (await userManager.FindByEmailAsync("lecturer@lms.edu.vn") == null)
            {
                var lecturer = new AppUser { UserName = "lecturer@lms.edu.vn", Email = "lecturer@lms.edu.vn", FullName = "Nguyễn Văn Thầy", EmailConfirmed = true };
                var result = await userManager.CreateAsync(lecturer, "Lecturer@123");
                if (result.Succeeded) await userManager.AddToRoleAsync(lecturer, "Lecturer");
            }

            // Seed Student
            if (await userManager.FindByEmailAsync("student@lms.edu.vn") == null)
            {
                var student = new AppUser { UserName = "student@lms.edu.vn", Email = "student@lms.edu.vn", FullName = "Trần Thị Sinh", EmailConfirmed = true };
                var result = await userManager.CreateAsync(student, "Student@123");
                if (result.Succeeded) await userManager.AddToRoleAsync(student, "Student");
            }

            // Seed Course (nếu chưa có)
            if (!db.Courses.Any())
            {
                var lecturer = await userManager.FindByEmailAsync("lecturer@lms.edu.vn");
                if (lecturer != null)
                {
                    db.Courses.AddRange(
                        new DAL.Entities.Course { Name = "Lập trình .NET", Code = "PRN222", Description = "Lập trình web với ASP.NET Core", LecturerId = lecturer.Id },
                        new DAL.Entities.Course { Name = "Trí tuệ nhân tạo", Code = "AI301", Description = "Nhập môn AI và Machine Learning", LecturerId = lecturer.Id },
                        new DAL.Entities.Course { Name = "Cơ sở dữ liệu", Code = "DBI202", Description = "SQL Server và Entity Framework", LecturerId = lecturer.Id }
                    );
                    await db.SaveChangesAsync();
                }
            }

            // Assign Basic package to student
            var studentUser = await userManager.FindByEmailAsync("student@lms.edu.vn");
            if (studentUser != null && !db.UserPackages.Any(up => up.UserId == studentUser.Id))
            {
                db.UserPackages.Add(new DAL.Entities.UserPackage
                {
                    UserId = studentUser.Id,
                    PackageId = 1,
                    StartDate = DateTime.UtcNow,
                    EndDate = DateTime.UtcNow.AddYears(1),
                    RemainingQueries = 50,
                    IsActive = true
                });
                await db.SaveChangesAsync();
            }
        }
    }

    // ── SignalR Chat Hub ──────────────────────────────────────────────────────
    public class ChatHub : Microsoft.AspNetCore.SignalR.Hub
    {
        public async Task JoinSession(string sessionId)
            => await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);

        public async Task LeaveSession(string sessionId)
            => await Groups.RemoveFromGroupAsync(Context.ConnectionId, sessionId);
    }
}
