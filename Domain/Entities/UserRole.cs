namespace JwtAuthApi_Sonnet45.Domain.Entities;

public class UserRole
{
    public string UserId { get; set; }
    public User User { get; set; }

    public string RoleId { get; set; }
    public Role Role { get; set; }

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}