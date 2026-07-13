using FinalProject_PRN222_Group7.BLL.Services;
using FinalProject_PRN222_Group7.DAL.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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

        public ChatApiController(IChatService chatService, UserManager<AppUser> userManager)
        {
            _chatService = chatService;
            _userManager = userManager;
        }

        public record SendRequest(string Question, int? SessionId, int? CourseId);

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

            var response = await _chatService.ProcessChatQuestionAsync(user.Id, req.Question, req.SessionId, req.CourseId, isAdmin || isLecturer);
            
            return Ok(new { 
                answer = response.Answer, 
                sessionId = response.SessionId, 
                citations = response.Citations, 
                tokensUsed = response.TokensUsed 
            });
        }
    }
}
