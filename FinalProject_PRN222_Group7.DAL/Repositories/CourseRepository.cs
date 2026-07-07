using FinalProject_PRN222_Group7.DAL.Data;
using FinalProject_PRN222_Group7.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinalProject_PRN222_Group7.DAL.Repositories;

public class CourseRepository : GenericRepository<Course>, ICourseRepository
{
    public CourseRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<Course>> GetCoursesWithDocumentsAsync()
        => await _dbSet.Include(c => c.Documents).ToListAsync();
}
