namespace WrenchWorks.Api.Features.Zones;

/// <summary>
/// The Zones slice's behaviour, behind an interface so the endpoints stay a thin HTTP
/// shell. Methods return DTOs rather than IResult: failures are thrown and mapped by
/// ErrorHandlingMiddleware, so the service never needs to know about status codes.
/// </summary>
public interface IZoneService
{
    Task<List<ZoneDto>> ListAsync(CancellationToken ct);
    Task<ZoneDto> CreateAsync(CreateZoneRequest request, CancellationToken ct);
    Task<ZoneDto> UpdateAsync(Guid id, UpdateZoneRequest request, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}
