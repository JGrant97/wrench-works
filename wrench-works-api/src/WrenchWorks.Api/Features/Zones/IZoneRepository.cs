using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Zones;

/// <summary>
/// Data access for the Zones slice. Returns entities, never DTOs -- the mapping happens in
/// ZoneEndpointHandler. Tenant scoping is not done here: AppDbContext's global query
/// filter applies BusinessId from the JWT, so every read below is already tenant-scoped.
/// </summary>
public interface IZoneRepository
{
    Task<List<Zone>> ListAsync(CancellationToken ct);
    Task<Zone?> FindAsync(Guid id, CancellationToken ct);
    Task<bool> NameExistsAsync(string name, Guid? excludingId, CancellationToken ct);
    Task<int> CountActiveAsync(CancellationToken ct);
    Task<int> CountDependentBookingsAsync(Guid zoneId, CancellationToken ct);
    Task<int> CountDependentJobsAsync(Guid zoneId, CancellationToken ct);
    Task<BusinessSubscription?> GetSubscriptionAsync(Guid businessId, CancellationToken ct);

    void Add(Zone zone);
    void Remove(Zone zone);
    Task SaveChangesAsync(CancellationToken ct);
}
