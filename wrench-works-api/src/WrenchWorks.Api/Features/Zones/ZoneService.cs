using FluentValidation;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Features.Common;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Zones;

public class ZoneService(IZoneRepository repository, CurrentUserService currentUser) : IZoneService
{
    public Task<List<Zone>> ListAsync(CancellationToken ct) => repository.ListAsync(ct);

    public async Task<Zone> CreateAsync(CreateZoneRequest request, CancellationToken ct)
    {
        await new CreateZoneValidator().ValidateAndThrowAsync(request, ct);

        var businessId = currentUser.RequireBusinessId();
        var name = request.Name.Trim();

        var subscription = await repository.GetSubscriptionAsync(businessId, ct);
        if (subscription != null)
        {
            var activeCount = await repository.CountActiveAsync(ct);
            if (activeCount >= subscription.ZoneLimit)
                throw new LimitReachedException($"Zone limit of {subscription.ZoneLimit} reached for your plan");
        }

        if (await repository.NameExistsAsync(name, null, ct))
            throw new ConflictException("Zone name already exists");

        var zone = new Zone
        {
            BusinessId = businessId,
            Name = name,
            Color = request.Color,
            Capacity = request.Capacity
        };

        repository.Add(zone);
        await repository.SaveChangesAsync(ct);
        return zone;
    }

    public async Task<Zone> UpdateAsync(Guid id, UpdateZoneRequest request, CancellationToken ct)
    {
        await new UpdateZoneValidator().ValidateAndThrowAsync(request, ct);

        var zone = await repository.FindAsync(id, ct)
            ?? throw new NotFoundException("Zone not found");

        var name = request.Name.Trim();
        if (await repository.NameExistsAsync(name, id, ct))
            throw new ConflictException("Zone name already exists");

        zone.Name = name;
        zone.Color = request.Color;
        zone.Capacity = request.Capacity;
        zone.IsActive = request.IsActive;

        await repository.SaveChangesAsync(ct);
        return zone;
    }

    // Zones model retirement with IsActive rather than ArchivedAtUtc, so this is delete
    // only: a bay that has never been booked can go, anything else is deactivated via
    // PUT. Deleting a used bay would previously have cascaded away every booking ever
    // made in it.
    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var zone = await repository.FindAsync(id, ct)
            ?? throw new NotFoundException("Zone not found");

        Archiving.EnsureDeletable("zone",
            new Dependent("bookings", await repository.CountDependentBookingsAsync(id, ct)),
            new Dependent("jobs", await repository.CountDependentJobsAsync(id, ct)));

        repository.Remove(zone);
        await repository.SaveChangesAsync(ct);
    }
}
