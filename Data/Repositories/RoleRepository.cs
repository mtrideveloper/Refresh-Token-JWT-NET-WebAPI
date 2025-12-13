using JwtAuthApi_Sonnet45.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JwtAuthApi_Sonnet45.Data.Repositories;

public class RoleRepository : GenericRepository<Role>, IRoleRepository
{
    public RoleRepository(ApplicationDbContext context) : base(context) { }

    public async Task<Role> GetByNameAsync(string name)
    {
        return await _dbSet.FirstOrDefaultAsync(r => r.Name == name);
    }
}