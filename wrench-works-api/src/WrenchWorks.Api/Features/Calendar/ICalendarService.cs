using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Calendar;

public interface ICalendarService
{
    Task<List<Booking>> GetBookingsAsync(DateTime fromUtc, DateTime toUtc, Guid? zoneId, CancellationToken ct);
    Task<Booking> CreateBookingAsync(CreateBookingRequest request, CancellationToken ct);
    Task<Booking> UpdateBookingAsync(Guid id, UpdateBookingRequest request, CancellationToken ct);
    Task<Booking> UpdateBookingStatusAsync(Guid id, UpdateBookingStatusRequest request, CancellationToken ct);
    Task<Booking> MoveBookingAsync(Guid id, MoveBookingRequest request, CancellationToken ct);
    Task DeleteBookingAsync(Guid id, CancellationToken ct);
}
