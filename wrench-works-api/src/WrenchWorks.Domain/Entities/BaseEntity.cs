namespace WrenchWorks.Domain.Entities;

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public uint RowVersion { get; set; }
}

public abstract class BusinessScopedEntity : BaseEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = null!;
}
