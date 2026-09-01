using Microsoft.AspNetCore.Http.HttpResults;
using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Calendar;

public static class CalendarEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/calendar").WithTags("Calendar").RequireAuthorization();

        group.MapGet("/bookings", GetBookingsAsync).RequireAuthorization("calendar.view");
        group.MapPost("/bookings", CreateBookingAsync).RequireAuthorization("calendar.edit");
        group.MapPut("/bookings/{id:guid}", UpdateBookingAsync).RequireAuthorization("calendar.edit");
        group.MapPut("/bookings/{id:guid}/move", MoveBookingAsync).RequireAuthorization("calendar.edit");
        group.MapPatch("/bookings/{id:guid}/status", UpdateBookingStatusAsync).RequireAuthorization("calendar.edit");
        group.MapDelete("/bookings/{id:guid}", DeleteBookingAsync).RequireAuthorization("calendar.edit");
    }

    private static async Task<Ok<List<BookingDto>>> GetBookingsAsync(ICalendarService svc, [AsParameters] GetBookingsQuery query, CancellationToken ct) =>
        TypedResults.Ok(await svc.GetBookingsAsync(query, ct));

    private static async Task<Created<BookingDto>> CreateBookingAsync(ICalendarService svc, CreateBookingRequest request, CancellationToken ct)
    {
        var result = await svc.CreateBookingAsync(request, ct);
        return TypedResults.Created($"/api/calendar/bookings/{result.Id}", result);
    }

    private static async Task<Ok<BookingActionResultDto>> UpdateBookingAsync(ICalendarService svc, Guid id, UpdateBookingRequest request, CancellationToken ct) =>
        TypedResults.Ok(await svc.UpdateBookingAsync(id, request, ct));

    private static async Task<Ok<BookingStatusDto>> UpdateBookingStatusAsync(ICalendarService svc, Guid id, UpdateBookingStatusRequest request, CancellationToken ct) =>
        TypedResults.Ok(await svc.UpdateBookingStatusAsync(id, request, ct));

    private static async Task<Ok<BookingActionResultDto>> MoveBookingAsync(ICalendarService svc, Guid id, MoveBookingRequest request, CancellationToken ct) =>
        TypedResults.Ok(await svc.MoveBookingAsync(id, request, ct));

    private static async Task<NoContent> DeleteBookingAsync(ICalendarService svc, Guid id, CancellationToken ct)
    {
        await svc.DeleteBookingAsync(id, ct);
        return TypedResults.NoContent();
    }
}
