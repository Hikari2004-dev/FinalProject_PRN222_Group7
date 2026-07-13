using FinalProject_PRN222_Group7.BLL.Services;
using FinalProject_PRN222_Group7.DAL.Data;
using FinalProject_PRN222_Group7.DAL.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace FinalProject_PRN222_Group7.Api
{
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

        [HttpPost("send")]
        public async Task<IActionResult> Send([FromBody] SendRequest req)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            // Kiểm tra giới hạn câu hỏi (miễn trừ cho Admin & Giảng viên)
            var isAdmin = User.IsInRole("Admin");
            var isLecturer = User.IsInRole("Lecturer");
            if (!isAdmin && !isLecturer)
            {
                var hasLimit = await _chatService.CheckQueryLimitAsync(user.Id);
                if (!hasLimit)
                    return BadRequest(new { error = "Bạn đã hết lượt hỏi trong tháng này. Vui lòng nâng cấp gói dịch vụ." });
            }

            // Lấy hoặc Tạo session chat
            int sessionId;
            if (req.SessionId.HasValue)
            {
                sessionId = req.SessionId.Value;
            }
            else
            {
                var session = await _chatService.CreateSessionAsync(user.Id, req.CourseId);
                sessionId = session.Id;
            }

            // Lấy lịch sử cuộc trò chuyện gần đây trước khi lưu câu hỏi mới làm ngữ cảnh bộ nhớ
            var history = await _context.ChatMessages
                .Where(m => m.ChatSessionId == sessionId)
                .OrderByDescending(m => m.Id)
                .Take(6)
                .ToListAsync();
            history.Reverse();
            var historyText = string.Join("\n", history.Select(h => $"{(h.Role == MessageRole.User ? "Học sinh" : "AI")}: {h.Content}"));

            // Lưu câu hỏi của User vào Database
            await _chatService.AddMessageAsync(sessionId, MessageRole.User, req.Question);

            // ── RAG Logic C# Native & Google Gemini 2.5 ─────────────────────────
            string answer = "";
            List<string> citations = new List<string>();
            int tokensUsed = 0;

            try
            {
                // 1. Lấy tất cả phân mảnh tài liệu của môn học này
                var dbChunks = await _context.DocumentChunks
                    .Include(c => c.Document)
                    .Where(c => c.Document.CourseId == req.CourseId && c.Document.Status == DocumentStatus.Indexed)
                    .ToListAsync();

                // 2. Thuật toán lọc trích xuất ngữ cảnh liên quan (Keyword relevance scoring)
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

                // 3. Cấu hình Gemini API Keys xoay vòng
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

                // Thử xoay vòng các Key cho đến khi thành công hoặc hết sạch Key
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

            // Lưu phản hồi của AI vào Database
            await _chatService.AddMessageAsync(sessionId, MessageRole.Assistant, answer, string.Join(",", citations), tokensUsed);

            // Khấu trừ lượt hỏi (chỉ áp dụng cho Sinh viên)
            if (!isAdmin && !isLecturer)
            {
                await _chatService.DecrementQueryLimitAsync(user.Id);
            }

            return Ok(new SendResponse(answer, sessionId, citations.ToArray(), tokensUsed));
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
}
