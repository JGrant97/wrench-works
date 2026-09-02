using Microsoft.AspNetCore.Http.HttpResults;
using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Calendar;

public class CalendarEndpointHandler(ICalendarService service) : ICalendarEndpointHandler
{
    // One display string for both the list and create, so a booking cannot read one way
    // in the grid and another in the response that created it.
    private static string VehicleDisplay(Vehicle v) =>
        (v.DisplayName ?? "") + (v.Registration != null ? " " + v.Registration : "");

    private static BookingDto ToDto(Booking b) =>
        new(b.Id, b.ZoneId, b.Zone.Name, b.Zone.Color,
            b.CustomerId, b.Customer.Name,
            b.VehicleId, VehicleDisplay(b.Vehicle),
            b.Title, b.StartUtc, b.EndUtc, b.Notes,
            b.Status.ToString(), b.JobId, b.CreatedAtUtc);

    public async Task<Ok<List<BookingDto>>> GetBookingsAsync(GetBookingsQuery query, CancellationToken ct)
    {
        var bookings = await service.GetBookingsAsync(query.From, query.To, query.ZoneId, ct);
        return TypedResults.Ok(bookings.Select(ToDto).ToList());
    }

    public async Task<Created<BookingDto>> CreateBookingAsync(CreateBookingRequest request, CancellationToken ct)
    {
        var booking = await service.CreateBookingAsync(request, ct);
        return TypedResults.Created($"/api/calendar/bookings/{booking.Id}", ToDto(booking));
    }

    // The message is presentation, so it is written here rather than in the service.
    public async Task<Ok<BookingActionResultDto>> UpdateBookingAsync(
        Guid id, UpdateBookingRequest request, CancellationToken ct)
    {
        await service.UpdateBookingAsync(id, request, ct);
        return TypedResults.Ok(new BookingActionResultDto("Booking updated"));
    }

    public async Task<Ok<BookingStatusDto>> UpdateBookingStatusAsync(
        Guid id, UpdateBookingStatusRequest request, CancellationToken ct)
    {
        var booking = await service.UpdateBookingStatusAsync(id, request, ct);
        return TypedResults.Ok(new BookingStatusDto(booking.Id, booking.Status.ToString()));
    }

    public async Task<Ok<BookingActionResultDto>> MoveBookingAsync(
        Guid id, MoveBookingRequest request, CancellationToken ct)
    {
        await service.MoveBookingAsync(id, request, ct);
        return TypedResults.Ok(new BookingActionResultDto("Booking moved successfully"));
    }

    public async Task<NoContent> DeleteBookingAsync(Guid id, CancellationToken ct)
    {
        await service.DeleteBookingAsync(id, ct);
        return TypedResults.NoContent();
    }
}
