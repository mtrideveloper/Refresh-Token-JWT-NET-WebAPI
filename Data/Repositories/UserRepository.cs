using JwtAuthApi_Sonnet45.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JwtAuthApi_Sonnet45.Data.Repositories;

public class UserRepository : GenericRepository<User>, IUserRepository
{
    public UserRepository(ApplicationDbContext context) : base(context) { }

    public async Task<User> GetByUsernameAsync(string username)
    {
        return await _dbSet.FirstOrDefaultAsync(u => u.Username == username);
    }

    public async Task<User> GetByEmailAsync(string email)
    {
        return await _dbSet.FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<User> GetByUsernameOrEmailAsync(string username, string email)
    {
        return await _dbSet.FirstOrDefaultAsync(u => u.Username == username || u.Email == email);
    }

    public async Task<User> GetByUsernameWithRolesAsync(string username)
    {
        return await _dbSet
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Username == username);
    }
}