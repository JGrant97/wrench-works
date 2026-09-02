using Microsoft.EntityFrameworkCore;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Dashboard;

public class DashboardRepository(AppDbContext db) : IDashboardRepository
{
    // List<T>, deliberately not T[]. On .NET 10 an array's .Contains() binds to
    // MemoryExtensions.Contains(ReadOnlySpan<T>, T), which EF Core cannot evaluate as a
    // query parameter -- it throws "GenericArguments[1] ... violates the constraint of type
    // parameter TRet" at runtime and surfaces as a bare 500. A List picks Enumerable.Contains
    // and translates to SQL IN. Caught by DashboardTests; see CLAUDE.md.

    // Statuses that mean "still in the workshop" rather than finished.
    private static readonly List<JobStatus> OpenStatuses =
        [JobStatus.Draft, JobStatus.Scheduled, JobStatus.InProgress, JobStatus.WaitingParts];

    // Statuses at which the work is done and the money is real.
    private static readonly List<JobStatus> EarnedStatuses =
        [JobStatus.Completed, JobStatus.Invoiced, JobStatus.Closed];

    private IQueryable<Job> OpenJobs =>
        db.Jobs.Where(j => j.ArchivedAtUtc == null && OpenStatuses.Contains(j.Status));

    public Task<List<TodaysBookingRow>> GetBookingsOverlappingAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct) =>
        db.Bookings
          .Where(b => b.Status != BookingStatus.Cancelled && b.StartUtc < toUtc && b.EndUtc > fromUtc)
          .OrderBy(b => b.StartUtc)
          .Select(b => new TodaysBookingRow(
              b.Id, b.Title, b.StartUtc, b.EndUtc,
              b.Customer.Name, b.Vehicle.DisplayName, b.Zone.Name, b.Status, b.JobId))
          .ToListAsync(ct);

    public Task<List<ActiveJobRow>> GetActiveJobsAsync(int take, CancellationToken ct) =>
        OpenJobs
          .OrderBy(j => j.ScheduledStartUtc == null)
          .ThenBy(j => j.ScheduledStartUtc)
          .Take(take)
          .Select(j => new ActiveJobRow(
              j.Id, j.Title, j.Status, j.Priority,
              j.Customer.Name, j.Vehicle.DisplayName, j.ScheduledStartUtc))
          .ToListAsync(ct);

    // Group to the enum in SQL, then name it in memory. Calling ToString() on the group
    // key inside the projection does not translate.
    public Task<List<StatusCountRow>> GetJobCountsByStatusAsync(CancellationToken ct) =>
        db.Jobs
          .Where(j => j.ArchivedAtUtc == null)
          .GroupBy(j => j.Status)
          .Select(g => new StatusCountRow(g.Key, g.Count()))
          .ToListAsync(ct);

    public Task<List<LowStockRow>> GetLowStockAsync(int take, CancellationToken ct) =>
        db.InventoryItems
          .Where(i => i.ArchivedAtUtc == null && i.StockOnHand <= i.ReorderThreshold)
          .OrderBy(i => i.StockOnHand)
          .Take(take)
          .Select(i => new LowStockRow(i.Id, i.Name, i.Sku, i.StockOnHand, i.ReorderThreshold))
          .ToListAsync(ct);

    public Task<int> CountOpenJobsAsync(CancellationToken ct) => OpenJobs.CountAsync(ct);

    public Task<int> CountUnscheduledOpenJobsAsync(CancellationToken ct) =>
        OpenJobs.CountAsync(j => j.ScheduledStartUtc == null, ct);

    // Labour plus parts on jobs that reached a finished state in the window. Summed from
    // the line items rather than a stored total, because no invoice record exists yet --
    // when invoicing lands this should read from the invoice instead.
    public async Task<decimal> GetRevenueBetweenAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        var jobs = db.Jobs.Where(j =>
            j.ArchivedAtUtc == null
            && EarnedStatuses.Contains(j.Status)
            && j.UpdatedAtUtc >= fromUtc && j.UpdatedAtUtc < toUtc);

        var labour = await jobs.SelectMany(j => j.LaborLines).SumAsync(l => (decimal?)(l.Hours * l.Rate), ct) ?? 0m;
        var parts = await jobs.SelectMany(j => j.PartLines).SumAsync(p => (decimal?)(p.Quantity * p.UnitPrice), ct) ?? 0m;

        return labour + parts;
    }

    public Task<int> CountActiveCustomersAsync(CancellationToken ct) =>
        db.Customers.CountAsync(c => c.ArchivedAtUtc == null, ct);

    public Task<int> CountActiveVehiclesAsync(CancellationToken ct) =>
        db.Vehicles.CountAsync(v => v.ArchivedAtUtc == null, ct);
}
