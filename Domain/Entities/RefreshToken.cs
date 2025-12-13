using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JwtAuthApi_Sonnet45.Domain.Entities;

public class RefreshToken : BaseEntity
{
    [Required]
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; } = false;
    public DateTime? RevokedAt { get; set; }
    // public string? RevokedByIp { get; set; }
    public string? ReplacedByToken { get; set; }
    public string LastDeviceIPAddress { get; set; } = "Unknown IP";

    [Required]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    #region 2 properties Không map vào DB, chỉ để sử dụng trong logic
    public bool IsExpired => ExpiresAt < DateTime.UtcNow;
    public bool IsActive => !IsRevoked && !IsExpired;
    #endregion
}