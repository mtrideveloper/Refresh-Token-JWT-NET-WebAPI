using System.ComponentModel.DataAnnotations;

namespace JwtAuthApi_Sonnet45.Application.DTOs.Auth;

public class RefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; set; }
}
