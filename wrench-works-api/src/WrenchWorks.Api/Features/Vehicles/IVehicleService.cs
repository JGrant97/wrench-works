using WrenchWorks.Api.Features.Common;

namespace WrenchWorks.Api.Features.Vehicles;

// The Vehicle slice behind an interface: the endpoints become a thin HTTP shell.
// Methods return DTOs, not IResult -- failures are thrown and mapped by
// ErrorHandlingMiddleware, so nothing here needs to know about status codes.
public interface IVehicleService
{
    Task<List<VehicleSearchResultDto>> SearchAsync(string q, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
    Task<ArchiveResultDto> ArchiveAsync(Guid id, CancellationToken ct);
    Task<ArchiveResultDto> UnarchiveAsync(Guid id, CancellationToken ct);
    Task<VehicleDto> CreateAsync(CreateVehicleRequest request, CancellationToken ct);
    Task<VehicleDto> UpdateAsync(Guid id, UpdateVehicleRequest request, CancellationToken ct);
    Task<VehicleDto> GetAsync(Guid id, CancellationToken ct);
    Task<List<VehicleHistoryItemDto>> GetHistoryAsync(Guid id, CancellationToken ct);
}
