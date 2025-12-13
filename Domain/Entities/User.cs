using System.ComponentModel.DataAnnotations;

namespace JwtAuthApi_Sonnet45.Domain.Entities;

public class User : BaseEntity
{
    // [Required]
    // public string UniqueId { get; set; }

    [Required]
    [MaxLength(50)]
    public string Username { get; set; }

    [Required]
    public string PasswordHash { get; set; }

    [Required]
    [EmailAddress]
    [MaxLength(100)]
    public string Email { get; set; }

    public bool IsActive { get; set; } = true;
    public bool EmailConfirmed { get; set; } = false;
    public int FailedLoginAttempts { get; set; } = 0;
    public DateTime? LockoutEnd { get; set; }

    // Navigation properties
    public ICollection<UserRole> UserRoles { get; set; } = [];

    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];

    // public User()
    // {
    //     string randomIndexStr = new Random().Next(0, 9999).ToString("D5");
    //     // UniqueId = $"{IDGenerator.GenerateUniqueId(GetType())}_{randomIndexStr}";
    // }
}