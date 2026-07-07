using FinalProject_PRN222_Group7.DAL.Enums;

namespace FinalProject_PRN222_Group7.DAL.Entities;

public class Document
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DocumentStatus Status { get; set; } = DocumentStatus.Pending;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;

    public int UploadedById { get; set; }
    public User UploadedBy { get; set; } = null!;

    public ICollection<DocumentChunk> Chunks { get; set; } = new List<DocumentChunk>();
}
