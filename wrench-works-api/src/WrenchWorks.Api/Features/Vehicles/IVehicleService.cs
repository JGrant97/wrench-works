using WrenchWorks.Api.Features.Common;
using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Vehicles;

public interface IVehicleService
{
    Task<List<Vehicle>> SearchAsync(string q, CancellationToken ct);
    Task<Vehicle> GetAsync(Guid id, CancellationToken ct);
    Task<List<VehicleHistoryRow>> GetHistoryAsync(Guid id, CancellationToken ct);
    Task<Vehicle> CreateAsync(CreateVehicleRequest request, CancellationToken ct);
    Task<Vehicle> UpdateAsync(Guid id, UpdateVehicleRequest request, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
    Task<ArchiveResultDto> ArchiveAsync(Guid id, CancellationToken ct);
    Task<ArchiveResultDto> UnarchiveAsync(Guid id, CancellationToken ct);
}
