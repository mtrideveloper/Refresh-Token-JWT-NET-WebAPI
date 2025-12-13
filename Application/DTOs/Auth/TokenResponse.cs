using JwtAuthApi_Sonnet45.Domain.Entities;

namespace JwtAuthApi_Sonnet45.Application.DTOs.Auth;

public class TokenResponse
{
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string TokenType { get; set; } = "Bearer";
    // public UserDto User { get; set; }
    public UserDto User { get; set; }
}