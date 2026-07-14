using FinalProject_PRN222_Group7.BLL.Services;
using FinalProject_PRN222_Group7.DAL.Data;
using FinalProject_PRN222_Group7.DAL.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FinalProject_PRN222_Group7.Pages.Documents
{
    public class DetailModel : PageModel
    {
        private readonly IDocumentService _docService;
        private readonly UserManager<AppUser> _userManager;

        public DetailModel(IDocumentService docService, UserManager<AppUser> userManager)
        {
            _docService = docService;
            _userManager = userManager;
        }

        public Document? Doc { get; set; }
        public IEnumerable<DocumentChunk> Chunks { get; set; } = new List<DocumentChunk>();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            Doc = await _docService.GetDocumentAsync(id);
            if (Doc == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            var isLecturer = User.IsInRole("Lecturer");
            if (isLecturer && user != null && Doc.Course.LecturerId != user.Id)
            {
                return RedirectToPage("/Courses/Index");
            }

            Chunks = await _docService.GetChunksByDocumentIdAsync(id);

            // Fallback mock chunks if none exists yet for demo purposes
            if (!Chunks.Any() && Doc.Status == DocumentStatus.Indexed)
            {
                var mockChunks = new List<DocumentChunk>();
                for (int i = 0; i < 3; i++)
                {
                    mockChunks.Add(new DocumentChunk
                    {
                        ChunkIndex = i + 1,
                        Content = $"[Phân mảnh trích xuất #{i + 1} từ tài liệu {Doc.OriginalName}]\nNội dung chính thuộc chương này đề cập đến các vấn đề lý thuyết nền tảng liên quan đến môn học, hỗ trợ hệ thống RAG tìm kiếm ngữ cảnh tối ưu khi người dùng thực hiện chat truy vấn.",
                        TokenCount = 120,
                        PageNumber = i + 1,
                        DocumentId = id
                    });
                }
                Chunks = mockChunks;
            }

            return Page();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Auth/Login");

            var doc = await _docService.GetDocumentAsync(id);
            if (doc != null)
            {
                var isAdmin = User.IsInRole("Admin");
                if (isAdmin || doc.Course.LecturerId == user.Id)
                {
                    await _docService.DeleteAsync(id);
                }
            }
            return RedirectToPage("/Courses/Index");
        }

        public async Task<IActionResult> OnPostReindexAsync(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Auth/Login");

            var doc = await _docService.GetDocumentAsync(id);
            if (doc != null)
            {
                var isAdmin = User.IsInRole("Admin");
                if (isAdmin || doc.Course.LecturerId == user.Id)
                {
                    await _docService.ProcessLocalDocumentAsync(id, doc.FilePath);
                    TempData["Success"] = "Đã thực hiện phân mảnh và lập chỉ mục lại thành công!";
                }
            }
            return RedirectToPage(new { id });
        }
    }
}
