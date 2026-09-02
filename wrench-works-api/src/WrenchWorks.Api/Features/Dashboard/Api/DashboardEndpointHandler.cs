using Microsoft.AspNetCore.Http.HttpResults;

namespace WrenchWorks.Api.Features.Dashboard;

public class DashboardEndpointHandler(IDashboardService service) : IDashboardEndpointHandler
{
    public async Task<Ok<DashboardDto>> GetAsync(CancellationToken ct)
    {
        var s = await service.GetAsync(ct);

        // Enums become display strings here and nowhere else -- the read models keep them
        // as enums so ordering and grouping stay meaningful upstream.
        return TypedResults.Ok(new DashboardDto(
            s.TodaysBookings.Select(b => new DashboardBookingDto(
                b.Id, b.Title, b.StartUtc, b.EndUtc,
                b.CustomerName, b.VehicleDisplay, b.ZoneName, b.Status.ToString(), b.JobId)).ToList(),
            s.ActiveJobs.Select(j => new DashboardJobDto(
                j.Id, j.Title, j.Status.ToString(), j.Priority.ToString(),
                j.CustomerName, j.VehicleDisplay, j.ScheduledStartUtc)).ToList(),
            s.JobsByStatus
                .Select(g => new StatusCountDto(g.Status.ToString(), g.Count))
                .OrderBy(g => g.Status)
                .ToList(),
            s.LowStock.Select(i => new LowStockItemDto(
                i.Id, i.Name, i.Sku, i.StockOnHand, i.ReorderThreshold)).ToList(),
            s.OpenJobCount,
            s.UnscheduledCount,
            s.RevenueThisMonth,
            s.RevenueLastMonth,
            s.CustomerCount,
            s.VehicleCount));
    }
}
