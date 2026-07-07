using FinalProject_PRN222_Group7.BLL.DTOs;
using FinalProject_PRN222_Group7.DAL.Entities;
using FinalProject_PRN222_Group7.DAL.Enums;
using FinalProject_PRN222_Group7.DAL.Repositories;

namespace FinalProject_PRN222_Group7.BLL.Services;

public sealed class DocumentService : IDocumentService
{
    private readonly IDocumentRepository _documentRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly IUserRepository _userRepository;
    private readonly IRagPipelineClient _ragPipelineClient;

    public DocumentService(
        IDocumentRepository documentRepository,
        ICourseRepository courseRepository,
        IUserRepository userRepository,
        IRagPipelineClient ragPipelineClient)
    {
        _documentRepository = documentRepository;
        _courseRepository = courseRepository;
        _userRepository = userRepository;
        _ragPipelineClient = ragPipelineClient;
    }

    public async Task<DocumentUploadResultDto> UploadAsync(
        string title,
        string fileName,
        string filePath,
        string contentType,
        long fileSize,
        int courseId,
        int uploadedById,
        CancellationToken cancellationToken = default)
    {
        if (await _courseRepository.GetByIdAsync(courseId) is null)
            throw new InvalidOperationException("Course not found.");

        if (await _userRepository.GetByIdAsync(uploadedById) is null)
            throw new InvalidOperationException("Uploader not found.");

        var document = new Document
        {
            Title = title,
            FileName = fileName,
            FilePath = filePath,
            ContentType = contentType,
            FileSize = fileSize,
            Status = DocumentStatus.Pending,
            CourseId = courseId,
            UploadedById = uploadedById
        };

        await _documentRepository.AddAsync(document);
        await _documentRepository.SaveChangesAsync();

        var triggerAccepted = false;
        try
        {
            triggerAccepted = await _ragPipelineClient.TriggerDocumentProcessingAsync(document, cancellationToken);
        }
        catch (HttpRequestException)
        {
            triggerAccepted = false;
        }
        catch (TaskCanceledException)
        {
            triggerAccepted = false;
        }

        document.Status = triggerAccepted ? DocumentStatus.Processing : DocumentStatus.Pending;
        _documentRepository.Update(document);
        await _documentRepository.SaveChangesAsync();

        return new DocumentUploadResultDto
        {
            Id = document.Id,
            Title = document.Title,
            FileName = document.FileName,
            FilePath = document.FilePath,
            ContentType = document.ContentType,
            FileSize = document.FileSize,
            Status = document.Status,
            CourseId = document.CourseId,
            UploadedById = document.UploadedById,
            RagTriggerAccepted = triggerAccepted
        };
    }
}
