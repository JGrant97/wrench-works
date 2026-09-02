using Microsoft.EntityFrameworkCore;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Zones;

public class ZoneRepository(AppDbContext db) : IZoneRepository
{
    public Task<List<Zone>> ListAsync(CancellationToken ct) =>
        db.Zones.OrderBy(z => z.Name).ToListAsync(ct);

    public async Task<Zone?> FindAsync(Guid id, CancellationToken ct) =>
        await db.Zones.FindAsync([id], ct);

    public Task<bool> NameExistsAsync(string name, Guid? excludingId, CancellationToken ct) =>
        db.Zones.AnyAsync(z => z.Name == name && (excludingId == null || z.Id != excludingId), ct);

    public Task<int> CountActiveAsync(CancellationToken ct) =>
        db.Zones.CountAsync(z => z.IsActive, ct);

    public Task<int> CountDependentBookingsAsync(Guid zoneId, CancellationToken ct) =>
        db.Bookings.CountAsync(b => b.ZoneId == zoneId, ct);

    public Task<int> CountDependentJobsAsync(Guid zoneId, CancellationToken ct) =>
        db.Jobs.CountAsync(j => j.AssignedZoneId == zoneId, ct);

    public Task<BusinessSubscription?> GetSubscriptionAsync(Guid businessId, CancellationToken ct) =>
        db.BusinessSubscriptions.FirstOrDefaultAsync(s => s.BusinessId == businessId, ct);

    public void Add(Zone zone) => db.Zones.Add(zone);
    public void Remove(Zone zone) => db.Zones.Remove(zone);
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
