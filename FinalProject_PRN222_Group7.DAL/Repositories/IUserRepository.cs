using FinalProject_PRN222_Group7.DAL.Entities;

namespace FinalProject_PRN222_Group7.DAL.Repositories;

public interface IUserRepository : IGenericRepository<User>
{
    Task<User?> GetByEmailAsync(string email);
    Task<bool> EmailExistsAsync(string email);
}
