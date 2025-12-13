using JwtAuthApi_Sonnet45.Data;
using JwtAuthApi_Sonnet45.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace JwtAuthApi_Sonnet45.Application.Services;

public class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<TokenService> _logger;
    private readonly ApplicationDbContext _context;
    // private readonly IUnitOfWork _unitOfWork;

    public TokenService(IConfiguration configuration, ILogger<TokenService> logger, ApplicationDbContext context/*, IUnitOfWork unitOfWork*/)
    {
        _configuration = configuration;
        _logger = logger;
        _context = context;
        // _unitOfWork = unitOfWork;
    }

    public async Task<JwtSecurityToken> GenerateAccessToken(User user, IEnumerable<string> roles)
    {
        var roleClaims = new List<Claim>();

        // Add roles to claims
        foreach (var role in roles)
        {
            roleClaims.Add(new Claim(ClaimTypes.Role, role));
        }

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.Username ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("uid", user.Id.ToString())
        }
        .Union(roleClaims);

        var jwtSettings = _configuration.GetSection("JwtSettings");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // Use minutes-based expiration and UTC to avoid timezone drift
        var accessMinutes = int.TryParse(jwtSettings["AccessTokenExpirationMinutes"], out var minutes)
            ? minutes
            : 3;
        var expiresAt = DateTime.UtcNow.AddMinutes(accessMinutes);

        var jwtSecurityToken = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        _logger.LogInformation("Access token generated for user {Username}", user.Username);

        return jwtSecurityToken;
    }

    public string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    public async Task<RefreshToken> CreateRefreshTokenAsync(User user, string ipAddress)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var expirationDays = Convert.ToInt32(jwtSettings["RefreshTokenExpirationDays"]);

        var refreshToken = new RefreshToken
        {
            Token = GenerateRefreshToken(),
            UserId = user.Id,
            User = user,
            ExpiresAt = DateTime.UtcNow.AddDays(expirationDays),
            CreatedAt = DateTime.UtcNow,
            LastDeviceIPAddress = ipAddress,
        };

        return await Task.FromResult(refreshToken);
    }

    public async Task<RefreshToken?> GetActiveRefreshTokenAsync(string token)
    {
        var query = _context.RefreshTokens
            .Include(rt => rt.User)
                .ThenInclude(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
            .Where(rt => rt.Token == token && !rt.IsRevoked && rt.ExpiresAt > DateTime.UtcNow);

        return await query.FirstOrDefaultAsync();
    }

    public async Task RevokeRefreshTokenAsync(string token, string reason = null)
    {
        var refreshToken = await _context.RefreshTokens
            .Include(rt => rt.User) // thêm navigation properties User
                    .ThenInclude(u => u.UserRoles) // và UserRoles
                        .ThenInclude(ur => ur.Role) 
            .FirstOrDefaultAsync(rt => rt.Token == token);
            // .FirstOrDefaultAsync(rt => rt.Token == token && rt.UserId == userId);

        if (refreshToken == null || !refreshToken.IsActive)
            throw new InvalidOperationException("Refresh token is invalid or already revoked");
        
        if (refreshToken.User == null)
            throw new InvalidOperationException($"No user associated, refreshTokenId {refreshToken.Id}");
        
        refreshToken.IsRevoked = true;
        refreshToken.RevokedAt = DateTime.UtcNow;
        refreshToken.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation(@"Refresh token {refreshToken.Token} 
                                revoked for user {refreshToken.User.Id}",
            refreshToken.Token, refreshToken.User.Id, reason ?? "Manual revocation");
    }

    public async Task RevokeAllUserTokensAsync(string userId, string reason = "All tokens revoked")
    {
         var tokens = await _context.RefreshTokens
            .Where(rt => rt.User.Id == userId && rt.IsActive)
            .ToListAsync();

        foreach (var token in tokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;
            // token.RevokedByIp = user.DeviceIPAddress;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("All refresh tokens revoked for user {UserId}", userId);
    }

    public async Task CleanupExpiredTokensAsync()
    {
        var expiredTokens = await _context.RefreshTokens
            .Where(rt => rt.ExpiresAt < DateTime.UtcNow.AddDays(-30))
            .ToListAsync();

        _context.RefreshTokens.RemoveRange(expiredTokens);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Cleaned up {Count} expired refresh tokens", expiredTokens.Count);
    }
}