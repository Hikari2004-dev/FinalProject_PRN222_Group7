using FinalProject_PRN222_Group7.BLL.Services;
using FinalProject_PRN222_Group7.DAL.Data;
using FinalProject_PRN222_Group7.DAL.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FinalProject_PRN222_Group7.Pages.Documents
{
    public class IndexModel : PageModel
    {
        private readonly IDocumentService _docService;
        private readonly ICourseService _courseService;
        private readonly IChapterService _chapterService;
        private readonly UserManager<AppUser> _userManager;
        private readonly AppDbContext _context;

        public IndexModel(IDocumentService docService, ICourseService courseService, IChapterService chapterService, UserManager<AppUser> userManager, AppDbContext context)
        {
            _docService = docService;
            _courseService = courseService;
            _chapterService = chapterService;
            _userManager = userManager;
            _context = context;
        }

        public IEnumerable<Document> Documents { get; set; } = new List<Document>();
        public IEnumerable<Course> Courses { get; set; } = new List<Course>();
        public IDictionary<int, string> ChapterNames { get; set; } = new Dictionary<int, string>();
        public string CurrentUserId { get; set; } = "";
        public bool IsLecturer { get; set; }
        public bool IsAdmin { get; set; }

        [BindProperty(SupportsGet = true)] public int? CourseId { get; set; }
        [BindProperty(SupportsGet = true)] public DocumentStatus? Status { get; set; }
        [BindProperty(SupportsGet = true)] public string? Search { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Auth/Login");

            CurrentUserId = user.Id;
            IsLecturer = User.IsInRole("Lecturer");
            IsAdmin = User.IsInRole("Admin");

            if (!IsLecturer && !IsAdmin)
            {
                return RedirectToPage("/Dashboard/Index");
            }

            var docs = await _docService.GetAllDocumentsAsync();

            if (IsLecturer && !IsAdmin)
            {
                docs = docs.Where(d => d.Course.LecturerId == user.Id || d.UploadedById == user.Id).ToList();
            }

            if (CourseId.HasValue)
            {
                docs = docs.Where(d => d.CourseId == CourseId.Value).ToList();
            }

            if (Status.HasValue)
            {
                docs = docs.Where(d => d.Status == Status.Value).ToList();
            }

            if (!string.IsNullOrWhiteSpace(Search))
            {
                var term = Search.Trim().ToLower();
                docs = docs.Where(d => d.OriginalName.ToLower().Contains(term) || (d.Course?.Name ?? "").ToLower().Contains(term)).ToList();
            }

            Documents = docs.OrderByDescending(d => d.UploadedAt).ToList();

            Courses = (await _courseService.GetAllCoursesAsync()).OrderBy(c => c.Name).ToList();

            var allChapters = await _context.Chapters.ToListAsync();
            ChapterNames = allChapters.ToDictionary(ch => ch.Id, ch => ch.Name);

            return Page();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Auth/Login");

            var doc = await _docService.GetDocumentAsync(id);
            if (doc != null)
            {
                if (IsAdmin || (IsLecturer && doc.Course.LecturerId == user.Id))
                {
                    await _docService.DeleteAsync(id);
                    TempData["Success"] = "Đã xóa tài liệu.";
                }
                else
                {
                    TempData["Error"] = "Bạn không có quyền xóa tài liệu này.";
                }
            }

            return RedirectToPage(new { CourseId, Status, Search });
        }

        public async Task<IActionResult> OnPostReindexAsync(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Auth/Login");

            var doc = await _docService.GetDocumentAsync(id);
            if (doc != null)
            {
                if (IsAdmin || (IsLecturer && doc.Course.LecturerId == user.Id))
                {
                    await _docService.ProcessLocalDocumentAsync(id, doc.FilePath);
                    TempData["Success"] = $"Đã lập chỉ mục lại '{doc.OriginalName}' thành công.";
                }
                else
                {
                    TempData["Error"] = "Bạn không có quyền lập chỉ mục tài liệu này.";
                }
            }

            return RedirectToPage(new { CourseId, Status, Search });
        }
    }
}
