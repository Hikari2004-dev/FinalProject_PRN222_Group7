using FinalProject_PRN222_Group7.BLL.Services;
using FinalProject_PRN222_Group7.DAL.Data;
using FinalProject_PRN222_Group7.DAL.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinalProject_PRN222_Group7.Api;

[ApiController]
[Route("api/chat")]
[Authorize]
public class ChatApiController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly UserManager<AppUser> _userManager;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ChatApiController> _logger;
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public ChatApiController(
        IChatService chatService,
        UserManager<AppUser> userManager,
        IHttpClientFactory httpClientFactory,
        ILogger<ChatApiController> logger,
        AppDbContext context,
        IConfiguration configuration)
    {
        _chatService = chatService;
        _userManager = userManager;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _context = context;
        _configuration = configuration;
    }

    public record SendRequest(string Question, int? SessionId, int? CourseId);
    public record SendResponse(string Answer, int SessionId, string[] Citations, int TokensUsed);

    public record ChatSessionDto(int Id, string Title, int? CourseId, string? CourseName, DateTime CreatedAt, DateTime UpdatedAt, int MessageCount, bool IsActive);

    public record ChatMessageDto(int Id, int SessionId, string Role, string Content, string? SourceCitations, int TokensUsed, DateTime CreatedAt);

    public record CreateSessionRequest(int? CourseId, string? Title);

    public record UpdateSessionRequest(string Title);

    public record RenameSessionRequest(string Title);

    private static int _keyIndex = 0;
    private static readonly object _keyLock = new object();

    private string GetNextApiKey(List<string> keys)
    {
        if (keys == null || !keys.Any()) return string.Empty;
        lock (_keyLock)
        {
            if (_keyIndex >= keys.Count) _keyIndex = 0;
            var key = keys[_keyIndex];
            _keyIndex = (_keyIndex + 1) % keys.Count;
            return key;
        }
    }

    [HttpGet("sessions")]
    public async Task<IActionResult> GetUserSessions()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var sessions = await _context.ChatSessions
            .Include(s => s.Course)
            .Where(s => s.UserId == user.Id && s.IsActive)
            .OrderByDescending(s => s.UpdatedAt)
            .ToListAsync();

        var result = sessions.Select(s => new ChatSessionDto(
            s.Id,
            s.Title,
            s.CourseId,
            s.Course?.Name,
            s.CreatedAt,
            s.UpdatedAt,
            s.Messages?.Count ?? 0,
            s.IsActive
        ));
        return Ok(result);
    }

    [HttpGet("sessions/{sessionId}")]
    public async Task<IActionResult> GetSession(int sessionId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var session = await _context.ChatSessions
            .Include(s => s.Course)
            .Include(s => s.Messages)
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session == null) return NotFound(new { message = "Session not found" });
        if (session.UserId != user.Id) return Forbid();

        var dto = new ChatSessionDto(
            session.Id,
            session.Title,
            session.CourseId,
            session.Course?.Name,
            session.CreatedAt,
            session.UpdatedAt,
            session.Messages?.Count ?? 0,
            session.IsActive
        );
        return Ok(dto);
    }

    [HttpGet("sessions/{sessionId}/messages")]
    public async Task<IActionResult> GetMessages(int sessionId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var session = await _context.ChatSessions.FirstOrDefaultAsync(s => s.Id == sessionId);
        if (session == null) return NotFound(new { message = "Session not found" });
        if (session.UserId != user.Id) return Forbid();

        var messages = await _context.ChatMessages
            .Where(m => m.ChatSessionId == sessionId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        var result = messages.Select(m => new ChatMessageDto(
            m.Id,
            m.ChatSessionId,
            m.Role.ToString(),
            m.Content,
            m.SourceCitations,
            m.TokensUsed,
            m.CreatedAt
        ));
        return Ok(result);
    }

    [HttpPost("sessions")]
    public async Task<IActionResult> CreateSession([FromBody] CreateSessionRequest req)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var session = await _chatService.CreateSessionAsync(user.Id, req.CourseId);
        if (!string.IsNullOrEmpty(req.Title) && req.Title != "New Chat")
        {
            session.Title = req.Title;
            _context.ChatSessions.Update(session);
            await _context.SaveChangesAsync();
        }

        return Ok(new ChatSessionDto(
            session.Id,
            session.Title,
            session.CourseId,
            null,
            session.CreatedAt,
            session.UpdatedAt,
            0,
            session.IsActive
        ));
    }

    [HttpPut("sessions/{sessionId}")]
    public async Task<IActionResult> UpdateSession(int sessionId, [FromBody] UpdateSessionRequest req)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var session = await _context.ChatSessions.FirstOrDefaultAsync(s => s.Id == sessionId);
        if (session == null) return NotFound(new { message = "Session not found" });
        if (session.UserId != user.Id) return Forbid();

        session.Title = req.Title;
        session.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new ChatSessionDto(
            session.Id,
            session.Title,
            session.CourseId,
            session.Course?.Name,
            session.CreatedAt,
            session.UpdatedAt,
            session.Messages?.Count ?? 0,
            session.IsActive
        ));
    }

    [HttpDelete("sessions/{sessionId}")]
    public async Task<IActionResult> DeleteSession(int sessionId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var session = await _context.ChatSessions.FirstOrDefaultAsync(s => s.Id == sessionId);
        if (session == null) return NotFound(new { message = "Session not found" });
        if (session.UserId != user.Id) return Forbid();

        session.IsActive = false;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Session deleted successfully" });
    }

    [HttpPost("send")]
    public async Task<IActionResult> Send([FromBody] SendRequest req)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var isAdmin = User.IsInRole("Admin");
        var isLecturer = User.IsInRole("Lecturer");
        if (!isAdmin && !isLecturer)
        {
            var hasLimit = await _chatService.CheckQueryLimitAsync(user.Id);
            if (!hasLimit)
                return BadRequest(new { error = "Bạn đã hết lượt hỏi trong tháng này. Vui lòng nâng cấp gói dịch vụ." });
        }

        int sessionId;
        if (req.SessionId.HasValue)
        {
            sessionId = req.SessionId.Value;
            var session = await _context.ChatSessions.FirstOrDefaultAsync(s => s.Id == sessionId);
            if (session == null) return NotFound(new { message = "Session not found" });
        }
        else
        {
            var session = await _chatService.CreateSessionAsync(user.Id, req.CourseId);
            sessionId = session.Id;
        }

        var history = await _context.ChatMessages
            .Where(m => m.ChatSessionId == sessionId)
            .OrderByDescending(m => m.Id)
            .Take(6)
            .ToListAsync();
        history.Reverse();
        var historyText = string.Join("\n", history.Select(h => $"{(h.Role == MessageRole.User ? "Học sinh" : "AI")}: {h.Content}"));

        await _chatService.AddMessageAsync(sessionId, MessageRole.User, req.Question);

        string answer = "";
        List<string> citations = new List<string>();
        int tokensUsed = 0;

        try
        {
            var dbChunks = await _context.DocumentChunks
                .Include(c => c.Document)
                .Where(c => c.Document.CourseId == req.CourseId && c.Document.Status == DocumentStatus.Indexed)
                .ToListAsync();

            var keywords = req.Question.ToLower()
                .Split(new[] { ' ', '?', ',', '.', '!', '-', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 2)
                .Distinct()
                .ToList();

            var matchedChunks = dbChunks.Select(c => new
            {
                Chunk = c,
                Score = keywords.Count(k => c.Content.ToLower().Contains(k))
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(4)
            .Select(x => x.Chunk)
            .ToList();

            if (!matchedChunks.Any())
            {
                matchedChunks = dbChunks.Take(2).ToList();
            }

            var contextText = string.Join("\n\n", matchedChunks.Select(c => $"[Tài liệu: {c.Document.OriginalName}]: {c.Content}"));
            citations = matchedChunks.Select(c => c.Document.OriginalName).Distinct().ToList();

            if (!citations.Any())
            {
                citations.Add("Kiến thức nền tảng hệ thống");
            }

            var geminiSection = _configuration.GetSection("Gemini");
            var apiKeys = geminiSection.GetSection("ApiKeys").Get<List<string>>() ?? new List<string>();
            var model = geminiSection.GetValue<string>("Model") ?? "gemini-2.5-flash";

            if (!apiKeys.Any())
            {
                throw new Exception("Chưa cấu hình API Key nào trong appsettings.json.");
            }

            var client = _httpClientFactory.CreateClient();
            bool callSuccess = false;
            int retries = 0;
            int maxRetries = apiKeys.Count;
            string lastErrorMsg = "";

            while (!callSuccess && retries < maxRetries)
            {
                var apiKey = GetNextApiKey(apiKeys);
                if (string.IsNullOrEmpty(apiKey) || apiKey.Contains("YOUR-"))
                {
                    retries++;
                    continue;
                }

                var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

                var prompt = $"Bạn là trợ lý học tập LMS AI chuyên nghiệp, vô cùng thông minh và nhiệt tình.\n" +
                             $"Dưới đây là lịch sử cuộc trò chuyện gần đây:\n" +
                             $"---\n{historyText}\n---\n\n" +
                             $"Dưới đây là phần ngữ cảnh trích xuất từ các tài liệu học tập của môn học:\n" +
                             $"---\n{contextText}\n---\n\n" +
                             $"Dựa vào ngữ cảnh tài liệu và lịch sử cuộc trò chuyện trên, hãy trả lời câu hỏi sau đây của học sinh một cách tự nhiên, sinh động và có tư duy logic thông minh: \"{req.Question}\"\n\n" +
                             $"Lưu ý quan trọng:\n" +
                             $"- Nếu ngữ cảnh trên chứa thông tin liên quan đến câu hỏi, hãy ưu tiên giải đáp chính xác dựa trên tài liệu đó.\n" +
                             $"- Nếu câu hỏi là các câu nói thông thường, chào hỏi xã giao hoặc không liên quan đến tài liệu học tập, hãy đóng vai một trợ lý chatbot thông minh, đối đáp tự nhiên và hóm hỉnh.\n" +
                             $"- Tuyệt đối không nhắc lại toàn bộ câu hỏi của học sinh hoặc các câu chào quá rập khuôn trong câu trả lời nếu không cần thiết.\n" +
                             $"- Trả lời bằng ngôn ngữ tự nhiên, định dạng Markdown thân thiện (sử dụng in đậm, danh sách gạch đầu dòng để dễ đọc).";

                var payload = new
                {
                    contents = new[]
                    {
                        new { parts = new[] { new { text = prompt } } }
                    }
                };

                try
                {
                    var response = await client.PostAsJsonAsync(url, payload);
                    if (response.IsSuccessStatusCode)
                    {
                        var geminiResult = await response.Content.ReadFromJsonAsync<GeminiGenerateResponse>();
                        var resultText = geminiResult?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
                        if (!string.IsNullOrEmpty(resultText))
                        {
                            answer = resultText;
                            tokensUsed = req.Question.Length / 4 + answer.Length / 4;
                            callSuccess = true;
                        }
                        else
                        {
                            lastErrorMsg = "Gemini API trả về nội dung rỗng.";
                            retries++;
                        }
                    }
                    else
                    {
                        var errContent = await response.Content.ReadAsStringAsync();
                        lastErrorMsg = $"HTTP {response.StatusCode} - {errContent}";
                        _logger.LogWarning("Gemini API key rotation warning (Key index {Index} failed): {Error}", _keyIndex, lastErrorMsg);
                        retries++;
                    }
                }
                catch (Exception ex)
                {
                    lastErrorMsg = ex.Message;
                    retries++;
                }
            }

            if (!callSuccess)
            {
                answer = $"[Lỗi Gemini API]: Toàn bộ {maxRetries} API Keys được thử nghiệm đều không thành công. Chi tiết lỗi cuối cùng: {lastErrorMsg}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi xử lý RAG Chatbot");
            answer = $"Đã xảy ra lỗi trong quá trình xử lý câu hỏi: {ex.Message}. Vui lòng thử lại sau.";
        }

        await _chatService.AddMessageAsync(sessionId, MessageRole.Assistant, answer, string.Join(",", citations), tokensUsed);

        if (!isAdmin && !isLecturer)
        {
            await _chatService.DecrementQueryLimitAsync(user.Id);
        }

        return Ok(new SendResponse(answer, sessionId, citations.ToArray(), tokensUsed));
    }

    [HttpGet("courses/{courseId}/sessions")]
    public async Task<IActionResult> GetCourseSessions(int courseId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var sessions = await _context.ChatSessions
            .Include(s => s.Course)
            .Where(s => s.UserId == user.Id && s.CourseId == courseId && s.IsActive)
            .OrderByDescending(s => s.UpdatedAt)
            .ToListAsync();

        var result = sessions.Select(s => new ChatSessionDto(
            s.Id,
            s.Title,
            s.CourseId,
            s.Course?.Name,
            s.CreatedAt,
            s.UpdatedAt,
            s.Messages?.Count ?? 0,
            s.IsActive
        ));
        return Ok(result);
    }

    [HttpGet("stats/usage")]
    public async Task<IActionResult> GetUsageStats()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var totalSessions = await _context.ChatSessions.CountAsync(s => s.UserId == user.Id && s.IsActive);
        var totalMessages = await _context.ChatMessages.CountAsync(m => m.ChatSession.UserId == user.Id);
        var totalTokens = await _context.ChatMessages
            .Where(m => m.ChatSession.UserId == user.Id)
            .SumAsync(m => m.TokensUsed);

        var recentSessions = await _context.ChatSessions
            .Where(s => s.UserId == user.Id && s.IsActive)
            .OrderByDescending(s => s.UpdatedAt)
            .Take(5)
            .Select(s => new { s.Id, s.Title, s.UpdatedAt })
            .ToListAsync();

        return Ok(new
        {
            totalSessions,
            totalMessages,
            totalTokens,
            recentSessions
        });
    }

    private class GeminiGenerateResponse
    {
        public List<Candidate>? Candidates { get; set; }
    }
    private class Candidate
    {
        public ContentObj? Content { get; set; }
    }
    private class ContentObj
    {
        public List<PartObj>? Parts { get; set; }
    }
    private class PartObj
    {
        public string? Text { get; set; }
    }
}
