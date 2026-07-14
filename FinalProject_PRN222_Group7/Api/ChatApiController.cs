using FinalProject_PRN222_Group7.BLL.Services;
using FinalProject_PRN222_Group7.DAL.Data;
using FinalProject_PRN222_Group7.DAL.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;

namespace FinalProject_PRN222_Group7.Api
{
    [ApiController]
    [Route("api/chat")]
    [Authorize]
    public class ChatApiController : ControllerBase
    {
        private readonly IChatService _chatService;
        private readonly IDocumentService _docService;
        private readonly IAiUsageGate _aiUsageGate;
        private readonly UserManager<AppUser> _userManager;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ChatApiController> _logger;
        private readonly IConfiguration _configuration;

        public ChatApiController(
            IChatService chatService,
            IDocumentService docService,
            IAiUsageGate aiUsageGate,
            UserManager<AppUser> userManager,
            IHttpClientFactory httpClientFactory,
            ILogger<ChatApiController> logger,
            IConfiguration configuration)
        {
            _chatService = chatService;
            _docService = docService;
            _aiUsageGate = aiUsageGate;
            _userManager = userManager;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _configuration = configuration;
        }

        public record SendRequest(string Question, int? SessionId, int? CourseId);
        public record SendResponse(string Answer, int SessionId, string[] Citations, int TokensUsed);
        private record ChatGenerationResult(string Answer, List<string> Citations, int TokensUsed, string? ModelName);

        private static int _keyIndex = 0;
        private static readonly object _keyLock = new();

        private string GetNextApiKey(List<string> keys)
        {
            if (keys == null || !keys.Any())
            {
                return string.Empty;
            }

            lock (_keyLock)
            {
                if (_keyIndex >= keys.Count)
                {
                    _keyIndex = 0;
                }

                var key = keys[_keyIndex];
                _keyIndex = (_keyIndex + 1) % keys.Count;
                return key;
            }
        }

        [HttpPost("send")]
        public async Task<IActionResult> Send([FromBody] SendRequest req)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            var roles = await _userManager.GetRolesAsync(user);
            var sessionId = req.SessionId ?? (await _chatService.CreateSessionAsync(user.Id, req.CourseId)).Id;

            var history = (await _chatService.GetRecentMessagesAsync(sessionId, 6)).ToList();
            history.Reverse();
            var historyText = string.Join("\n", history.Select(h => $"{(h.Role == MessageRole.User ? "Học sinh" : "AI")}: {h.Content}"));

            await _chatService.AddMessageAsync(sessionId, MessageRole.User, req.Question);

            var usageResult = await _aiUsageGate.ExecuteAsync(
                user.Id,
                roles,
                "chat.ask",
                async () =>
                {
                    var generated = await GenerateAnswerAsync(req, historyText);
                    return new AiUsageExecutionPayload<ChatGenerationResult>(generated, generated.TokensUsed, generated.ModelName);
                },
                $"chat:{sessionId}:{Guid.NewGuid():N}");

            var answer = usageResult.Success && usageResult.Payload != null
                ? usageResult.Payload.Answer
                : $"Đã xảy ra lỗi trong quá trình xử lý câu hỏi: {usageResult.ErrorMessage}. Vui lòng thử lại sau.";
            var citations = usageResult.Success && usageResult.Payload != null
                ? usageResult.Payload.Citations
                : new List<string> { "Hệ thống" };
            var tokensUsed = usageResult.Success && usageResult.Payload != null
                ? usageResult.Payload.TokensUsed
                : 0;

            await _chatService.AddMessageAsync(sessionId, MessageRole.Assistant, answer, string.Join(",", citations), tokensUsed);
            return Ok(new SendResponse(answer, sessionId, citations.ToArray(), tokensUsed));
        }

        private async Task<ChatGenerationResult> GenerateAnswerAsync(SendRequest req, string historyText)
        {
            var dbChunks = new List<DocumentChunk>();
            if (req.CourseId.HasValue)
            {
                dbChunks = (await _docService.GetIndexedChunksByCourseAsync(req.CourseId.Value)).ToList();
            }

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
            var citations = matchedChunks.Select(c => c.Document.OriginalName).Distinct().ToList();
            if (!citations.Any())
            {
                citations.Add("Kiến thức nền tảng hệ thống");
            }

            var geminiSection = _configuration.GetSection("Gemini");
            var apiKeys = geminiSection.GetSection("ApiKeys").Get<List<string>>() ?? new List<string>();
            var model = geminiSection.GetValue<string>("Model") ?? "gemini-3.5-flash";

            if (!apiKeys.Any())
            {
                throw new InvalidOperationException("Chưa cấu hình API Key nào trong appsettings.json.");
            }

            var client = _httpClientFactory.CreateClient();
            var retries = 0;
            var maxRetries = apiKeys.Count;
            var lastErrorMsg = string.Empty;

            while (retries < maxRetries)
            {
                var apiKey = GetNextApiKey(apiKeys);
                if (string.IsNullOrEmpty(apiKey) || apiKey.Contains("YOUR-"))
                {
                    retries++;
                    continue;
                }

                var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
                var prompt = $"Bạn là trợ lý học tập LMS AI chuyên nghiệp, vô cùng thông minh và nhiệt tình.\n" +
                             $"Dưới đây là lịch sử cuộc trò chuyện gần đây:\n---\n{historyText}\n---\n\n" +
                             $"Dưới đây là phần ngữ cảnh trích xuất từ các tài liệu học tập của môn học:\n---\n{contextText}\n---\n\n" +
                             $"Dựa vào ngữ cảnh tài liệu và lịch sử cuộc trò chuyện trên, hãy trả lời câu hỏi sau đây của học sinh một cách tự nhiên, sinh động và có tư duy logic thông minh: \"{req.Question}\"\n\n" +
                             "Lưu ý quan trọng:\n" +
                             "- Nếu ngữ cảnh trên chứa thông tin liên quan đến câu hỏi, hãy ưu tiên giải đáp chính xác dựa trên tài liệu đó.\n" +
                             "- Nếu câu hỏi là các câu nói thông thường, chào hỏi xã giao hoặc không liên quan đến tài liệu học tập, hãy đóng vai một trợ lý chatbot thông minh, đối đáp tự nhiên và hóm hỉnh.\n" +
                             "- Tuyệt đối không nhắc lại toàn bộ câu hỏi của học sinh hoặc các câu chào quá rập khuôn trong câu trả lời nếu không cần thiết.\n" +
                             "- Trả lời bằng ngôn ngữ tự nhiên, định dạng Markdown thân thiện (sử dụng in đậm, danh sách gạch đầu dòng để dễ đọc).";

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
                            var tokensUsed = req.Question.Length / 4 + resultText.Length / 4;
                            return new ChatGenerationResult(resultText, citations, tokensUsed, model);
                        }

                        lastErrorMsg = "Gemini API trả về nội dung rỗng.";
                        retries++;
                        continue;
                    }

                    var errContent = await response.Content.ReadAsStringAsync();
                    lastErrorMsg = $"HTTP {response.StatusCode} - {errContent}";
                    _logger.LogWarning("Gemini API key rotation warning (Key index {Index} failed): {Error}", _keyIndex, lastErrorMsg);
                    retries++;
                }
                catch (Exception ex)
                {
                    lastErrorMsg = ex.Message;
                    retries++;
                }
            }

            throw new InvalidOperationException($"Toàn bộ {maxRetries} API Keys được thử nghiệm đều không thành công. Chi tiết lỗi cuối cùng: {lastErrorMsg}");
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
