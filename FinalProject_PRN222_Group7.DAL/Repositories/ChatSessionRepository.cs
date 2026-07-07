using FinalProject_PRN222_Group7.DAL.Data;
using FinalProject_PRN222_Group7.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinalProject_PRN222_Group7.DAL.Repositories;

public class ChatSessionRepository : GenericRepository<ChatSession>, IChatSessionRepository
{
    public ChatSessionRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<ChatSession>> GetByUserIdAsync(int userId)
        => await _dbSet.Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

    public async Task<ChatSession?> GetWithMessagesAsync(int id)
        => await _dbSet.Include(s => s.Messages.OrderBy(m => m.SentAt))
            .FirstOrDefaultAsync(s => s.Id == id);
}
