using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Calendar;

public interface ICalendarRepository
{
    Task<List<Booking>> GetBookingsInRangeAsync(DateTime fromUtc, DateTime toUtc, Guid? zoneId, CancellationToken ct);
    Task<Booking?> FindAsync(Guid id, CancellationToken ct);
    Task<Zone?> FindZoneAsync(Guid zoneId, CancellationToken ct);
    Task<Customer?> FindCustomerAsync(Guid customerId, CancellationToken ct);
    Task<Vehicle?> FindVehicleAsync(Guid vehicleId, CancellationToken ct);
    Task<Job?> FindJobAsync(Guid jobId, CancellationToken ct);
    Task<List<Guid>> GetOverlappingBookingIdsAsync(Guid zoneId, DateTime startUtc, DateTime endUtc, Guid? excludeBookingId, CancellationToken ct);

    /// <summary>
    /// Runs <paramref name="work"/> inside a transaction holding a row lock on the zone.
    ///
    /// Conflict checking is read-then-write: without this, two simultaneous requests both
    /// see a free slot and both commit, putting two cars in one bay. A Postgres exclusion
    /// constraint would forbid ANY overlap, which is wrong here because a zone may have
    /// Capacity > 1 — so the count still has to run, it just must never run concurrently
    /// for the same bay. Locking the zone row serialises writes per bay and leaves
    /// different bays fully parallel.
    /// </summary>
    Task<T> WithZoneLockAsync<T>(Guid zoneId, Func<Task<T>> work, CancellationToken ct);

    void AddBooking(Booking booking);
    void AddJob(Job job);
    Task SaveChangesAsync(CancellationToken ct);
}
