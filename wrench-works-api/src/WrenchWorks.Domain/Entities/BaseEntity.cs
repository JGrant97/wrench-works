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

/// <summary>
/// A record that can be retired without being destroyed.
///
/// A workshop's value is its service history, so deleting a customer who has jobs would
/// throw away the thing the product exists to keep. Delete therefore removes a row only
/// when nothing references it; anything with history is archived instead — hidden from
/// lists and pickers, still resolvable by id so old jobs and invoices keep reading
/// correctly.
///
/// Deliberately NOT part of the global query filter. Filtering archived rows out
/// everywhere would blank the customer name on a historical job, which is exactly the
/// loss archiving exists to prevent. List endpoints exclude archived rows; detail and
/// history lookups still resolve them.
/// </summary>
public interface IArchivable
{
    /// <summary>Null while active. Set to the UTC instant the record was archived.</summary>
    DateTime? ArchivedAtUtc { get; set; }
}
