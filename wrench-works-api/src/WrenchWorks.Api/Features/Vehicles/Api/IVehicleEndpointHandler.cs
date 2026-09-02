using Microsoft.AspNetCore.Http.HttpResults;
using WrenchWorks.Api.Features.Common;

namespace WrenchWorks.Api.Features.Vehicles;

public interface IVehicleEndpointHandler
{
    Task<Ok<List<VehicleSearchResultDto>>> SearchAsync(string q, CancellationToken ct);
    Task<Ok<VehicleDto>> GetAsync(Guid id, CancellationToken ct);
    Task<Ok<List<VehicleHistoryItemDto>>> GetHistoryAsync(Guid id, CancellationToken ct);
    Task<Created<VehicleDto>> CreateAsync(CreateVehicleRequest request, CancellationToken ct);
    Task<Ok<VehicleDto>> UpdateAsync(Guid id, UpdateVehicleRequest request, CancellationToken ct);
    Task<NoContent> DeleteAsync(Guid id, CancellationToken ct);
    Task<Ok<ArchiveResultDto>> ArchiveAsync(Guid id, CancellationToken ct);
    Task<Ok<ArchiveResultDto>> UnarchiveAsync(Guid id, CancellationToken ct);
}
