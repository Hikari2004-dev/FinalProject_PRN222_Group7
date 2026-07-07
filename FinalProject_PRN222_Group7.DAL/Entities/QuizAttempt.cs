namespace FinalProject_PRN222_Group7.DAL.Entities;

public class QuizAttempt
{
    public int Id { get; set; }
    public int Score { get; set; }
    public int TotalQuestions { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? Answers { get; set; }

    public int QuizId { get; set; }
    public Quiz Quiz { get; set; } = null!;

    public int UserId { get; set; }
    public User User { get; set; } = null!;
}
