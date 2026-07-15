using FinalProject_PRN222_Group7.BLL.Services;
using FinalProject_PRN222_Group7.DAL.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FinalProject_PRN222_Group7.Api
{
    [ApiController]
    [Route("api/chat")]
    [Authorize]
    public class ChatApiController : ControllerBase
    {
        private readonly IChatService _chatService;
        private readonly IAiUsageGate _aiUsageGate;
        private readonly UserManager<AppUser> _userManager;
        private readonly ILogger<ChatApiController> _logger;

        public ChatApiController(
            IChatService chatService,
            IAiUsageGate aiUsageGate,
            UserManager<AppUser> userManager,
            ILogger<ChatApiController> logger)
        {
            _chatService = chatService;
            _aiUsageGate = aiUsageGate;
            _userManager = userManager;
            _logger = logger;
        }

        public record SendRequest(string Question, int? SessionId, int? CourseId);
        public record SendResponse(string Answer, int SessionId, string[] Citations, int TokensUsed);

        [HttpPost("send")]
        public async Task<IActionResult> Send([FromBody] SendRequest req)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var roles = await _userManager.GetRolesAsync(user);
            
            int sessionId;
            if (req.SessionId.HasValue)
            {
                sessionId = req.SessionId.Value;
                var session = await _chatService.GetSessionAsync(sessionId);
                if (session == null) return NotFound("Phiên chat không tồn tại.");
                if (!User.IsInRole("Admin") && session.UserId != user.Id)
                {
                    return StatusCode(403, "Bạn không có quyền gửi tin nhắn vào phiên chat này.");
                }
            }
            else
            {
                sessionId = (await _chatService.CreateSessionAsync(user.Id, req.CourseId)).Id;
            }

            var history = (await _chatService.GetRecentMessagesAsync(sessionId, 6)).ToList();
            history.Reverse();
            var historyText = string.Join("\n", history.Select(h =>
                $"{(h.Role == MessageRole.User ? "Học sinh" : "AI")}: {h.Content}"));

            await _chatService.AddMessageAsync(sessionId, MessageRole.User, req.Question);

            var usageResult = await _aiUsageGate.ExecuteAsync(
                user.Id,
                roles,
                "chat.ask",
                async () =>
                {
                    var generated = await _chatService.GenerateAnswerAsync(req.Question, req.CourseId, historyText);
                    return new AiUsageExecutionPayload<ChatAnswerResult>(generated, generated.TokensUsed, generated.ModelName);
                },
                $"chat:{sessionId}:{Guid.NewGuid():N}");

            var answer = usageResult.Success && usageResult.Payload != null
                ? usageResult.Payload.Answer
                : $"Đã xảy ra lỗi: {usageResult.ErrorMessage}. Vui lòng thử lại.";

            var citations = usageResult.Success && usageResult.Payload != null
                ? usageResult.Payload.Citations
                : new List<string> { "Hệ thống" };

            var tokensUsed = usageResult.Success && usageResult.Payload != null
                ? usageResult.Payload.TokensUsed : 0;

            await _chatService.AddMessageAsync(sessionId, MessageRole.Assistant, answer, string.Join(",", citations), tokensUsed);
            return Ok(new SendResponse(answer, sessionId, citations.ToArray(), tokensUsed));
        }
    }
}
