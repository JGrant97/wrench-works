using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.HttpResults;
using WrenchWorks.Api.Auth;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Dashboard;

public class DashboardService(AppDbContext db, CurrentUserService currentUser) : IDashboardService
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

    public async Task<DashboardDto> GetAsync(CancellationToken ct)
    {
        // "Today" is UTC, matching every stored timestamp. Business.Timezone is not read
        // anywhere yet — see the timezone note in CLAUDE.md; when that is settled this is
        // one of the places that has to change.
        var todayStart = DateTime.UtcNow.Date;
        var todayEnd = todayStart.AddDays(1);

        var monthStart = new DateTime(todayStart.Year, todayStart.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var lastMonthStart = monthStart.AddMonths(-1);

        var todaysBookings = await db.Bookings
            .Where(b => b.Status != BookingStatus.Cancelled
                        && b.StartUtc < todayEnd && b.EndUtc > todayStart)
            .OrderBy(b => b.StartUtc)
            .Select(b => new DashboardBookingDto(
                b.Id, b.Title, b.StartUtc, b.EndUtc,
                b.Customer.Name,
                b.Vehicle.DisplayName,
                b.Zone.Name,
                b.Status.ToString(),
                b.JobId))
            .ToListAsync(ct);

        var openJobs = db.Jobs.Where(j => j.ArchivedAtUtc == null && OpenStatuses.Contains(j.Status));

        var activeJobs = await openJobs
            .OrderBy(j => j.ScheduledStartUtc == null)
            .ThenBy(j => j.ScheduledStartUtc)
            .Take(10)
            .Select(j => new DashboardJobDto(
                j.Id, j.Title, j.Status.ToString(), j.Priority.ToString(),
                j.Customer.Name, j.Vehicle.DisplayName, j.ScheduledStartUtc))
            .ToListAsync(ct);

        // Group to the enum in SQL, then name it in memory. Calling ToString() on the
        // group key inside the projection does not translate.
        var statusGroups = await db.Jobs
            .Where(j => j.ArchivedAtUtc == null)
            .GroupBy(j => j.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var jobsByStatus = statusGroups
            .Select(g => new StatusCountDto(g.Status.ToString(), g.Count))
            .ToList();

        // Inventory is a plan feature. Without it the section is simply absent rather than
        // shown empty, which would read as "you have no low stock" — the opposite of true.
        var lowStock = currentUser.HasFeature("inventory")
            ? await db.InventoryItems
                .Where(i => i.ArchivedAtUtc == null && i.StockOnHand <= i.ReorderThreshold)
                .OrderBy(i => i.StockOnHand)
                .Take(10)
                .Select(i => new LowStockItemDto(i.Id, i.Name, i.Sku, i.StockOnHand, i.ReorderThreshold))
                .ToListAsync(ct)
            : [];

        return new DashboardDto(
            todaysBookings,
            activeJobs,
            jobsByStatus.OrderBy(s => s.Status),
            lowStock,
            await openJobs.CountAsync(ct),
            await openJobs.CountAsync(j => j.ScheduledStartUtc == null, ct),
            await RevenueBetweenAsync(db, monthStart, monthStart.AddMonths(1), ct),
            await RevenueBetweenAsync(db, lastMonthStart, monthStart, ct),
            await db.Customers.CountAsync(c => c.ArchivedAtUtc == null, ct),
            await db.Vehicles.CountAsync(v => v.ArchivedAtUtc == null, ct));
    }

    // Labour plus parts on jobs that reached a finished state in the window. Summed from
    // the line items rather than a stored total, because no invoice record exists yet —
    // when invoicing lands this should read from the invoice instead.
    //
    // Plain // rather than ///: the .NET 10 preview OpenAPI XML-comment source generator
    // fails with CS0673 on Task-returning methods carrying a <summary>. See CLAUDE.md.
    private static async Task<decimal> RevenueBetweenAsync(AppDbContext db, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        var jobs = db.Jobs.Where(j =>
            j.ArchivedAtUtc == null
            && EarnedStatuses.Contains(j.Status)
            && j.UpdatedAtUtc >= fromUtc && j.UpdatedAtUtc < toUtc);

        var labour = await jobs.SelectMany(j => j.LaborLines).SumAsync(l => (decimal?)(l.Hours * l.Rate), ct) ?? 0m;
        var parts = await jobs.SelectMany(j => j.PartLines).SumAsync(p => (decimal?)(p.Quantity * p.UnitPrice), ct) ?? 0m;

        return labour + parts;
    }
}
