using JwtAuthApi_Sonnet45.Application.DTOs.Auth;
using JwtAuthApi_Sonnet45.Domain.Entities;

namespace JwtAuthApi_Sonnet45.Application.Services;

public interface IAuthService
{
    Task<TokenResponse> RegisterAsync(RegisterRequest request, string userAgent, string ipAddress);
    Task<TokenResponse> LoginAsync(LoginRequest request, string userAgent, string ipAddress);
    Task<TokenResponse> GenerateTokenResponseAsync(User user, string ipAddress);
    Task<TokenResponse> RefreshTokenAsync(string refreshToken, string ipAddress);
    Task RevokeTokenAsync(string refreshToken);
    Task<bool> ValidateCredentialsAsync(string username, string password);
}