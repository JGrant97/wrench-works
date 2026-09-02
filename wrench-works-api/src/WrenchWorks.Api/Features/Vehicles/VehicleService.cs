using FluentValidation;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Features.Common;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Vehicles;

public class VehicleService(IVehicleRepository repository, CurrentUserService currentUser) : IVehicleService
{
    /// <summary>
    /// Finds vehicles by registration, VIN, or description.
    ///
    /// Previously the only route to a vehicle was to search for its *customer* first, so
    /// someone ringing up with a plate could not be looked up at all. Spaces are stripped
    /// from the query so "AB12 CDE" finds "AB12CDE".
    /// </summary>
    public Task<List<Vehicle>> SearchAsync(string q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            return Task.FromResult(new List<Vehicle>());

        var term = q.Trim().ToUpperInvariant();
        return repository.SearchAsync(term.Replace(" ", ""), term, 25, ct);
    }

    public async Task<Vehicle> GetAsync(Guid id, CancellationToken ct) =>
        await repository.FindWithDetailsAsync(id, ct)
            ?? throw new NotFoundException("Vehicle not found");

    public async Task<List<VehicleHistoryRow>> GetHistoryAsync(Guid id, CancellationToken ct)
    {
        if (!await repository.ExistsAsync(id, ct))
            throw new NotFoundException("Vehicle not found");

        return await repository.GetHistoryAsync(id, ct);
    }

    public async Task<Vehicle> CreateAsync(CreateVehicleRequest request, CancellationToken ct)
    {
        await new CreateVehicleValidator().ValidateAndThrowAsync(request, ct);

        _ = await repository.FindCustomerAsync(request.CustomerId, ct)
            ?? throw new NotFoundException("Customer not found");

        var variant = await LoadVariantAsync(request.VariantId, ct);
        EnsureYearInRange(variant, request.Year);
        await EnsureColourExistsAsync(request.ColourId, ct);
        await EnsureRegistrationIsFreeAsync(request.Registration, null, ct);

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

        repository.Add(vehicle);
        await repository.SaveChangesAsync(ct);

        // Re-read with the customer, colour and variant graph the handler needs to map.
        return await repository.FindWithDetailsAsync(vehicle.Id, ct)
            ?? throw new NotFoundException("Vehicle not found");
    }

    public async Task<Vehicle> UpdateAsync(Guid id, UpdateVehicleRequest request, CancellationToken ct)
    {
        await new UpdateVehicleValidator().ValidateAndThrowAsync(request, ct);

        var vehicle = await repository.FindAsync(id, ct)
            ?? throw new NotFoundException("Vehicle not found");

        var variant = await LoadVariantAsync(request.VariantId, ct);
        EnsureYearInRange(variant, request.Year);
        await EnsureColourExistsAsync(request.ColourId, ct);
        await EnsureRegistrationIsFreeAsync(request.Registration, vehicle.Id, ct);

        vehicle.VariantId = variant.Id;
        vehicle.Year = request.Year;
        vehicle.ColourId = request.ColourId;
        vehicle.DisplayName = BuildDisplayName(variant, request.Year);
        vehicle.Vin = request.Vin?.Trim();
        vehicle.Registration = request.Registration?.Trim().ToUpperInvariant();
        vehicle.Notes = request.Notes;

        await repository.SaveChangesAsync(ct);

        return await repository.FindWithDetailsAsync(vehicle.Id, ct)
            ?? throw new NotFoundException("Vehicle not found");
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var vehicle = await repository.FindAsync(id, ct)
            ?? throw new NotFoundException("Vehicle not found");

        Archiving.EnsureDeletable("vehicle",
            new Dependent("jobs", await repository.CountJobsAsync(id, ct)),
            new Dependent("bookings", await repository.CountBookingsAsync(id, ct)));

        repository.Remove(vehicle);
        await repository.SaveChangesAsync(ct);
    }

    public async Task<ArchiveResultDto> ArchiveAsync(Guid id, CancellationToken ct)
    {
        var vehicle = await repository.FindAsync(id, ct)
            ?? throw new NotFoundException("Vehicle not found");

        var result = Archiving.Archive(vehicle, id);
        await repository.SaveChangesAsync(ct);
        return result;
    }

    public async Task<ArchiveResultDto> UnarchiveAsync(Guid id, CancellationToken ct)
    {
        var vehicle = await repository.FindAsync(id, ct)
            ?? throw new NotFoundException("Vehicle not found");

        var result = Archiving.Unarchive(vehicle, id);
        await repository.SaveChangesAsync(ct);
        return result;
    }

    private async Task<VehicleVariant> LoadVariantAsync(Guid variantId, CancellationToken ct) =>
        await repository.FindActiveVariantAsync(variantId, ct)
            ?? throw new NotFoundException("Vehicle variant not found");

    /// <summary>
    /// The cascade only offers years inside the variant range, but the API must not trust
    /// that -- a hand-crafted request could pair a 2020 year with a 1998 variant.
    /// </summary>
    private static void EnsureYearInRange(VehicleVariant variant, int year)
    {
        if (year < variant.YearFrom || year > variant.YearTo)
            throw new ConflictException(
                $"{variant.Model.Make.Name} {variant.Model.Name} '{variant.Describe()}' was not built in {year} " +
                $"(available {variant.YearFrom}-{variant.YearTo})");
    }

    // Rejects a registration already used by another vehicle in this business.
    //
    // Two records for one plate silently split a vehicle service history. The index on
    // (BusinessId, Registration) is deliberately not unique -- a hard constraint would
    // break the legitimate case of a plate being transferred -- so the check lives here,
    // where it can produce a useful message. Being read-then-write it can still lose a
    // genuine race; finding 8 in docs/review-findings.md.
    //
    // Plain comment, not XML doc: the .NET 10 preview OpenAPI XML-comment source generator
    // emits System.Void (CS0673) for Task-returning helpers.
    private async Task EnsureRegistrationIsFreeAsync(string? registration, Guid? excludeVehicleId, CancellationToken ct)
    {
        var normalised = registration?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalised)) return;

        var clash = await repository.FindByRegistrationAsync(normalised, excludeVehicleId, ct);
        if (clash is not null)
            throw new ConflictException(
                $"Registration {normalised} is already on {clash.Customer.Name}'s {clash.DisplayName ?? "vehicle"}.",
                new { existingVehicleId = clash.Id, existingCustomerId = clash.CustomerId });
    }

    private async Task EnsureColourExistsAsync(Guid? colourId, CancellationToken ct)
    {
        if (colourId is null) return;
        if (!await repository.ActiveColourExistsAsync(colourId.Value, ct))
            throw new NotFoundException("Colour not found");
    }

    /// <summary>
    /// The denormalised description stamped onto the vehicle at write time, so a later
    /// catalogue correction cannot rewrite what a historical job says. See
    /// docs/vehicle-catalogue.md.
    /// </summary>
    private static string BuildDisplayName(VehicleVariant variant, int year)
    {
        var parts = new List<string> { year.ToString(), variant.Model.Make.Name, variant.Model.Name };

        var trim = variant.Trim?.Trim();
        var displacement = variant.EngineDisplacementL.HasValue
            ? variant.EngineDisplacementL.Value.ToString("0.0")
            : null;

        // Trims are often named after the engine ("1.8", "1.6 TDCi"), so adding the
        // displacement as well would read "MX-5 1.8 1.8". Only add it when the trim does
        // not already lead with it.
        if (displacement is not null &&
            (string.IsNullOrEmpty(trim) || !trim.StartsWith(displacement, StringComparison.Ordinal)))
        {
            parts.Add(displacement);
        }

        // "Base" is the absence of a named edition -- showing it adds nothing.
        if (!string.IsNullOrWhiteSpace(trim) && !trim.Equals("Base", StringComparison.OrdinalIgnoreCase))
            parts.Add(trim);

        if (!string.IsNullOrWhiteSpace(variant.BodyStyle)) parts.Add(variant.BodyStyle!);

        return string.Join(' ', parts);
    }
}
