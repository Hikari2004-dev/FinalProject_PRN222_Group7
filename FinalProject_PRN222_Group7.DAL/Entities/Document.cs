namespace FinalProject_PRN222_Group7.DAL.Entities
{
    public enum DocumentStatus
    {
        Uploaded,
        Processing,
        Indexed,
        Failed
    }

    public class Document
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string OriginalName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public DocumentStatus Status { get; set; } = DocumentStatus.Uploaded;
        public int ChunkCount { get; set; }
        public string? EmbeddingModel { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
        public DateTime? IndexedAt { get; set; }

        // FK
        public int CourseId { get; set; }
        public Course Course { get; set; } = null!;
        public string UploadedById { get; set; } = string.Empty;
        public AppUser UploadedBy { get; set; } = null!;
        public string UploadedByUserEmail { get; set; } = string.Empty;
        public int? ChapterId { get; set; }
        public Chapter? Chapter { get; set; }

        // Navigation
        public ICollection<DocumentChunk> Chunks { get; set; } = new List<DocumentChunk>();
    }

    public class UploadLog
    {
        public int Id { get; set; }
        public string DocumentName { get; set; } = string.Empty;
        public string CourseCode { get; set; } = string.Empty;
        public string CourseName { get; set; } = string.Empty;
        public string UploadedByEmail { get; set; } = string.Empty;
        public string UploadedByFullName { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class DocumentChunk
    {
        public int Id { get; set; }
        public int ChunkIndex { get; set; }
        public string Content { get; set; } = string.Empty;
        public int TokenCount { get; set; }
        public string? EmbeddingVector { get; set; } // JSON serialized float[]
        public int PageNumber { get; set; }

        // FK
        public int DocumentId { get; set; }
        public Document Document { get; set; } = null!;
    }
}
