using FinalProject_PRN222_Group7.BLL.DTOs;
using FinalProject_PRN222_Group7.DAL.Entities;

namespace FinalProject_PRN222_Group7.BLL.Services;

public interface IAuthService
{
    Task<User?> LoginAsync(LoginDto dto);
    Task<(bool Success, string Error)> RegisterAsync(RegisterDto dto);
}
