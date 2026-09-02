using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Vehicles;

// A job as the vehicle service-history view shows it, with both totals summed in SQL.
public record VehicleHistoryRow(Guid JobId, string Title, JobStatus Status,
    DateTime? ScheduledStartUtc, DateTime CreatedAtUtc,
    List<string> PartsUsed, decimal LaborTotal, decimal PartsTotal);

public interface IVehicleRepository
{
    Task<List<Vehicle>> SearchAsync(string compact, string term, int take, CancellationToken ct);
    Task<Vehicle?> FindAsync(Guid id, CancellationToken ct);
    Task<Vehicle?> FindWithDetailsAsync(Guid id, CancellationToken ct);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct);
    Task<List<VehicleHistoryRow>> GetHistoryAsync(Guid vehicleId, CancellationToken ct);
    Task<Customer?> FindCustomerAsync(Guid customerId, CancellationToken ct);
    Task<VehicleVariant?> FindActiveVariantAsync(Guid variantId, CancellationToken ct);
    Task<bool> ActiveColourExistsAsync(Guid colourId, CancellationToken ct);
    Task<Vehicle?> FindByRegistrationAsync(string registration, Guid? excludeVehicleId, CancellationToken ct);
    Task<int> CountJobsAsync(Guid vehicleId, CancellationToken ct);
    Task<int> CountBookingsAsync(Guid vehicleId, CancellationToken ct);

    void Add(Vehicle vehicle);
    void Remove(Vehicle vehicle);
    Task SaveChangesAsync(CancellationToken ct);
}
