namespace FinalProject_PRN222_Group7.DAL.Entities;

public class ChatMessage
{
    public int Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsFromUser { get; set; }
    public string? SourceReferences { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    public int ChatSessionId { get; set; }
    public ChatSession ChatSession { get; set; } = null!;
}
