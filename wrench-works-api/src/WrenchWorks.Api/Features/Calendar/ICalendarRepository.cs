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

    void AddBooking(Booking booking);
    void AddJob(Job job);
    Task SaveChangesAsync(CancellationToken ct);
}
