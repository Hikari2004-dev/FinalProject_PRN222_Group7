namespace FinalProject_PRN222_Group7.DAL.Entities;

public class Question
{
    public int Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public string OptionA { get; set; } = string.Empty;
    public string OptionB { get; set; } = string.Empty;
    public string OptionC { get; set; } = string.Empty;
    public string OptionD { get; set; } = string.Empty;
    public string CorrectAnswer { get; set; } = string.Empty;
    public string? Explanation { get; set; }
    public int OrderIndex { get; set; }

    public int QuizId { get; set; }
    public Quiz Quiz { get; set; } = null!;
}
