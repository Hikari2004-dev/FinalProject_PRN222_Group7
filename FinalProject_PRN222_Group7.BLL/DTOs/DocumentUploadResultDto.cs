using FinalProject_PRN222_Group7.DAL.Enums;

namespace FinalProject_PRN222_Group7.BLL.DTOs;

public sealed class DocumentUploadResultDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DocumentStatus Status { get; set; }
    public int CourseId { get; set; }
    public int UploadedById { get; set; }
    public bool RagTriggerAccepted { get; set; }
}
