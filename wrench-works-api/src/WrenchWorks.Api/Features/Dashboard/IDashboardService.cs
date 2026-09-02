namespace WrenchWorks.Api.Features.Dashboard;

// Everything the dashboard shows, in domain terms. The handler turns this into DashboardDto.
public record DashboardSnapshot(
    List<TodaysBookingRow> TodaysBookings,
    List<ActiveJobRow> ActiveJobs,
    List<StatusCountRow> JobsByStatus,
    List<LowStockRow> LowStock,
    int OpenJobCount,
    int UnscheduledCount,
    decimal RevenueThisMonth,
    decimal RevenueLastMonth,
    int CustomerCount,
    int VehicleCount);

public interface IDashboardService
{
    Task<DashboardSnapshot> GetAsync(CancellationToken ct);
}
