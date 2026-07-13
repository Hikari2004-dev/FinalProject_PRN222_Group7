using FinalProject_PRN222_Group7.BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinalProject_PRN222_Group7.Api
{
    [ApiController]
    [Route("api/quiz")]
    [Authorize]
    public class QuizApiController : ControllerBase
    {
        private readonly IQuizService _quizService;
        public QuizApiController(IQuizService quizService) => _quizService = quizService;

        public record SubmitRequest(int AttemptId, Dictionary<int, char> Answers);

        [HttpPost("submit")]
        public async Task<IActionResult> Submit([FromBody] SubmitRequest req)
        {
            var attempt = await _quizService.SubmitAttemptAsync(req.AttemptId, req.Answers);
            return Ok(new { attemptId = attempt.Id, score = attempt.Score });
        }
    }
}
