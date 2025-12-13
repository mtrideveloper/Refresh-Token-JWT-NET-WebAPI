using JwtAuthApi_Sonnet45.Data.Repositories;
using JwtAuthApi_Sonnet45.Domain.Entities;

namespace JwtAuthApi_Sonnet45.Data;

public interface IUnitOfWork : IDisposable
{
    IUserRepository Users { get; }
    IRoleRepository Roles { get; }
    IGenericRepository<RefreshToken> RefreshTokens { get; }
    Task<int> CompleteAsync();
}