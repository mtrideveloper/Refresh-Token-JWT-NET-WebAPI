namespace JwtAuthApi_Sonnet45.Application.DTOs;

public class UserDto
{
    public string Id { get; set; }
    public string Username { get; set; }
    public string Email { get; set; }
    public bool IsActive { get; set; }
    public bool EmailConfirmed { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public IEnumerable<string> Roles { get; set; }
}