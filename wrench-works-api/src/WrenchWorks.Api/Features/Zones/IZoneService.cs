using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Zones;

/// <summary>
/// The business rules for Zones: validation, plan limits, name uniqueness, and the
/// delete-vs-deactivate rule. Returns entities; ZoneEndpointHandler maps them to DTOs.
/// Failures throw the ErrorHandlingMiddleware exception types, so nothing here knows
/// about status codes.
/// </summary>
public interface IZoneService
{
    Task<List<Zone>> ListAsync(CancellationToken ct);
    Task<Zone> CreateAsync(CreateZoneRequest request, CancellationToken ct);
    Task<Zone> UpdateAsync(Guid id, UpdateZoneRequest request, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}
