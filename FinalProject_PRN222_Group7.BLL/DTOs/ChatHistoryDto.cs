namespace FinalProject_PRN222_Group7.BLL.DTOs;

public sealed class ChatHistoryDto
{
    public int SessionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int CourseId { get; set; }
    public DateTime CreatedAt { get; set; }
    public IReadOnlyList<ChatMessageDto> Messages { get; set; } = [];
}

public sealed class ChatMessageDto
{
    public int Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsFromUser { get; set; }
    public string? SourceReferences { get; set; }
    public DateTime SentAt { get; set; }
}
