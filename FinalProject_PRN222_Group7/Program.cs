using System.Security.Claims;
using FinalProject_PRN222_Group7.BLL.DTOs;
using FinalProject_PRN222_Group7.BLL.Services;
using FinalProject_PRN222_Group7.DAL.Data;
using FinalProject_PRN222_Group7.DAL.Enums;
using FinalProject_PRN222_Group7.DAL.Repositories;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinalProject_PRN222_Group7
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddRazorPages(options =>
            {
                options.Conventions.AuthorizeFolder("/Documents", "LecturerOnly");
                options.Conventions.AuthorizeFolder("/Chat", "StudentOnly");
                options.Conventions.AuthorizeFolder("/Quiz", "StudentOnly");
            });

            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.LoginPath = "/Auth/Login";
                    options.LogoutPath = "/Auth/Logout";
                    options.AccessDeniedPath = "/Auth/Login";
                    options.ExpireTimeSpan = TimeSpan.FromHours(24);
                    options.Events.OnRedirectToLogin = context =>
                    {
                        if (context.Request.Path.StartsWithSegments("/api"))
                        {
                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            return Task.CompletedTask;
                        }

                        context.Response.Redirect(context.RedirectUri);
                        return Task.CompletedTask;
                    };
                    options.Events.OnRedirectToAccessDenied = context =>
                    {
                        if (context.Request.Path.StartsWithSegments("/api"))
                        {
                            context.Response.StatusCode = StatusCodes.Status403Forbidden;
                            return Task.CompletedTask;
                        }

                        context.Response.Redirect(context.RedirectUri);
                        return Task.CompletedTask;
                    };
                });

            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("LecturerOnly", policy => policy.RequireRole(nameof(UserRole.Lecturer)));
                options.AddPolicy("StudentOnly", policy => policy.RequireRole(nameof(UserRole.Student)));
            });

            builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
            builder.Services.AddScoped<IUserRepository, UserRepository>();
            builder.Services.AddScoped<ICourseRepository, CourseRepository>();
            builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
            builder.Services.AddScoped<IChatSessionRepository, ChatSessionRepository>();

            builder.Services.AddScoped<IAuthService, AuthService>();
            builder.Services.AddScoped<IDocumentService, DocumentService>();
            builder.Services.AddScoped<IChatService, ChatService>();
            builder.Services.AddHttpClient("RagPipeline");
            builder.Services.AddScoped<IRagPipelineClient>(serviceProvider =>
            {
                var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
                return new RagPipelineClient(
                    httpClientFactory.CreateClient("RagPipeline"),
                    builder.Configuration["Rag:PipelineEndpoint"]);
            });

            var app = builder.Build();

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
            }

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapStaticAssets();
            app.MapRazorPages()
               .WithStaticAssets();

            var documentApi = app.MapGroup("/api/documents")
                .RequireAuthorization("LecturerOnly");

            documentApi.MapPost("/upload", UploadDocumentAsync)
                .DisableAntiforgery();

            var chatApi = app.MapGroup("/api/chat")
                .RequireAuthorization("StudentOnly");

            chatApi.MapGet("/sessions", GetChatSessionsAsync);
            chatApi.MapGet("/sessions/{sessionId:int}/history", GetChatHistoryAsync);

            await DbSeeder.SeedAsync(app.Services);

            app.Run();
        }

        private static async Task<IResult> UploadDocumentAsync(
            [FromForm] string? title,
            [FromForm] int courseId,
            [FromForm] IFormFile file,
            ClaimsPrincipal user,
            IWebHostEnvironment environment,
            IDocumentService documentService,
            CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId(user);
            if (userId is null)
                return Results.Unauthorized();

            if (file.Length == 0)
                return Results.BadRequest(new { error = "Uploaded file is empty." });

            var originalFileName = Path.GetFileName(file.FileName);
            var safeFileName = $"{Guid.NewGuid():N}_{originalFileName}";
            var uploadRoot = Path.Combine(environment.WebRootPath, "uploads", "documents");
            Directory.CreateDirectory(uploadRoot);

            var physicalPath = Path.Combine(uploadRoot, safeFileName);
            await using (var stream = File.Create(physicalPath))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            var relativePath = $"/uploads/documents/{safeFileName}";
            DocumentUploadResultDto result;
            try
            {
                result = await documentService.UploadAsync(
                    string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(originalFileName) : title,
                    originalFileName,
                    relativePath,
                    string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                    file.Length,
                    courseId,
                    userId.Value,
                    cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                if (File.Exists(physicalPath))
                    File.Delete(physicalPath);

                return Results.BadRequest(new { error = ex.Message });
            }

            return Results.Created($"/api/documents/{result.Id}", result);
        }

        private static async Task<IResult> GetChatSessionsAsync(
            ClaimsPrincipal user,
            IChatService chatService)
        {
            var userId = GetCurrentUserId(user);
            if (userId is null)
                return Results.Unauthorized();

            var sessions = await chatService.GetSessionsForUserAsync(userId.Value);
            return Results.Ok(sessions);
        }

        private static async Task<IResult> GetChatHistoryAsync(
            int sessionId,
            ClaimsPrincipal user,
            IChatService chatService)
        {
            var userId = GetCurrentUserId(user);
            if (userId is null)
                return Results.Unauthorized();

            var history = await chatService.GetSessionHistoryAsync(sessionId, userId.Value);
            return history is null ? Results.NotFound() : Results.Ok(history);
        }

        private static int? GetCurrentUserId(ClaimsPrincipal user)
        {
            var claimValue = user.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(claimValue, out var userId) ? userId : null;
        }
    }
}
