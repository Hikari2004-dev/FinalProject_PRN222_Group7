using FinalProject_PRN222_Group7.DAL.Data;
using FinalProject_PRN222_Group7.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinalProject_PRN222_Group7.DAL.Repositories;

public class DocumentRepository : GenericRepository<Document>, IDocumentRepository
{
    public DocumentRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<Document>> GetByCourseIdAsync(int courseId)
        => await _dbSet.Where(d => d.CourseId == courseId).Include(d => d.UploadedBy).ToListAsync();

    public async Task<Document?> GetWithChunksAsync(int id)
        => await _dbSet.Include(d => d.Chunks).FirstOrDefaultAsync(d => d.Id == id);
}
