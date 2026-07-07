using FinalProject_PRN222_Group7.DAL.Entities;

namespace FinalProject_PRN222_Group7.DAL.Repositories;

public interface ICourseRepository : IGenericRepository<Course>
{
    Task<IEnumerable<Course>> GetCoursesWithDocumentsAsync();
}
