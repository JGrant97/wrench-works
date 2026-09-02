using Microsoft.AspNetCore.Http.HttpResults;

namespace WrenchWorks.Api.Features.Calendar;

public interface ICalendarEndpointHandler
{
    Task<Ok<List<BookingDto>>> GetBookingsAsync(GetBookingsQuery query, CancellationToken ct);
    Task<Created<BookingDto>> CreateBookingAsync(CreateBookingRequest request, CancellationToken ct);
    Task<Ok<BookingActionResultDto>> UpdateBookingAsync(Guid id, UpdateBookingRequest request, CancellationToken ct);
    Task<Ok<BookingStatusDto>> UpdateBookingStatusAsync(Guid id, UpdateBookingStatusRequest request, CancellationToken ct);
    Task<Ok<BookingActionResultDto>> MoveBookingAsync(Guid id, MoveBookingRequest request, CancellationToken ct);
    Task<NoContent> DeleteBookingAsync(Guid id, CancellationToken ct);
}
