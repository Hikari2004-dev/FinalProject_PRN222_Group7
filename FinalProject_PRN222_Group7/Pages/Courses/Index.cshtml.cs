using FinalProject_PRN222_Group7.BLL.Services;
using FinalProject_PRN222_Group7.DAL.Entities;
using FinalProject_PRN222_Group7.Hubs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FinalProject_PRN222_Group7.Pages.Courses
{
    public class IndexModel : PageModel
    {
        private readonly ICourseService _courseService;
        private readonly IDocumentService _docService;
        private readonly IChapterService _chapterService;
        private readonly UserManager<AppUser> _userManager;
        private readonly IWebHostEnvironment _env;
        private readonly IHubContext<LmsHub> _hubContext;

        public IndexModel(
            ICourseService courseService,
            IDocumentService docService,
            IChapterService chapterService,
            UserManager<AppUser> userManager,
            IWebHostEnvironment env,
            IHubContext<LmsHub> hubContext)
        {
            _courseService = courseService;
            _docService = docService;
            _chapterService = chapterService;
            _userManager = userManager;
            _env = env;
            _hubContext = hubContext;
        }

        public IEnumerable<Course> Courses { get; set; } = new List<Course>();
        public IEnumerable<AppUser> Lecturers { get; set; } = new List<AppUser>();
        public string CurrentUserId { get; set; } = "";

        [BindProperty] public IFormFile? UploadedFile { get; set; }
        [BindProperty] public int UploadCourseId { get; set; }
        [BindProperty] public int? UploadChapterId { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Auth/Login");

            CurrentUserId = user.Id;
            var isLecturer = User.IsInRole("Lecturer");
            var isAdmin = User.IsInRole("Admin");

            if (isLecturer)
            {
                Courses = await _courseService.GetAllCoursesAsync(lecturerId: user.Id);
            }
            else
            {
                Courses = await _courseService.GetAllCoursesAsync();
                Lecturers = await _userManager.GetUsersInRoleAsync("Lecturer");
            }

            return Page();
        }

        // Chỉ Admin được tạo môn học mới
        public async Task<IActionResult> OnPostAsync(string name, string code, string? description, string? lecturerId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Auth/Login");

            if (!User.IsInRole("Admin"))
            {
                return Forbid();
            }

            var targetLecturerId = lecturerId ?? user.Id;

            var course = new Course 
            { 
                Name = name, 
                Code = code, 
                Description = description ?? "", 
                LecturerId = targetLecturerId 
            };
            await _courseService.CreateCourseAsync(course);

            // Bắn tín hiệu cập nhật thời gian thực
            await _hubContext.Clients.All.SendAsync("ReceiveCourseUpdate");

            return RedirectToPage();
        }

        // Chỉ Admin được chỉnh sửa môn học
        public async Task<IActionResult> OnPostEditCourseAsync(int id, string name, string code, string? description, string? lecturerId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Auth/Login");

            if (!User.IsInRole("Admin"))
            {
                return Forbid();
            }

            var course = await _courseService.GetCourseAsync(id);
            if (course == null) return NotFound();

            course.Name = name;
            course.Code = code;
            course.Description = description ?? "";
            
            if (!string.IsNullOrEmpty(lecturerId))
            {
                course.LecturerId = lecturerId;
            }

            await _courseService.UpdateCourseAsync(course);

            // Bắn tín hiệu cập nhật thời gian thực
            await _hubContext.Clients.All.SendAsync("ReceiveCourseUpdate");

            return RedirectToPage();
        }

        // Chỉ Admin được xóa môn học
        public async Task<IActionResult> OnPostDeleteCourseAsync(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Auth/Login");

            if (!User.IsInRole("Admin"))
            {
                return Forbid();
            }

            var course = await _courseService.GetCourseAsync(id);
            if (course == null) return NotFound();

            await _courseService.DeleteAsync(id);

            // Bắn tín hiệu cập nhật thời gian thực
            await _hubContext.Clients.All.SendAsync("ReceiveCourseUpdate");

            return RedirectToPage();
        }

        // Chỉ Giảng viên phụ trách môn học đó hoặc Admin mới được upload tài liệu
        public async Task<IActionResult> OnPostUploadDocAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Auth/Login");

            var isAdmin = User.IsInRole("Admin");

            if (!isAdmin && !User.IsInRole("Lecturer"))
            {
                TempData["Error"] = "Chỉ giảng viên mới được quyền tải lên tài liệu giảng dạy.";
                return RedirectToPage();
            }

            if (UploadedFile == null || UploadedFile.Length == 0)
            {
                TempData["Error"] = "Vui lòng chọn file tài liệu.";
                return RedirectToPage();
            }

            var course = await _courseService.GetCourseAsync(UploadCourseId);
            if (course == null)
            {
                TempData["Error"] = "Môn học không tồn tại.";
                return RedirectToPage();
            }

            if (!isAdmin && course.LecturerId != user.Id)
            {
                TempData["Error"] = "Bạn không có quyền upload tài liệu cho môn học này.";
                return RedirectToPage();
            }

            var ext = Path.GetExtension(UploadedFile.FileName).ToLower();
            var allowed = new[] { ".txt", ".pdf", ".docx" };
            if (!allowed.Contains(ext))
            {
                TempData["Error"] = "Chỉ hỗ trợ file .txt, .pdf, .docx";
                return RedirectToPage();
            }

            var uploadDir = Path.Combine(_env.WebRootPath, "uploads");
            Directory.CreateDirectory(uploadDir);
            var uniqueName = $"{Guid.NewGuid()}{ext}";
            var filePath = Path.Combine(uploadDir, uniqueName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await UploadedFile.CopyToAsync(stream);
            }

            var doc = new Document
            {
                FileName = uniqueName,
                OriginalName = UploadedFile.FileName,
                FilePath = filePath,
                FileSizeBytes = UploadedFile.Length,
                ContentType = UploadedFile.ContentType,
                CourseId = UploadCourseId,
                ChapterId = UploadChapterId,
                UploadedById = user.Id,
                UploadedByUserEmail = user.Email ?? "",
                Status = DocumentStatus.Uploaded
            };

            await _docService.CreateDocumentAsync(doc);

            // Chạy chunking cục bộ
            await _docService.ProcessLocalDocumentAsync(doc.Id, filePath);

            // Bắn tín hiệu cập nhật thời gian thực
            await _hubContext.Clients.All.SendAsync("ReceiveCourseUpdate");

            TempData["Success"] = $"Đã tải lên và đánh chỉ mục tài liệu '{UploadedFile.FileName}' thành công!";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteDocumentAsync(int docId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Auth/Login");

            var doc = await _docService.GetDocumentAsync(docId);
            if (doc != null)
            {
                var isAdmin = User.IsInRole("Admin");
                if (isAdmin || (User.IsInRole("Lecturer") && doc.Course.LecturerId == user.Id))
                {
                    await _docService.DeleteAsync(docId);

                    // Bắn tín hiệu cập nhật thời gian thực
                    await _hubContext.Clients.All.SendAsync("ReceiveCourseUpdate");

                    TempData["Success"] = "Đã xóa tài liệu thành công.";
                }
                else
                {
                    TempData["Error"] = "Bạn không có quyền xóa tài liệu này.";
                }
            }
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostReindexDocumentAsync(int docId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Auth/Login");

            var doc = await _docService.GetDocumentAsync(docId);
            if (doc != null)
            {
                var isAdmin = User.IsInRole("Admin");
                if (isAdmin || (User.IsInRole("Lecturer") && doc.Course.LecturerId == user.Id))
                {
                    var filePath = ResolveDocumentFilePath(doc);
                    if (filePath == null)
                    {
                        TempData["Error"] = "Không tìm thấy file gốc trên máy chủ.";
                        return RedirectToPage();
                    }

                    await _docService.ProcessLocalDocumentAsync(docId, filePath);

                    // Bắn tín hiệu cập nhật thời gian thực
                    await _hubContext.Clients.All.SendAsync("ReceiveCourseUpdate");

                    TempData["Success"] = $"Đã lập chỉ mục lại tài liệu '{doc.OriginalName}' thành công!";
                }
                else
                {
                    TempData["Error"] = "Bạn không có quyền lập chỉ mục lại tài liệu này.";
                }
            }
            return RedirectToPage();
        }

        public async Task<IActionResult> OnGetDownloadDocumentAsync(int docId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Auth/Login");

            var doc = await _docService.GetDocumentAsync(docId);
            if (doc == null) return NotFound();

            if (User.IsInRole("Lecturer") && doc.Course.LecturerId != user.Id)
            {
                return Forbid();
            }

            var filePath = ResolveDocumentFilePath(doc);
            if (filePath == null)
            {
                TempData["Error"] = "Không tìm thấy file gốc trên máy chủ.";
                return RedirectToPage();
            }

            var contentType = string.IsNullOrWhiteSpace(doc.ContentType)
                ? "application/octet-stream"
                : doc.ContentType;

            return PhysicalFile(filePath, contentType, doc.OriginalName);
        }

        private string? ResolveDocumentFilePath(Document doc)
        {
            if (!string.IsNullOrWhiteSpace(doc.FilePath) && System.IO.File.Exists(doc.FilePath))
            {
                return doc.FilePath;
            }

            if (!string.IsNullOrWhiteSpace(doc.FileName))
            {
                var uploadPath = Path.Combine(_env.WebRootPath, "uploads", doc.FileName);
                if (System.IO.File.Exists(uploadPath))
                {
                    return uploadPath;
                }
            }

            return null;
        }

        public async Task<IActionResult> OnPostAddChapterAsync(int courseId, string name, string? description)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Auth/Login");

            var course = await _courseService.GetCourseAsync(courseId);
            if (course == null)
            {
                TempData["Error"] = "Môn học không tồn tại.";
                return RedirectToPage();
            }

            var isAdmin = User.IsInRole("Admin");
            if (!isAdmin && course.LecturerId != user.Id)
            {
                TempData["Error"] = "Bạn không có quyền quản lý chương học của môn này.";
                return RedirectToPage();
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Tên chương không được để trống.";
                return RedirectToPage();
            }

            var chapter = new Chapter
            {
                CourseId = courseId,
                Name = name,
                Description = description
            };

            await _chapterService.CreateChapterAsync(chapter);
            await _hubContext.Clients.All.SendAsync("ReceiveCourseUpdate");

            TempData["Success"] = $"Đã tạo chương học '{name}' thành công!";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteChapterAsync(int chapterId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Auth/Login");

            var chapter = await _chapterService.GetChapterAsync(chapterId);
            if (chapter != null)
            {
                var course = await _courseService.GetCourseAsync(chapter.CourseId);
                var isAdmin = User.IsInRole("Admin");
                if (isAdmin || (course != null && course.LecturerId == user.Id))
                {
                    await _chapterService.DeleteChapterAsync(chapterId);
                    await _hubContext.Clients.All.SendAsync("ReceiveCourseUpdate");
                    TempData["Success"] = "Đã xóa chương học thành công.";
                }
                else
                {
                    TempData["Error"] = "Bạn không có quyền xóa chương học này.";
                }
            }
            return RedirectToPage();
        }
    }
}
