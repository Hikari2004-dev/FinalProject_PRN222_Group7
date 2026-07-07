using FinalProject_PRN222_Group7.BLL.DTOs;
using FinalProject_PRN222_Group7.DAL.Repositories;

namespace FinalProject_PRN222_Group7.BLL.Services;

public sealed class ChatService : IChatService
{
    private readonly IChatSessionRepository _chatSessionRepository;

    public ChatService(IChatSessionRepository chatSessionRepository)
    {
        _chatSessionRepository = chatSessionRepository;
    }

    public async Task<IReadOnlyList<ChatHistoryDto>> GetSessionsForUserAsync(int userId)
    {
        var sessions = await _chatSessionRepository.GetByUserIdAsync(userId);

        return sessions
            .Select(session => new ChatHistoryDto
            {
                SessionId = session.Id,
                Title = session.Title,
                CourseId = session.CourseId,
                CreatedAt = session.CreatedAt,
                Messages = []
            })
            .ToList();
    }

    public async Task<ChatHistoryDto?> GetSessionHistoryAsync(int sessionId, int userId)
    {
        var session = await _chatSessionRepository.GetWithMessagesAsync(sessionId);
        if (session == null || session.UserId != userId)
            return null;

        return new ChatHistoryDto
        {
            SessionId = session.Id,
            Title = session.Title,
            CourseId = session.CourseId,
            CreatedAt = session.CreatedAt,
            Messages = session.Messages
                .OrderBy(message => message.SentAt)
                .Select(message => new ChatMessageDto
                {
                    Id = message.Id,
                    Content = message.Content,
                    IsFromUser = message.IsFromUser,
                    SourceReferences = message.SourceReferences,
                    SentAt = message.SentAt
                })
                .ToList()
        };
    }
}
