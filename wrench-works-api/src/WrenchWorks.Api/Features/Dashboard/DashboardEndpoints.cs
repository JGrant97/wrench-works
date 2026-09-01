using Microsoft.EntityFrameworkCore;
using WrenchWorks.Api.Auth;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Dashboard;

public record DashboardBookingDto(
    Guid Id, string Title, DateTime StartUtc, DateTime EndUtc,
    string CustomerName, string? VehicleDisplay, string ZoneName, string Status, Guid? JobId);

public record DashboardJobDto(
    Guid Id, string Title, string Status, string Priority,
    string CustomerName, string? VehicleDisplay, DateTime? ScheduledStartUtc);

public record LowStockItemDto(Guid Id, string Name, string? Sku, int StockOnHand, int ReorderThreshold);

public record StatusCountDto(string Status, int Count);

/// <summary>
/// Everything the opening screen needs, in one request.
///
/// Deliberately one endpoint rather than six: the dashboard is the first thing loaded
/// after login, and six round trips through the proxy would each pay the cookie→bearer
/// hop. It also keeps "what counts as today" decided in one place on the server.
/// </summary>
public record DashboardDto(
    IEnumerable<DashboardBookingDto> TodaysBookings,
    IEnumerable<DashboardJobDto> ActiveJobs,
    IEnumerable<StatusCountDto> JobsByStatus,
    IEnumerable<LowStockItemDto> LowStockItems,
    int OpenJobCount,
    int UnscheduledJobCount,
    decimal RevenueThisMonth,
    decimal RevenueLastMonth,
    int CustomerCount,
    int VehicleCount);

public static class DashboardEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/dashboard").WithTags("Dashboard").RequireAuthorization();

        group.MapGet("/", GetAsync).RequireAuthorization("jobs.view").Produces<DashboardDto>();
    }

    // List<T>, deliberately not T[]. On .NET 10 an array's .Contains() binds to
    // MemoryExtensions.Contains(ReadOnlySpan<T>, T), which EF Core cannot evaluate as a
    // query parameter — it throws "GenericArguments[1] ... violates the constraint of type
    // parameter TRet" at runtime and surfaces as a bare 500. A List picks Enumerable.Contains
    // and translates to SQL IN. Caught by DashboardTests; see CLAUDE.md.

    /// <summary>Statuses that mean "still in the workshop" rather than finished.</summary>
    private static readonly List<JobStatus> OpenStatuses =
        [JobStatus.Draft, JobStatus.Scheduled, JobStatus.InProgress, JobStatus.WaitingParts];

    /// <summary>Statuses at which the work is done and the money is real.</summary>
    private static readonly List<JobStatus> EarnedStatuses =
        [JobStatus.Completed, JobStatus.Invoiced, JobStatus.Closed];

    private static async Task<IResult> GetAsync(
        AppDbContext db,
        CurrentUserService currentUser,
        CancellationToken ct)
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

        return Results.Ok(new DashboardDto(
            todaysBookings,
            activeJobs,
            jobsByStatus.OrderBy(s => s.Status),
            lowStock,
            await openJobs.CountAsync(ct),
            await openJobs.CountAsync(j => j.ScheduledStartUtc == null, ct),
            await RevenueBetweenAsync(db, monthStart, monthStart.AddMonths(1), ct),
            await RevenueBetweenAsync(db, lastMonthStart, monthStart, ct),
            await db.Customers.CountAsync(c => c.ArchivedAtUtc == null, ct),
            await db.Vehicles.CountAsync(v => v.ArchivedAtUtc == null, ct)));
    }

    // Labour plus parts on jobs that reached a finished state in the window. Summed from
    // the line items rather than a stored total, because no invoice record exists yet —
    // when invoicing lands this should read from the invoice instead.
    //
    // Plain // rather than ///: the .NET 10 preview OpenAPI XML-comment source generator
    // fails with CS0673 on Task-returning methods carrying a <summary>. See CLAUDE.md.
    private static async Task<decimal> RevenueBetweenAsync(
        AppDbContext db, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
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
