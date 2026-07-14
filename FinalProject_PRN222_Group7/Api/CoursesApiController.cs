using FinalProject_PRN222_Group7.BLL.Services;
using FinalProject_PRN222_Group7.DAL.Data;
using FinalProject_PRN222_Group7.DAL.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinalProject_PRN222_Group7.Api;

[ApiController]
[Route("api/courses")]
[Authorize]
public class CoursesApiController : ControllerBase
{
    private readonly ICourseService _courseService;
    private readonly IChapterService _chapterService;
    private readonly AppDbContext _context;

    public CoursesApiController(ICourseService courseService, IChapterService chapterService, AppDbContext context)
    {
        _courseService = courseService;
        _chapterService = chapterService;
        _context = context;
    }

    public record CourseDto(int Id, string Code, string Name, string? Description, string LecturerName, int DocumentCount, int ChapterCount, int QuizCount, DateTime CreatedAt, bool IsActive);

    public record ChapterDto(int Id, string Name, string? Description, int OrderIndex, DateTime CreatedAt, int DocumentCount);

    public record CreateCourseRequest(string Code, string Name, string? Description);

    public record UpdateCourseRequest(string Name, string? Description, bool IsActive);

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var courses = await _courseService.GetAllCoursesAsync();
        var result = new List<CourseDto>();

        foreach (var c in courses)
        {
            var docCount = await _context.Documents.CountAsync(d => d.CourseId == c.Id);
            var chapterCount = await _context.Chapters.CountAsync(ch => ch.CourseId == c.Id);
            var quizCount = await _context.Quizzes.CountAsync(q => q.CourseId == c.Id);

            result.Add(new CourseDto(
                c.Id,
                c.Code,
                c.Name,
                c.Description,
                c.Lecturer?.FullName ?? "Unknown",
                docCount,
                chapterCount,
                quizCount,
                c.CreatedAt,
                c.IsActive
            ));
        }

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var course = await _courseService.GetCourseAsync(id);
        if (course == null) return NotFound(new { message = "Course not found" });

        var docCount = await _context.Documents.CountAsync(d => d.CourseId == course.Id);
        var chapterCount = await _context.Chapters.CountAsync(ch => ch.CourseId == course.Id);
        var quizCount = await _context.Quizzes.CountAsync(q => q.CourseId == course.Id);

        var dto = new CourseDto(
            course.Id,
            course.Code,
            course.Name,
            course.Description,
            course.Lecturer?.FullName ?? "Unknown",
            docCount,
            chapterCount,
            quizCount,
            course.CreatedAt,
            course.IsActive
        );
        return Ok(dto);
    }

    [HttpGet("{id}/chapters")]
    public async Task<IActionResult> GetChapters(int id)
    {
        var course = await _courseService.GetCourseAsync(id);
        if (course == null) return NotFound(new { message = "Course not found" });

        var chapters = await _chapterService.GetCourseChaptersAsync(id);
        var result = new List<ChapterDto>();

        foreach (var ch in chapters)
        {
            var docCount = await _context.Documents.CountAsync(d => d.ChapterId == ch.Id);
            result.Add(new ChapterDto(
                ch.Id,
                ch.Name,
                ch.Description,
                ch.OrderIndex,
                ch.CreatedAt,
                docCount
            ));
        }

        return Ok(result);
    }

    [HttpGet("{id}/documents")]
    public async Task<IActionResult> GetDocuments(int id)
    {
        var course = await _courseService.GetCourseAsync(id);
        if (course == null) return NotFound(new { message = "Course not found" });

        var documents = await _context.Documents
            .Include(d => d.UploadedBy)
            .Where(d => d.CourseId == id)
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync();

        var result = documents.Select(d => new
        {
            d.Id,
            d.FileName,
            d.OriginalName,
            d.FileSizeBytes,
            d.ContentType,
            d.Status,
            d.ChunkCount,
            d.UploadedAt,
            UploadedBy = d.UploadedBy?.FullName ?? d.UploadedByUserEmail
        });
        return Ok(result);
    }

    [HttpGet("{id}/quizzes")]
    public async Task<IActionResult> GetQuizzes(int id)
    {
        var course = await _courseService.GetCourseAsync(id);
        if (course == null) return NotFound(new { message = "Course not found" });

        var quizzes = await _context.Quizzes
            .Include(q => q.Document)
            .Where(q => q.CourseId == id)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync();

        var result = quizzes.Select(q => new
        {
            q.Id,
            q.Title,
            q.Description,
            q.TotalQuestions,
            q.TimeLimit,
            q.IsAiGenerated,
            q.CreatedAt,
            DocumentName = q.Document?.OriginalName ?? string.Empty
        });
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Lecturer")]
    public async Task<IActionResult> CreateCourse([FromBody] CreateCourseRequest req)
    {
        var course = new Course
        {
            Code = req.Code,
            Name = req.Name,
            Description = req.Description,
            LecturerId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var created = await _courseService.CreateCourseAsync(course);
        return Ok(new { created.Id, created.Code, created.Name });
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Lecturer")]
    public async Task<IActionResult> UpdateCourse(int id, [FromBody] UpdateCourseRequest req)
    {
        var course = await _courseService.GetCourseAsync(id);
        if (course == null) return NotFound(new { message = "Course not found" });

        course.Name = req.Name;
        course.Description = req.Description;
        course.IsActive = req.IsActive;

        await _courseService.UpdateCourseAsync(course);
        return Ok(new { message = "Course updated successfully" });
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteCourse(int id)
    {
        await _courseService.DeleteAsync(id);
        return Ok(new { message = "Course deleted successfully" });
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q)) return Ok(new List<object>());

        var normalized = q.ToLower();
        var courses = await _context.Courses
            .Include(c => c.Lecturer)
            .Where(c => c.Name.ToLower().Contains(normalized) || c.Code.ToLower().Contains(normalized) || (c.Description ?? "").ToLower().Contains(normalized))
            .OrderBy(c => c.Name)
            .Take(20)
            .ToListAsync();

        var result = courses.Select(c => new
        {
            c.Id,
            c.Code,
            c.Name,
            c.Description,
            LecturerName = c.Lecturer?.FullName ?? "Unknown",
            c.CreatedAt
        });
        return Ok(result);
    }
}
