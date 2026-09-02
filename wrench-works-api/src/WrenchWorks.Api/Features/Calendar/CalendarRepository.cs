using Microsoft.EntityFrameworkCore;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Calendar;

public class CalendarRepository(AppDbContext db) : ICalendarRepository
{
    public Task<List<Booking>> GetBookingsInRangeAsync(
        DateTime fromUtc, DateTime toUtc, Guid? zoneId, CancellationToken ct)
    {
        var query = db.Bookings
            .Include(b => b.Zone)
            .Include(b => b.Customer)
            .Include(b => b.Vehicle)
            .Where(b => b.StartUtc < toUtc && b.EndUtc > fromUtc)
            .Where(b => b.Status != BookingStatus.Cancelled);

        if (zoneId.HasValue)
            query = query.Where(b => b.ZoneId == zoneId.Value);

        return query.OrderBy(b => b.StartUtc).ToListAsync(ct);
    }

    public async Task<Booking?> FindAsync(Guid id, CancellationToken ct) =>
        await db.Bookings.FindAsync([id], ct);

    // Each lookup goes through its tenant-filtered DbSet, so another business's row is
    // simply not found rather than accepted.
    public async Task<Zone?> FindZoneAsync(Guid zoneId, CancellationToken ct) =>
        await db.Zones.FindAsync([zoneId], ct);

    public async Task<Customer?> FindCustomerAsync(Guid customerId, CancellationToken ct) =>
        await db.Customers.FindAsync([customerId], ct);

    public async Task<Vehicle?> FindVehicleAsync(Guid vehicleId, CancellationToken ct) =>
        await db.Vehicles.FindAsync([vehicleId], ct);

    public async Task<Job?> FindJobAsync(Guid jobId, CancellationToken ct) =>
        await db.Jobs.FindAsync([jobId], ct);

    // Read-then-write with no constraint behind it, so two simultaneous requests can both
    // pass and double-book a capacity-1 bay -- finding 7 in docs/review-findings.md. The
    // structural fix is a Postgres exclusion constraint over (ZoneId, time range).
    public Task<List<Guid>> GetOverlappingBookingIdsAsync(
        Guid zoneId, DateTime startUtc, DateTime endUtc, Guid? excludeBookingId, CancellationToken ct)
    {
        var query = db.Bookings
            .Where(b => b.ZoneId == zoneId)
            .Where(b => b.Status != BookingStatus.Cancelled)
            .Where(b => b.StartUtc < endUtc && b.EndUtc > startUtc);

        if (excludeBookingId.HasValue)
            query = query.Where(b => b.Id != excludeBookingId.Value);

        return query.Select(b => b.Id).ToListAsync(ct);
    }

    // SELECT ... FOR UPDATE takes a row lock on the zone and holds it until commit, so a
    // second request for the same bay blocks at the lock and then re-counts against the
    // row the first one wrote. Different zones take different locks and never block each
    // other. Everything work() saves joins this transaction, which also closes the create
    // path's two-save gap: the booking and its reverse job FK now commit together.
    public async Task<T> WithZoneLockAsync<T>(Guid zoneId, Func<Task<T>> work, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        await db.Database.ExecuteSqlAsync(
            $"""SELECT 1 FROM "Zones" WHERE "Id" = {zoneId} FOR UPDATE""", ct);

        var result = await work();

        await transaction.CommitAsync(ct);
        return result;
    }

    public void AddBooking(Booking booking) => db.Bookings.Add(booking);
    public void AddJob(Job job) => db.Jobs.Add(job);
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
