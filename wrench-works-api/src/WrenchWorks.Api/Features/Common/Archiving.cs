using WrenchWorks.Api.Middleware;
using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Common;

/// <summary>What a delete or archive call returns, so the client can tell which happened.</summary>
public record ArchiveResultDto(Guid Id, bool Archived, DateTime? ArchivedAtUtc);

/// <summary>
/// Named count of rows blocking a hard delete, e.g. ("jobs", 3). Label is the plural;
/// <see cref="Describe"/> trims it back to the singular when there is only one, because
/// "1 vehicles" in a message the user is meant to act on reads as a bug.
/// </summary>
public record Dependent(string Label, int Count)
{
    public string Describe() => Count == 1 ? $"1 {Singular(Label)}" : $"{Count} {Label}";

    /// <summary>
    /// Enough for the labels actually in use ("vehicles", "jobs", "bookings", "part lines",
    /// "stock movements"). Deliberately not a general pluraliser — a wrong guess on an
    /// irregular noun would be worse than the trailing "s" it removes.
    /// </summary>
    private static string Singular(string plural) =>
        plural.EndsWith("ies") ? $"{plural[..^3]}y"
        : plural.EndsWith('s') ? plural[..^1]
        : plural;
}

/// <summary>
/// The delete rule, in one place: a record is removed permanently only when nothing
/// references it. Anything carrying history is archived instead.
///
/// The reason this is shared rather than repeated per slice is that the failure mode is
/// silent. Every one of these foreign keys used to be Cascade, so a delete endpoint
/// written without the check would not have errored — it would have quietly taken the
/// customer's vehicles, jobs and bookings with them and reported success.
/// </summary>
public static class Archiving
{
    /// <summary>
    /// Refuses the delete when anything still points at the record, naming what and how
    /// many so the caller can act. A vague "cannot delete" leaves the user guessing which
    /// of five relationships is the problem.
    /// </summary>
    public static void EnsureDeletable(string entity, params Dependent[] dependents)
    {
        var blocking = dependents.Where(d => d.Count > 0).ToList();
        if (blocking.Count == 0) return;

        var detail = string.Join(", ", blocking.Select(d => d.Describe()));

        throw new ConflictException(
            $"This {entity} has {detail} and cannot be deleted. Archive it instead — " +
            "it will be hidden from lists while its history stays intact.",
            new { dependents = blocking.Select(d => new { d.Label, d.Count }) });
    }

    /// <summary>Marks a record archived. Idempotent — re-archiving keeps the original instant.</summary>
    public static ArchiveResultDto Archive(IArchivable entity, Guid id)
    {
        entity.ArchivedAtUtc ??= DateTime.UtcNow;
        return new ArchiveResultDto(id, true, entity.ArchivedAtUtc);
    }

    public static ArchiveResultDto Unarchive(IArchivable entity, Guid id)
    {
        entity.ArchivedAtUtc = null;
        return new ArchiveResultDto(id, false, null);
    }
}
