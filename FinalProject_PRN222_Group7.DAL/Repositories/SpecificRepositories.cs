using FinalProject_PRN222_Group7.DAL.Data;
using FinalProject_PRN222_Group7.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinalProject_PRN222_Group7.DAL.Repositories
{
    public interface IDocumentRepository : IGenericRepository<Document>
    {
        Task<IEnumerable<Document>> GetByCourseAsync(int courseId);
        Task<Document?> GetWithChunksAsync(int id);
    }

    public class DocumentRepository : GenericRepository<Document>, IDocumentRepository
    {
        public DocumentRepository(AppDbContext context) : base(context) { }

        public async Task<IEnumerable<Document>> GetByCourseAsync(int courseId)
            => await _context.Documents
                .Where(d => d.CourseId == courseId)
                .Include(d => d.UploadedBy)
                .OrderByDescending(d => d.UploadedAt)
                .ToListAsync();

        public async Task<Document?> GetWithChunksAsync(int id)
            => await _context.Documents
                .Include(d => d.Chunks)
                .Include(d => d.Course)
                .Include(d => d.UploadedBy)
                .FirstOrDefaultAsync(d => d.Id == id);
    }

    public interface IChatRepository : IGenericRepository<ChatSession>
    {
        Task<IEnumerable<ChatSession>> GetUserSessionsAsync(string userId);
        Task<ChatSession?> GetSessionWithMessagesAsync(int sessionId);
    }

    public class ChatRepository : GenericRepository<ChatSession>, IChatRepository
    {
        public ChatRepository(AppDbContext context) : base(context) { }

        public async Task<IEnumerable<ChatSession>> GetUserSessionsAsync(string userId)
            => await _context.ChatSessions
                .Where(s => s.UserId == userId && s.IsActive)
                .OrderByDescending(s => s.UpdatedAt)
                .Take(50)
                .ToListAsync();

        public async Task<ChatSession?> GetSessionWithMessagesAsync(int sessionId)
            => await _context.ChatSessions
                .Include(s => s.Messages.OrderBy(m => m.CreatedAt))
                .Include(s => s.Course)
                .FirstOrDefaultAsync(s => s.Id == sessionId);
    }

    public interface IQuizRepository : IGenericRepository<Quiz>
    {
        Task<IEnumerable<Quiz>> GetByCourseAsync(int courseId);
        Task<Quiz?> GetWithQuestionsAsync(int id);
        Task<QuizAttempt?> GetUserAttemptAsync(int quizId, string userId);
    }

    public class QuizRepository : GenericRepository<Quiz>, IQuizRepository
    {
        public QuizRepository(AppDbContext context) : base(context) { }

        public async Task<IEnumerable<Quiz>> GetByCourseAsync(int courseId)
            => await _context.Quizzes
                .Where(q => q.CourseId == courseId)
                .Include(q => q.Document)
                .OrderByDescending(q => q.CreatedAt)
                .ToListAsync();

        public async Task<Quiz?> GetWithQuestionsAsync(int id)
            => await _context.Quizzes
                .Include(q => q.Questions.OrderBy(qq => qq.OrderIndex))
                .Include(q => q.Course)
                .Include(q => q.Document)
                .FirstOrDefaultAsync(q => q.Id == id);

        public async Task<QuizAttempt?> GetUserAttemptAsync(int quizId, string userId)
            => await _context.QuizAttempts
                .Where(a => a.QuizId == quizId && a.UserId == userId && a.IsCompleted)
                .OrderByDescending(a => a.CompletedAt)
                .FirstOrDefaultAsync();
    }

    public interface IPaymentRepository : IGenericRepository<Payment>
    {
        Task<IEnumerable<Payment>> GetUserPaymentsAsync(string userId);
        Task<IEnumerable<Payment>> GetAllWithUsersAsync();
    }

    public class PaymentRepository : GenericRepository<Payment>, IPaymentRepository
    {
        public PaymentRepository(AppDbContext context) : base(context) { }

        public async Task<IEnumerable<Payment>> GetUserPaymentsAsync(string userId)
            => await _context.Payments
                .Where(p => p.UserId == userId)
                .Include(p => p.Package)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

        public async Task<IEnumerable<Payment>> GetAllWithUsersAsync()
            => await _context.Payments
                .Include(p => p.User)
                .Include(p => p.Package)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
    }
}
