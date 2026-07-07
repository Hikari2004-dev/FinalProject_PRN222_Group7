using System.Net.Http.Json;
using FinalProject_PRN222_Group7.DAL.Entities;

namespace FinalProject_PRN222_Group7.BLL.Services;

public sealed class RagPipelineClient : IRagPipelineClient
{
    private readonly HttpClient _httpClient;
    private readonly string? _pipelineEndpoint;

    public RagPipelineClient(HttpClient httpClient, string? pipelineEndpoint)
    {
        _httpClient = httpClient;
        _pipelineEndpoint = pipelineEndpoint;
    }

    public async Task<bool> TriggerDocumentProcessingAsync(Document document, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_pipelineEndpoint))
            return false;

        var payload = new
        {
            documentId = document.Id,
            title = document.Title,
            fileName = document.FileName,
            filePath = document.FilePath,
            contentType = document.ContentType,
            courseId = document.CourseId,
            uploadedById = document.UploadedById
        };

        var response = await _httpClient.PostAsJsonAsync(_pipelineEndpoint, payload, cancellationToken);
        return response.IsSuccessStatusCode;
    }
}
