using FinalProject_PRN222_Group7.BLL.DTOs;

namespace FinalProject_PRN222_Group7.BLL.Services;

public interface IDocumentService
{
    Task<DocumentUploadResultDto> UploadAsync(
        string title,
        string fileName,
        string filePath,
        string contentType,
        long fileSize,
        int courseId,
        int uploadedById,
        CancellationToken cancellationToken = default);
}
