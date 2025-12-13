using System.ComponentModel.DataAnnotations;

namespace JwtAuthApi_Sonnet45.Domain.Entities;

public class Role : BaseEntity
{
    [Required]
    [MaxLength(50)]
    public string Name { get; set; }

    [MaxLength(200)]
    public string Description { get; set; }

    // Navigation property
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}