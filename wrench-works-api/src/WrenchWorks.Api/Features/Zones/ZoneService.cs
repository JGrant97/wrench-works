using FluentValidation;
using Microsoft.EntityFrameworkCore;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Features.Common;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Zones;

public class ZoneService(AppDbContext db, CurrentUserService currentUser) : IZoneService
{
    private static ZoneDto ToDto(Zone z) =>
        new(z.Id, z.Name, z.Color, z.Capacity, z.IsActive, z.CreatedAtUtc);

    public async Task<List<ZoneDto>> ListAsync(CancellationToken ct) =>
        await db.Zones
            .OrderBy(z => z.Name)
            .Select(z => new ZoneDto(z.Id, z.Name, z.Color, z.Capacity, z.IsActive, z.CreatedAtUtc))
            .ToListAsync(ct);

    public async Task<ZoneDto> CreateAsync(CreateZoneRequest request, CancellationToken ct)
    {
        await new CreateZoneValidator().ValidateAndThrowAsync(request, ct);

        var businessId = currentUser.RequireBusinessId();

        // Check zone limit
        var sub = await db.BusinessSubscriptions.FirstOrDefaultAsync(s => s.BusinessId == businessId, ct);
        if (sub != null)
        {
            var currentCount = await db.Zones.CountAsync(z => z.IsActive, ct);
            if (currentCount >= sub.ZoneLimit)
                throw new LimitReachedException($"Zone limit of {sub.ZoneLimit} reached for your plan");
        }

        // Check duplicate name
        var exists = await db.Zones.AnyAsync(z => z.Name == request.Name.Trim(), ct);
        if (exists)
            throw new ConflictException("Zone name already exists");

        var zone = new Zone
        {
            BusinessId = businessId,
            Name = request.Name.Trim(),
            Color = request.Color,
            Capacity = request.Capacity
        };
        db.Zones.Add(zone);
        await db.SaveChangesAsync(ct);

        return ToDto(zone);
    }

    public async Task<ZoneDto> UpdateAsync(Guid id, UpdateZoneRequest request, CancellationToken ct)
    {
        await new UpdateZoneValidator().ValidateAndThrowAsync(request, ct);

        var zone = await db.Zones.FindAsync([id], ct)
            ?? throw new NotFoundException("Zone not found");

        var nameConflict = await db.Zones.AnyAsync(z => z.Id != id && z.Name == request.Name.Trim(), ct);
        if (nameConflict) throw new ConflictException("Zone name already exists");

        zone.Name = request.Name.Trim();
        zone.Color = request.Color;
        zone.Capacity = request.Capacity;
        zone.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);

        return ToDto(zone);
    }

    // Zones model retirement with IsActive rather than ArchivedAtUtc, so this is delete
    // only: a bay that has never been booked can go, anything else is deactivated via
    // PUT. Deleting a used bay would previously have cascaded away every booking ever
    // made in it.
    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var zone = await db.Zones.FindAsync([id], ct)
            ?? throw new NotFoundException("Zone not found");

        Archiving.EnsureDeletable("zone",
            new Dependent("bookings", await db.Bookings.CountAsync(b => b.ZoneId == id, ct)),
            new Dependent("jobs", await db.Jobs.CountAsync(j => j.AssignedZoneId == id, ct)));

        db.Zones.Remove(zone);
        await db.SaveChangesAsync(ct);
    }
}
