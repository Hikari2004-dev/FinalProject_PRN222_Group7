using FinalProject_PRN222_Group7.DAL.Entities;

namespace FinalProject_PRN222_Group7.DAL.Repositories;

public interface IChatSessionRepository : IGenericRepository<ChatSession>
{
    Task<IEnumerable<ChatSession>> GetByUserIdAsync(int userId);
    Task<ChatSession?> GetWithMessagesAsync(int id);
}
