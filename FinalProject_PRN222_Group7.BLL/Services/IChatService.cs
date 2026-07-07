using FinalProject_PRN222_Group7.BLL.DTOs;

namespace FinalProject_PRN222_Group7.BLL.Services;

public interface IChatService
{
    Task<IReadOnlyList<ChatHistoryDto>> GetSessionsForUserAsync(int userId);
    Task<ChatHistoryDto?> GetSessionHistoryAsync(int sessionId, int userId);
}
