using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Features.Common;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Vehicles;

public class VehicleService(AppDbContext db, CurrentUserService currentUser) : IVehicleService
{
    /// <summary>
    /// Finds vehicles by registration, VIN, or description.
    ///
    /// Previously the only route to a vehicle was to search for its *customer* first,
    /// so someone ringing up with a plate could not be looked up at all — despite
    /// (BusinessId, Registration) being indexed for exactly this. Spaces are stripped
    /// from the query so "AB12 CDE" finds "AB12CDE".
    /// </summary>
    public async Task<List<VehicleSearchResultDto>> SearchAsync(string q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            return new List<VehicleSearchResultDto>();

        var term = q.Trim().ToUpperInvariant();
        var compact = term.Replace(" ", "");

        var results = await db.Vehicles
            .Include(v => v.Customer)
            .Where(v =>
                (v.Registration != null && v.Registration.Contains(compact)) ||
                (v.Vin != null && v.Vin.ToUpper().Contains(compact)) ||
                (v.DisplayName != null && v.DisplayName.ToUpper().Contains(term)))
            .OrderBy(v => v.DisplayName)
            .Take(25)
            .Select(v => new VehicleSearchResultDto(
                v.Id, v.DisplayName ?? "", v.Registration, v.Vin,
                v.CustomerId, v.Customer.Name))
            .ToListAsync(ct);

        return results;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var vehicle = await db.Vehicles.FindAsync([id], ct)
            ?? throw new NotFoundException("Vehicle not found");

        Archiving.EnsureDeletable("vehicle",
            new Dependent("jobs", await db.Jobs.CountAsync(j => j.VehicleId == id, ct)),
            new Dependent("bookings", await db.Bookings.CountAsync(b => b.VehicleId == id, ct)));

        db.Vehicles.Remove(vehicle);
        await db.SaveChangesAsync(ct);
        return;
    }

    public async Task<ArchiveResultDto> ArchiveAsync(Guid id, CancellationToken ct)
    {
        var vehicle = await db.Vehicles.FindAsync([id], ct)
            ?? throw new NotFoundException("Vehicle not found");

        var result = Archiving.Archive(vehicle, id);
        await db.SaveChangesAsync(ct);
        return result;
    }

    public async Task<ArchiveResultDto> UnarchiveAsync(Guid id, CancellationToken ct)
    {
        var vehicle = await db.Vehicles.FindAsync([id], ct)
            ?? throw new NotFoundException("Vehicle not found");

        var result = Archiving.Unarchive(vehicle, id);
        await db.SaveChangesAsync(ct);
        return result;
    }

    public async Task<VehicleDto> CreateAsync(CreateVehicleRequest request, CancellationToken ct)
    {
        await new CreateVehicleValidator().ValidateAndThrowAsync(request, ct);

        var customer = await db.Customers.FindAsync([request.CustomerId], ct)
            ?? throw new NotFoundException("Customer not found");

        var variant = await LoadVariantAsync(db, request.VariantId, ct);
        EnsureYearInRange(variant, request.Year);
        await EnsureColourExistsAsync(db, request.ColourId, ct);
        await EnsureRegistrationIsFreeAsync(db, request.Registration, null, ct);

        var vehicle = new Vehicle
        {
            BusinessId = currentUser.RequireBusinessId(),
            CustomerId = request.CustomerId,
            VariantId = variant.Id,
            Year = request.Year,
            ColourId = request.ColourId,
            DisplayName = BuildDisplayName(variant, request.Year),
            Vin = request.Vin?.Trim(),
            Registration = request.Registration?.Trim().ToUpperInvariant(),
            Notes = request.Notes
        };
        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync(ct);

        return await ToDtoAsync(db, vehicle.Id, ct);
    }

    public async Task<VehicleDto> UpdateAsync(Guid id, UpdateVehicleRequest request, CancellationToken ct)
    {
        await new UpdateVehicleValidator().ValidateAndThrowAsync(request, ct);

        var vehicle = await db.Vehicles.FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw new NotFoundException("Vehicle not found");

        var variant = await LoadVariantAsync(db, request.VariantId, ct);
        EnsureYearInRange(variant, request.Year);
        await EnsureColourExistsAsync(db, request.ColourId, ct);
        await EnsureRegistrationIsFreeAsync(db, request.Registration, vehicle.Id, ct);

        vehicle.VariantId = variant.Id;
        vehicle.Year = request.Year;
        vehicle.ColourId = request.ColourId;
        vehicle.DisplayName = BuildDisplayName(variant, request.Year);
        vehicle.Vin = request.Vin?.Trim();
        vehicle.Registration = request.Registration?.Trim().ToUpperInvariant();
        vehicle.Notes = request.Notes;

        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(db, vehicle.Id, ct);
    }

    public async Task<VehicleDto> GetAsync(Guid id, CancellationToken ct)
    {
        var exists = await db.Vehicles.AnyAsync(v => v.Id == id, ct);
        if (!exists) throw new NotFoundException("Vehicle not found");

        return await ToDtoAsync(db, id, ct);
    }

    public async Task<List<VehicleHistoryItemDto>> GetHistoryAsync(Guid id, CancellationToken ct)
    {
        var vehicle = await db.Vehicles.AnyAsync(v => v.Id == id, ct);
        if (!vehicle) throw new NotFoundException("Vehicle not found");

        var jobs = await db.Jobs
            .Where(j => j.VehicleId == id)
            .OrderByDescending(j => j.CreatedAtUtc)
            .Include(j => j.PartLines).ThenInclude(pl => pl.InventoryItem)
            .Include(j => j.LaborLines)
            .Select(j => new VehicleHistoryItemDto(
                j.Id,
                j.Title,
                j.Status.ToString(),
                j.ScheduledStartUtc,
                j.CreatedAtUtc,
                j.PartLines.Select(pl => pl.InventoryItem.Name),
                j.LaborLines.Sum(l => l.Hours * l.Rate),
                j.PartLines.Sum(p => p.Quantity * p.UnitPrice)))
            .ToListAsync(ct);

        return jobs;
    }

    /// <summary>
    /// The catalogue is global, so this is not tenant-filtered — but the UI can only
    /// ever offer variants it was served, and the year is re-checked below regardless.
    /// </summary>
    private static async Task<VehicleVariant> LoadVariantAsync(AppDbContext db, Guid variantId, CancellationToken ct)
=> await db.VehicleVariants
               .Include(v => v.Model).ThenInclude(m => m.Make)
               .FirstOrDefaultAsync(v => v.Id == variantId && v.IsActive, ct)
           ?? throw new NotFoundException("Vehicle variant not found");

    /// <summary>
    /// The cascade only offers years inside the variant's range, but the API must not
    /// trust that — a hand-crafted request could pair a 2020 year with a 1998 variant.
    /// </summary>
    private static void EnsureYearInRange(VehicleVariant variant, int year)
    {
        if (year < variant.YearFrom || year > variant.YearTo)
            throw new ConflictException(
                $"{variant.Model.Make.Name} {variant.Model.Name} '{variant.Describe()}' was not built in {year} " +
                $"(available {variant.YearFrom}–{variant.YearTo})");
    }

    // Rejects a registration already used by another vehicle in this business.
    // 
    // Two records for one plate silently split a vehicle's service history, and the
    // customer create path already guards phone/email the same way. The index on
    // (BusinessId, Registration) is deliberately not unique — a hard constraint would
    // break the legitimate case of a plate being transferred between vehicles — so the
    // check lives here, where it can produce a useful message. The query is tenant
    // filtered, so this can only ever match a vehicle in the caller's own business.
    //
    // NOTE: plain comment, not XML doc — the .NET 10 preview OpenAPI XML-comment
    // source generator emits System.Void (CS0673) for Task-returning helpers.
    private static async Task EnsureRegistrationIsFreeAsync(AppDbContext db, string? registration, Guid? excludeVehicleId, CancellationToken ct)
    {
        var normalised = registration?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalised)) return;

        var clash = await db.Vehicles
            .Include(v => v.Customer)
            .FirstOrDefaultAsync(v =>
                v.Registration == normalised &&
                (excludeVehicleId == null || v.Id != excludeVehicleId), ct);

        if (clash is not null)
            throw new ConflictException(
                $"Registration {normalised} is already on {clash.Customer.Name}'s {clash.DisplayName ?? "vehicle"}.",
                new { existingVehicleId = clash.Id, existingCustomerId = clash.CustomerId });
    }

    private static async Task EnsureColourExistsAsync(AppDbContext db, Guid? colourId, CancellationToken ct)
    {
        if (colourId is null) return;
        var exists = await db.VehicleColours.AnyAsync(c => c.Id == colourId.Value && c.IsActive, ct);
        if (!exists) throw new NotFoundException("Colour not found");
    }

    private static string BuildDisplayName(VehicleVariant variant, int year)
    {
        var parts = new List<string> { year.ToString(), variant.Model.Make.Name, variant.Model.Name };

        var trim = variant.Trim?.Trim();
        var displacement = variant.EngineDisplacementL.HasValue
            ? variant.EngineDisplacementL.Value.ToString("0.0")
            : null;

        // Trims are often named after the engine ("1.8", "1.6 TDCi"), so adding the
        // displacement as well would read "MX-5 1.8 1.8". Only add it when the trim
        // doesn't already lead with it.
        if (displacement is not null &&
            (string.IsNullOrEmpty(trim) || !trim.StartsWith(displacement, StringComparison.Ordinal)))
        {
            parts.Add(displacement);
        }

        // "Base" is the absence of a named edition — showing it adds nothing.
        if (!string.IsNullOrWhiteSpace(trim) && !trim.Equals("Base", StringComparison.OrdinalIgnoreCase))
            parts.Add(trim);

        if (!string.IsNullOrWhiteSpace(variant.BodyStyle)) parts.Add(variant.BodyStyle!);

        return string.Join(' ', parts);
    }

    private static async Task<VehicleDto> ToDtoAsync(AppDbContext db, Guid vehicleId, CancellationToken ct)
    {
        var v = await db.Vehicles
            .Include(x => x.Customer)
            .Include(x => x.Colour)
            .Include(x => x.Variant!).ThenInclude(va => va.Model).ThenInclude(m => m.Make)
            .FirstAsync(x => x.Id == vehicleId, ct);

        // A vehicle predating the catalogue has no Variant. This used to dereference it
        // unconditionally, so such a row 500'd on read and could not even be opened to be
        // corrected. Fall back to the deprecated free-text columns instead — those still
        // hold what the row was created with, and are the only description it has until
        // someone re-picks it from the catalogue.
        var variant = v.Variant;

        return new VehicleDto(
            v.Id, v.CustomerId, v.Customer.Name,
            v.DisplayName ?? LegacyDisplayName(v),
            variant?.Id, v.Year,
            variant?.Model.Make.Name ?? v.Make, variant?.Model.Name ?? v.Model,
            variant?.Trim, variant?.BodyStyle,
            variant?.EngineDisplacementL,
            variant?.FuelType.ToString() ?? v.FuelType,
            variant?.Transmission.ToString(),
            v.ColourId, v.Colour?.Name,
            v.Vin, v.Registration, v.Notes, v.CreatedAtUtc);
    }

    /// <summary>
    /// How a pre-catalogue vehicle reads when it has no DisplayName snapshot: the same
    /// "Unnamed" fallback the list view already used, so nothing renders blank.
    /// </summary>
    private static string LegacyDisplayName(Domain.Entities.Vehicle v)
    {
        var parts = new[] { v.Year?.ToString(), v.Make, v.Model }
            .Where(p => !string.IsNullOrWhiteSpace(p));

        var name = string.Join(" ", parts);
        return string.IsNullOrWhiteSpace(name) ? "Unnamed vehicle" : name;
    }
}
