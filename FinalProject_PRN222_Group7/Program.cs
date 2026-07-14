using FinalProject_PRN222_Group7.BLL.Abstractions;
using FinalProject_PRN222_Group7.BLL.Services;
using FinalProject_PRN222_Group7.DAL.Data;
using FinalProject_PRN222_Group7.DAL.Entities;
using FinalProject_PRN222_Group7.DAL.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PayOS;

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

            // ── Repositories ──────────────────────────────────────────────────
            builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
            builder.Services.AddScoped<IChatRepository, ChatRepository>();
            builder.Services.AddScoped<IQuizRepository, QuizRepository>();
            builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();

            // ── BLL Services ──────────────────────────────────────────────────
            builder.Services.AddScoped<IDocumentService, DocumentService>();
            builder.Services.AddScoped<ICourseService, CourseService>();
            builder.Services.AddScoped<IChapterService, ChapterService>();
            builder.Services.AddScoped<ICreditWalletService, CreditWalletService>();
            builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
            builder.Services.AddScoped<IAiUsageGate, AiUsageGate>();
            builder.Services.AddScoped<IChatService, ChatService>();
            builder.Services.AddScoped<IQuizService, QuizService>();
            builder.Services.AddScoped<IQuestionBankService, QuestionBankService>();
            builder.Services.AddScoped<IPaymentService, PaymentService>();
            builder.Services.AddScoped<IReportService, ReportService>();
            builder.Services.AddScoped<IEmailService, EmailService>();
            builder.Services.AddScoped<ISeedDataService, SeedDataService>();

            // ── PayOS ─────────────────────────────────────────────────────
            var payOSClientId = builder.Configuration["PayOS:ClientId"] ?? "";
            var payOSApiKey = builder.Configuration["PayOS:ApiKey"] ?? "";
            var payOSChecksumKey = builder.Configuration["PayOS:ChecksumKey"] ?? "";
            builder.Services.AddSingleton(new PayOSClient(new PayOSOptions
            {
                ClientId = payOSClientId,
                ApiKey = payOSApiKey,
                ChecksumKey = payOSChecksumKey
            }));

            // ── HTTP Client (for RAG Python API) ─────────────────────────────
            builder.Services.AddHttpClient("RAGService", client =>
            {
                client.BaseAddress = new Uri(builder.Configuration["RAGService:BaseUrl"] ?? "http://localhost:8000");
                client.Timeout = TimeSpan.FromSeconds(60);
            });

            // ── Identity cookie ───────────────────────────────────────────────

            // ── Auth cookie ───────────────────────────────────────────────────
            builder.Services.ConfigureApplicationCookie(options =>
            {
                options.LoginPath = "/Auth/Login";
                options.LogoutPath = "/Auth/Logout";
                options.AccessDeniedPath = "/Auth/AccessDenied";
                options.ExpireTimeSpan = TimeSpan.FromDays(7);
                options.SlidingExpiration = true;
            });

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
                options.Conventions.AuthorizeFolder("/Reports", "StaffOnly");
                options.Conventions.AuthorizeFolder("/Packages", "StudentOnly");
                options.Conventions.AuthorizeFolder("/Payments", "StudentOnly");
                options.Conventions.AuthorizeFolder("/Benchmark", "StaffOnly");
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

            // ── Authorization Policies ────────────────────────────────────────
            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
                options.AddPolicy("LecturerOnly", policy => policy.RequireRole("Lecturer"));
                options.AddPolicy("StudentOnly", policy => policy.RequireRole("Student"));
                options.AddPolicy("StaffOnly", policy => policy.RequireRole("Admin", "Lecturer"));
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
                var seedDataService = scope.ServiceProvider.GetRequiredService<ISeedDataService>();
                await seedDataService.SeedAsync();
            }

            app.Run();
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
