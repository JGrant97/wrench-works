using Microsoft.AspNetCore.Http.HttpResults;
using WrenchWorks.Api.Features.Common;
using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Vehicles;

public class VehicleEndpointHandler(IVehicleService service) : IVehicleEndpointHandler
{
    // A vehicle predating the catalogue has no Variant. This used to be dereferenced
    // unconditionally, so such a row 500'd on read and could not even be opened to be
    // corrected. Fall back to the deprecated free-text columns instead -- those still hold
    // what the row was created with, and are its only description until someone re-picks
    // it from the catalogue. Finding 10 in docs/review-findings.md.
    private static VehicleDto ToDto(Vehicle v)
    {
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
    private static string LegacyDisplayName(Vehicle v)
    {
        var parts = new[] { v.Year?.ToString(), v.Make, v.Model }
            .Where(p => !string.IsNullOrWhiteSpace(p));

        var name = string.Join(" ", parts);
        return string.IsNullOrWhiteSpace(name) ? "Unnamed vehicle" : name;
    }

    public async Task<Ok<List<VehicleSearchResultDto>>> SearchAsync(string q, CancellationToken ct)
    {
        var vehicles = await service.SearchAsync(q, ct);
        return TypedResults.Ok(vehicles.Select(v => new VehicleSearchResultDto(
            v.Id, v.DisplayName ?? "", v.Registration, v.Vin, v.CustomerId, v.Customer.Name)).ToList());
    }

    public async Task<Ok<VehicleDto>> GetAsync(Guid id, CancellationToken ct) =>
        TypedResults.Ok(ToDto(await service.GetAsync(id, ct)));

    public async Task<Ok<List<VehicleHistoryItemDto>>> GetHistoryAsync(Guid id, CancellationToken ct)
    {
        var history = await service.GetHistoryAsync(id, ct);
        return TypedResults.Ok(history.Select(j => new VehicleHistoryItemDto(
            j.JobId, j.Title, j.Status.ToString(), j.ScheduledStartUtc, j.CreatedAtUtc,
            j.PartsUsed, j.LaborTotal, j.PartsTotal)).ToList());
    }

    public async Task<Created<VehicleDto>> CreateAsync(CreateVehicleRequest request, CancellationToken ct)
    {
        var vehicle = await service.CreateAsync(request, ct);
        return TypedResults.Created($"/api/vehicles/{vehicle.Id}", ToDto(vehicle));
    }

    public async Task<Ok<VehicleDto>> UpdateAsync(Guid id, UpdateVehicleRequest request, CancellationToken ct) =>
        TypedResults.Ok(ToDto(await service.UpdateAsync(id, request, ct)));

    public async Task<NoContent> DeleteAsync(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return TypedResults.NoContent();
    }

    public async Task<Ok<ArchiveResultDto>> ArchiveAsync(Guid id, CancellationToken ct) =>
        TypedResults.Ok(await service.ArchiveAsync(id, ct));

    public async Task<Ok<ArchiveResultDto>> UnarchiveAsync(Guid id, CancellationToken ct) =>
        TypedResults.Ok(await service.UnarchiveAsync(id, ct));
}
