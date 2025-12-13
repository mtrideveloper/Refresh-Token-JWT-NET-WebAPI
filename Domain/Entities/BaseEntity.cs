using JwtAuthApi_Sonnet45.Utils;

namespace JwtAuthApi_Sonnet45.Domain.Entities;

public abstract class BaseEntity
{
    public string Id { get; private set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }

    protected BaseEntity()
    {
        Id = IDGenerator.GenerateUniqueId(GetType());
    }
}
