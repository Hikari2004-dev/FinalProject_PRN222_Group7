using FinalProject_PRN222_Group7.BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using FinalProject_PRN222_Group7.DAL.Entities;
using FinalProject_PRN222_Group7.DAL.Data;
using Microsoft.EntityFrameworkCore;

namespace FinalProject_PRN222_Group7.Api;

[ApiController]
[Route("api/quiz")]
[Authorize]
public class QuizApiController : ControllerBase
{
    private readonly IQuizService _quizService;
    private readonly AppDbContext _context;
    private readonly UserManager<AppUser> _userManager;

    public QuizApiController(IQuizService quizService, AppDbContext context, UserManager<AppUser> userManager)
    {
        _quizService = quizService;
        _context = context;
        _userManager = userManager;
    }

    public record SubmitRequest(int AttemptId, Dictionary<int, char> Answers);

    public record StartQuizResponse(int AttemptId, int QuizId, string QuizTitle, int TotalQuestions, int TimeLimit);

    public record QuestionDto(int Id, string Content, string OptionA, string OptionB, string OptionC, string OptionD, char CorrectAnswer, string? Explanation, int OrderIndex);

    public record AttemptDetailDto(int Id, int QuizId, string QuizTitle, int Score, int TotalQuestions, int CorrectAnswers, string? AnswersJson, DateTime? StartedAt, DateTime? CompletedAt, bool IsCompleted);

    public record AttemptSummaryDto(int Id, string QuizTitle, int Score, int TotalQuestions, int CorrectAnswers, DateTime? CompletedAt, bool IsCompleted);

    public record CreateQuizRequest(int CourseId, string Title, string? Description, int? DocumentId, int TimeLimit, List<QuestionInput> Questions);

    public record QuestionInput(string Content, string OptionA, string OptionB, string OptionC, string OptionD, char CorrectAnswer, string? Explanation);

    [HttpPost("submit")]
    public async Task<IActionResult> Submit([FromBody] SubmitRequest req)
    {
        var attempt = await _quizService.SubmitAttemptAsync(req.AttemptId, req.Answers);
        return Ok(new { attemptId = attempt.Id, score = attempt.Score });
    }

    [HttpGet("course/{courseId}")]
    public async Task<IActionResult> GetQuizzesByCourse(int courseId)
    {
        var quizzes = await _quizService.GetByCourseAsync(courseId);
        var result = quizzes.Select(q => new
        {
            q.Id,
            q.Title,
            q.Description,
            q.TotalQuestions,
            q.TimeLimit,
            q.IsAiGenerated,
            q.CreatedAt,
            CourseName = q.Course?.Name ?? string.Empty
        });
        return Ok(result);
    }

    [HttpGet("{id}/detail")]
    public async Task<IActionResult> GetQuizDetail(int id)
    {
        var quiz = await _quizService.GetQuizWithQuestionsAsync(id);
        if (quiz == null) return NotFound(new { message = "Quiz not found" });

        var dto = new
        {
            quiz.Id,
            quiz.Title,
            quiz.Description,
            quiz.TotalQuestions,
            quiz.TimeLimit,
            quiz.IsAiGenerated,
            quiz.CreatedAt,
            CourseId = quiz.CourseId,
            CourseName = quiz.Course?.Name ?? string.Empty,
            DocumentId = quiz.DocumentId,
            Questions = quiz.Questions.Select(q => new QuestionDto(
                q.Id, q.Content, q.OptionA, q.OptionB, q.OptionC, q.OptionD,
                q.CorrectAnswer, q.Explanation, q.OrderIndex))
        };
        return Ok(dto);
    }

    [HttpPost("start")]
    public async Task<IActionResult> StartAttempt([FromBody] StartQuizRequest req)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var quiz = await _quizService.GetQuizWithQuestionsAsync(req.QuizId);
        if (quiz == null) return NotFound(new { message = "Quiz not found" });

        var existingAttempt = await _context.QuizAttempts
            .FirstOrDefaultAsync(a => a.QuizId == req.QuizId && a.UserId == userId && !a.IsCompleted);
        if (existingAttempt != null)
        {
            return Ok(new StartQuizResponse(existingAttempt.Id, quiz.Id, quiz.Title, quiz.TotalQuestions, quiz.TimeLimit));
        }

        var attempt = await _quizService.StartAttemptAsync(req.QuizId, userId);
        return Ok(new StartQuizResponse(attempt.Id, quiz.Id, quiz.Title, quiz.TotalQuestions, quiz.TimeLimit));
    }

    public record StartQuizRequest(int QuizId);

    [HttpGet("attempt/{attemptId}")]
    public async Task<IActionResult> GetAttempt(int attemptId)
    {
        var attempt = await _quizService.GetAttemptAsync(attemptId);
        if (attempt == null) return NotFound(new { message = "Attempt not found" });

        var dto = new AttemptDetailDto(
            attempt.Id,
            attempt.QuizId,
            attempt.Quiz?.Title ?? string.Empty,
            attempt.Score,
            attempt.TotalQuestions,
            attempt.CorrectAnswers,
            attempt.AnswersJson,
            attempt.StartedAt,
            attempt.CompletedAt,
            attempt.IsCompleted
        );
        return Ok(dto);
    }

    [HttpGet("user/attempts")]
    public async Task<IActionResult> GetUserAttempts()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var attempts = await _context.QuizAttempts
            .Include(a => a.Quiz)
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CompletedAt)
            .ToListAsync();

        var result = attempts.Select(a => new AttemptSummaryDto(
            a.Id,
            a.Quiz?.Title ?? "Unknown Quiz",
            a.Score,
            a.TotalQuestions,
            a.CorrectAnswers,
            a.CompletedAt,
            a.IsCompleted
        ));
        return Ok(result);
    }

    [HttpGet("attempt/{attemptId}/review")]
    public async Task<IActionResult> ReviewAttempt(int attemptId)
    {
        var attempt = await _context.QuizAttempts
            .Include(a => a.Quiz).ThenInclude(q => q.Questions)
            .FirstOrDefaultAsync(a => a.Id == attemptId);

        if (attempt == null) return NotFound(new { message = "Attempt not found" });
        if (!attempt.IsCompleted) return BadRequest(new { message = "Attempt not completed yet" });

        var userAnswers = new Dictionary<int, char?>();
        if (!string.IsNullOrEmpty(attempt.AnswersJson))
        {
            try
            {
                userAnswers = System.Text.Json.JsonSerializer.Deserialize<Dictionary<int, char?>>(attempt.AnswersJson) ?? new();
            }
            catch { }
        }

        var questions = attempt.Quiz.Questions.Select(q => new
        {
            q.Id,
            q.Content,
            q.OptionA,
            q.OptionB,
            q.OptionC,
            q.OptionD,
            q.CorrectAnswer,
            q.Explanation,
            q.OrderIndex,
            UserAnswer = userAnswers.ContainsKey(q.Id) ? userAnswers[q.Id] : null,
            IsCorrect = userAnswers.ContainsKey(q.Id) && userAnswers[q.Id] == q.CorrectAnswer
        }).ToList();

        return Ok(new
        {
            attempt.Id,
            attempt.Score,
            attempt.TotalQuestions,
            attempt.CorrectAnswers,
            QuizTitle = attempt.Quiz?.Title ?? string.Empty,
            Questions = questions
        });
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Lecturer")]
    public async Task<IActionResult> CreateQuiz([FromBody] CreateQuizRequest req)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var quiz = new Quiz
        {
            Title = req.Title,
            Description = req.Description,
            CourseId = req.CourseId,
            DocumentId = req.DocumentId,
            TimeLimit = req.TimeLimit,
            TotalQuestions = req.Questions.Count,
            IsAiGenerated = false,
            CreatedAt = DateTime.UtcNow
        };

        var questions = req.Questions.Select((q, idx) => new Question
        {
            Content = q.Content,
            OptionA = q.OptionA,
            OptionB = q.OptionB,
            OptionC = q.OptionC,
            OptionD = q.OptionD,
            CorrectAnswer = q.CorrectAnswer,
            Explanation = q.Explanation,
            OrderIndex = idx
        });

        var created = await _quizService.CreateQuizAsync(quiz, questions);
        return Ok(new { created.Id, created.Title, created.TotalQuestions });
    }

    [HttpGet("user/stats")]
    public async Task<IActionResult> GetUserStats()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var totalAttempts = await _context.QuizAttempts.CountAsync(a => a.UserId == userId);
        var completedAttempts = await _context.QuizAttempts.CountAsync(a => a.UserId == userId && a.IsCompleted);
        var avgScore = await _context.QuizAttempts
            .Where(a => a.UserId == userId && a.IsCompleted)
            .Select(a => a.Score)
            .AverageAsync();

        var bestQuiz = await _context.QuizAttempts
            .Where(a => a.UserId == userId && a.IsCompleted)
            .OrderByDescending(a => a.Score)
            .Include(a => a.Quiz)
            .FirstOrDefaultAsync();

        return Ok(new
        {
            totalAttempts,
            completedAttempts,
            avgScore = Math.Round(avgScore, 2),
            bestScore = bestQuiz?.Score ?? 0,
            bestQuizTitle = bestQuiz?.Quiz?.Title ?? "N/A"
        });
    }
}
