using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Dashboard;

// Read models, not API DTOs. Every dashboard query is an aggregate or a narrow projection,
// so returning entities would mean loading whole graphs to count them. These stay
// domain-shaped -- enums stay enums; turning them into display strings is the handler's job.
public record TodaysBookingRow(Guid Id, string Title, DateTime StartUtc, DateTime EndUtc,
    string CustomerName, string VehicleDisplay, string ZoneName, BookingStatus Status, Guid? JobId);

public record ActiveJobRow(Guid Id, string Title, JobStatus Status, JobPriority Priority,
    string CustomerName, string VehicleDisplay, DateTime? ScheduledStartUtc);

public record StatusCountRow(JobStatus Status, int Count);

public record LowStockRow(Guid Id, string Name, string? Sku, int StockOnHand, int ReorderThreshold);

public interface IDashboardRepository
{
    Task<List<TodaysBookingRow>> GetBookingsOverlappingAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct);
    Task<List<ActiveJobRow>> GetActiveJobsAsync(int take, CancellationToken ct);
    Task<List<StatusCountRow>> GetJobCountsByStatusAsync(CancellationToken ct);
    Task<List<LowStockRow>> GetLowStockAsync(int take, CancellationToken ct);
    Task<int> CountOpenJobsAsync(CancellationToken ct);
    Task<int> CountUnscheduledOpenJobsAsync(CancellationToken ct);
    Task<decimal> GetRevenueBetweenAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct);
    Task<int> CountActiveCustomersAsync(CancellationToken ct);
    Task<int> CountActiveVehiclesAsync(CancellationToken ct);
}
