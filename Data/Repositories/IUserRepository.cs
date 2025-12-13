using JwtAuthApi_Sonnet45.Domain.Entities;

namespace JwtAuthApi_Sonnet45.Data.Repositories;

public interface IUserRepository : IGenericRepository<User>
{
    Task<User> GetByUsernameAsync(string username);
    Task<User> GetByEmailAsync(string email);
    Task<User> GetByUsernameOrEmailAsync(string username, string email);
    Task<User> GetByUsernameWithRolesAsync(string username);
}