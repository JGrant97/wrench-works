using Microsoft.AspNetCore.Http.HttpResults;

namespace WrenchWorks.Api.Features.Zones;

/// <summary>
/// The HTTP layer for Zones. This is the only place a Zone becomes a ZoneDto, and the
/// only place that knows about status codes. ZoneEndpoints.Map does nothing but route to
/// these methods, so the endpoints class holds no logic and no private helpers.
/// </summary>
public interface IZoneEndpointHandler
{
    Task<Ok<List<ZoneDto>>> ListAsync(CancellationToken ct);
    Task<Created<ZoneDto>> CreateAsync(CreateZoneRequest request, CancellationToken ct);
    Task<Ok<ZoneDto>> UpdateAsync(Guid id, UpdateZoneRequest request, CancellationToken ct);
    Task<NoContent> DeleteAsync(Guid id, CancellationToken ct);
}
