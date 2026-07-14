using FinalProject_PRN222_Group7.BLL.Services;
using FinalProject_PRN222_Group7.DAL.Data;
using FinalProject_PRN222_Group7.DAL.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace FinalProject_PRN222_Group7.Api;

[ApiController]
[Route("api/documents")]
[Authorize]
public class DocumentsApiController : ControllerBase
{
    private readonly IDocumentService _documentService;
    private readonly AppDbContext _context;
    private readonly UserManager<AppUser> _userManager;
    private readonly ILogger<DocumentsApiController> _logger;

    public DocumentsApiController(IDocumentService documentService, AppDbContext context, UserManager<AppUser> userManager, ILogger<DocumentsApiController> logger)
    {
        _documentService = documentService;
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    public record DocumentDto(
        int Id,
        string FileName,
        string OriginalName,
        long FileSizeBytes,
        string ContentType,
        DocumentStatus Status,
        int ChunkCount,
        string? EmbeddingModel,
        string? ErrorMessage,
        DateTime UploadedAt,
        DateTime? IndexedAt,
        int CourseId,
        string? CourseName,
        int? ChapterId,
        string? ChapterName,
        string UploadedByUserEmail,
        string? UploadedByFullName
    );

    public record CreateDocumentRequest(int CourseId, int? ChapterId, string FileName, string ContentType, long FileSizeBytes);

    public record ChunkDto(int Id, int ChunkIndex, string Content, int TokenCount, int PageNumber);

    public record DocumentStatistics(int TotalDocuments, int IndexedDocuments, int ProcessingDocuments, int FailedDocuments, int TotalChunks, long TotalSizeBytes);

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? courseId = null, [FromQuery] int? chapterId = null)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var documents = await _documentService.GetAllDocumentsAsync(user.Id, courseId);

        if (chapterId.HasValue)
        {
            documents = documents.Where(d => d.ChapterId == chapterId.Value);
        }

        var result = documents.Select(d => new DocumentDto(
            d.Id,
            d.FileName,
            d.OriginalName,
            d.FileSizeBytes,
            d.ContentType,
            d.Status,
            d.ChunkCount,
            d.EmbeddingModel,
            d.ErrorMessage,
            d.UploadedAt,
            d.IndexedAt,
            d.CourseId,
            d.Course?.Name,
            d.ChapterId,
            d.Chapter?.Name,
            d.UploadedByUserEmail,
            d.UploadedBy?.FullName
        ));
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var doc = await _documentService.GetDocumentAsync(id);
        if (doc == null) return NotFound(new { message = "Document not found" });

        if (!User.IsInRole("Admin") && !User.IsInRole("Lecturer") && doc.UploadedById != user.Id)
        {
            return Forbid();
        }

        var dto = new DocumentDto(
            doc.Id,
            doc.FileName,
            doc.OriginalName,
            doc.FileSizeBytes,
            doc.ContentType,
            doc.Status,
            doc.ChunkCount,
            doc.EmbeddingModel,
            doc.ErrorMessage,
            doc.UploadedAt,
            doc.IndexedAt,
            doc.CourseId,
            doc.Course?.Name,
            doc.ChapterId,
            doc.Chapter?.Name,
            doc.UploadedByUserEmail,
            doc.UploadedBy?.FullName
        );
        return Ok(dto);
    }

    [HttpGet("{id}/chunks")]
    public async Task<IActionResult> GetChunks(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var doc = await _context.Documents
            .Include(d => d.Chunks)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (doc == null) return NotFound(new { message = "Document not found" });

        if (!User.IsInRole("Admin") && !User.IsInRole("Lecturer") && doc.UploadedById != user.Id)
        {
            return Forbid();
        }

        var chunks = doc.Chunks.OrderBy(c => c.ChunkIndex).Select(c => new ChunkDto(
            c.Id,
            c.ChunkIndex,
            c.Content,
            c.TokenCount,
            c.PageNumber
        ));
        return Ok(chunks);
    }

    [HttpGet("stats")]
    [Authorize(Roles = "Admin,Lecturer")]
    public async Task<IActionResult> GetStatistics()
    {
        var total = await _context.Documents.CountAsync();
        var indexed = await _context.Documents.CountAsync(d => d.Status == DocumentStatus.Indexed);
        var processing = await _context.Documents.CountAsync(d => d.Status == DocumentStatus.Processing);
        var failed = await _context.Documents.CountAsync(d => d.Status == DocumentStatus.Failed);
        var totalChunks = await _context.DocumentChunks.CountAsync();
        var totalSize = await _context.Documents.SumAsync(d => d.FileSizeBytes);

        var stats = new DocumentStatistics(total, indexed, processing, failed, totalChunks, totalSize);
        return Ok(stats);
    }

    [HttpGet("course/{courseId}/summary")]
    public async Task<IActionResult> GetCourseDocumentSummary(int courseId)
    {
        var documents = await _context.Documents
            .Where(d => d.CourseId == courseId)
            .Include(d => d.Course)
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync();

        var result = documents.GroupBy(d => d.Status)
            .Select(g => new
            {
                Status = g.Key.ToString(),
                Count = g.Count(),
                TotalSizeBytes = g.Sum(d => d.FileSizeBytes)
            });

        return Ok(result);
    }

    [HttpPost("{id}/reindex")]
    [Authorize(Roles = "Admin,Lecturer")]
    public async Task<IActionResult> ReindexDocument(int id)
    {
        var doc = await _context.Documents.FirstOrDefaultAsync(d => d.Id == id);
        if (doc == null) return NotFound(new { message = "Document not found" });

        await _documentService.UpdateStatusAsync(id, DocumentStatus.Processing);
        return Accepted(new { message = "Document re-indexing started" });
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,Lecturer")]
    public async Task<IActionResult> DeleteDocument(int id)
    {
        await _documentService.DeleteAsync(id);
        return Ok(new { message = "Document deleted successfully" });
    }
}
