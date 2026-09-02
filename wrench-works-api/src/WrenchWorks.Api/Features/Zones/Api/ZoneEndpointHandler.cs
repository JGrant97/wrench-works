using Microsoft.AspNetCore.Http.HttpResults;
using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Zones;

public class ZoneEndpointHandler(IZoneService service) : IZoneEndpointHandler
{
    private static ZoneDto ToDto(Zone zone) =>
        new(zone.Id, zone.Name, zone.Color, zone.Capacity, zone.IsActive, zone.CreatedAtUtc);

    public async Task<Ok<List<ZoneDto>>> ListAsync(CancellationToken ct)
    {
        var zones = await service.ListAsync(ct);
        return TypedResults.Ok(zones.Select(ToDto).ToList());
    }

    public async Task<Created<ZoneDto>> CreateAsync(CreateZoneRequest request, CancellationToken ct)
    {
        var zone = await service.CreateAsync(request, ct);
        return TypedResults.Created($"/api/zones/{zone.Id}", ToDto(zone));
    }

    public async Task<Ok<ZoneDto>> UpdateAsync(Guid id, UpdateZoneRequest request, CancellationToken ct)
    {
        var zone = await service.UpdateAsync(id, request, ct);
        return TypedResults.Ok(ToDto(zone));
    }

    public async Task<NoContent> DeleteAsync(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return TypedResults.NoContent();
    }
}
