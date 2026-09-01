using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Calendar;

// The Calendar slice behind an interface: the endpoints become a thin HTTP shell.
// Methods return DTOs, not IResult -- failures are thrown and mapped by
// ErrorHandlingMiddleware, so nothing here needs to know about status codes.
public interface ICalendarService
{
    Task<List<BookingDto>> GetBookingsAsync([AsParameters] GetBookingsQuery query, CancellationToken ct);
    Task<BookingDto> CreateBookingAsync(CreateBookingRequest request, CancellationToken ct);
    Task<BookingActionResultDto> UpdateBookingAsync(Guid id, UpdateBookingRequest request, CancellationToken ct);
    Task<BookingStatusDto> UpdateBookingStatusAsync(Guid id, UpdateBookingStatusRequest request, CancellationToken ct);
    Task<BookingActionResultDto> MoveBookingAsync(Guid id, MoveBookingRequest request, CancellationToken ct);
    Task DeleteBookingAsync(Guid id, CancellationToken ct);
}
