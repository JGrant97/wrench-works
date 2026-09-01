using FluentValidation;
using Microsoft.EntityFrameworkCore;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Features.Common;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Zones;

// DTOs
public record CreateZoneRequest(string Name, string? Color, int Capacity);
public record UpdateZoneRequest(string Name, string? Color, int Capacity, bool IsActive);
public record ZoneDto(Guid Id, string Name, string? Color, int Capacity, bool IsActive, DateTime CreatedAtUtc);

// Validators
public class CreateZoneValidator : AbstractValidator<CreateZoneRequest>
{
    public CreateZoneValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Capacity).GreaterThan(0).LessThanOrEqualTo(10);
    }
}

public class UpdateZoneValidator : AbstractValidator<UpdateZoneRequest>
{
    public UpdateZoneValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Capacity).GreaterThan(0).LessThanOrEqualTo(10);
    }
}

// Endpoints
public static class ZoneEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/zones").WithTags("Zones").RequireAuthorization();

        group.MapGet("/", ListAsync).RequireAuthorization("calendar.view");
        group.MapPost("/", CreateAsync).RequireAuthorization("settings.manage");
        group.MapPut("/{id:guid}", UpdateAsync).RequireAuthorization("settings.manage");
        group.MapDelete("/{id:guid}", DeleteAsync).RequireAuthorization("settings.manage");
    }

    private static async Task<IResult> ListAsync(AppDbContext db, CancellationToken ct)
    {
        var zones = await db.Zones
            .OrderBy(z => z.Name)
            .Select(z => new ZoneDto(z.Id, z.Name, z.Color, z.Capacity, z.IsActive, z.CreatedAtUtc))
            .ToListAsync(ct);

        return Results.Ok(zones);
    }

    /// <summary>
    /// Zones model retirement with IsActive rather than ArchivedAtUtc, so this is delete
    /// only: a bay that has never been booked can go, anything else is deactivated via
    /// PUT. Deleting a used bay would previously have cascaded away every booking ever
    /// made in it.
    /// </summary>
    private static async Task<IResult> DeleteAsync(Guid id, AppDbContext db, CancellationToken ct)
    {
        var zone = await db.Zones.FindAsync([id], ct)
            ?? throw new NotFoundException("Zone not found");

        Archiving.EnsureDeletable("zone",
            new Dependent("bookings", await db.Bookings.CountAsync(b => b.ZoneId == id, ct)),
            new Dependent("jobs", await db.Jobs.CountAsync(j => j.AssignedZoneId == id, ct)));

        db.Zones.Remove(zone);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> CreateAsync(
        CreateZoneRequest request,
        AppDbContext db,
        CurrentUserService currentUser,
        CancellationToken ct)
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

        return Results.Created($"/api/zones/{zone.Id}",
            new ZoneDto(zone.Id, zone.Name, zone.Color, zone.Capacity, zone.IsActive, zone.CreatedAtUtc));
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateZoneRequest request,
        AppDbContext db,
        CancellationToken ct)
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

        return Results.Ok(new ZoneDto(zone.Id, zone.Name, zone.Color, zone.Capacity, zone.IsActive, zone.CreatedAtUtc));
    }
}
