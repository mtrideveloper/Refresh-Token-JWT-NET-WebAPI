using JwtAuthApi_Sonnet45.Domain.Entities;

namespace JwtAuthApi_Sonnet45.Data.Repositories;

public interface IRoleRepository : IGenericRepository<Role>
{
    Task<Role> GetByNameAsync(string name);
}