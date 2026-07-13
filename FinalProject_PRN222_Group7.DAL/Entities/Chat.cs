namespace FinalProject_PRN222_Group7.DAL.Entities
{
    public class ChatSession
    {
        public int Id { get; set; }
        public string Title { get; set; } = "New Chat";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;

        // FK
        public string UserId { get; set; } = string.Empty;
        public AppUser User { get; set; } = null!;
        public int? CourseId { get; set; }
        public Course? Course { get; set; }

        // Navigation
        public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    }

    public enum MessageRole { User, Assistant, System }

    public class ChatMessage
    {
        public int Id { get; set; }
        public MessageRole Role { get; set; }
        public string Content { get; set; } = string.Empty;
        public string? SourceCitations { get; set; } // JSON: [{docName, chunkId, page}]
        public int TokensUsed { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // FK
        public int ChatSessionId { get; set; }
        public ChatSession ChatSession { get; set; } = null!;
    }
}
