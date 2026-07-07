using FinalProject_PRN222_Group7.DAL.Entities;

namespace FinalProject_PRN222_Group7.DAL.Repositories;

public interface IDocumentRepository : IGenericRepository<Document>
{
    Task<IEnumerable<Document>> GetByCourseIdAsync(int courseId);
    Task<Document?> GetWithChunksAsync(int id);
}
