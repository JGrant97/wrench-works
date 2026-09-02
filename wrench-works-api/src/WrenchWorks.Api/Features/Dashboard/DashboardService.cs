using WrenchWorks.Api.Auth;

namespace WrenchWorks.Api.Features.Dashboard;

public class DashboardService(IDashboardRepository repository, CurrentUserService currentUser) : IDashboardService
{
    public async Task<DashboardSnapshot> GetAsync(CancellationToken ct)
    {
        // "Today" is UTC, matching every stored timestamp. Business.Timezone is not read
        // anywhere yet -- see the timezone note in CLAUDE.md; when that is settled this is
        // one of the places that has to change.
        var todayStart = DateTime.UtcNow.Date;
        var todayEnd = todayStart.AddDays(1);

        var monthStart = new DateTime(todayStart.Year, todayStart.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var lastMonthStart = monthStart.AddMonths(-1);

        // Inventory is a plan feature. Without it the section is simply absent rather than
        // shown empty, which would read as "you have no low stock" -- the opposite of true.
        var lowStock = currentUser.HasFeature("inventory")
            ? await repository.GetLowStockAsync(10, ct)
            : [];

        return new DashboardSnapshot(
            await repository.GetBookingsOverlappingAsync(todayStart, todayEnd, ct),
            await repository.GetActiveJobsAsync(10, ct),
            await repository.GetJobCountsByStatusAsync(ct),
            lowStock,
            await repository.CountOpenJobsAsync(ct),
            await repository.CountUnscheduledOpenJobsAsync(ct),
            await repository.GetRevenueBetweenAsync(monthStart, monthStart.AddMonths(1), ct),
            await repository.GetRevenueBetweenAsync(lastMonthStart, monthStart, ct),
            await repository.CountActiveCustomersAsync(ct),
            await repository.CountActiveVehiclesAsync(ct));
    }
}
