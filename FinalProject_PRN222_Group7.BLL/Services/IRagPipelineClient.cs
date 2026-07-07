using FinalProject_PRN222_Group7.DAL.Entities;

namespace FinalProject_PRN222_Group7.BLL.Services;

public interface IRagPipelineClient
{
    Task<bool> TriggerDocumentProcessingAsync(Document document, CancellationToken cancellationToken = default);
}
