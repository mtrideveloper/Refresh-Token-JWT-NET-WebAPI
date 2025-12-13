using JwtAuthApi_Sonnet45.Domain.Entities;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace JwtAuthApi_Sonnet45.Application.Services;

public interface ITokenService
{
    Task<JwtSecurityToken> GenerateAccessToken(User user, IEnumerable<string> roles);
    string GenerateRefreshToken();
    Task<RefreshToken> CreateRefreshTokenAsync(User user, string ipAddress);
    Task<RefreshToken?> GetActiveRefreshTokenAsync(string token);
    Task RevokeRefreshTokenAsync(string token, string reason = "Token revoked");
    Task RevokeAllUserTokensAsync(string userId, string reason = "All tokens revoked");
    Task CleanupExpiredTokensAsync();
}