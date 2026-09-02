namespace WrenchWorks.Api.Features.Calendar;

public static class CalendarEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/calendar").WithTags("Calendar").RequireAuthorization();

        group.MapGet("/bookings",
            ([AsParameters] GetBookingsQuery query, ICalendarEndpointHandler handler, CancellationToken ct) =>
                handler.GetBookingsAsync(query, ct))
            .RequireAuthorization("calendar.view");

        group.MapPost("/bookings",
            (CreateBookingRequest request, ICalendarEndpointHandler handler, CancellationToken ct) =>
                handler.CreateBookingAsync(request, ct))
            .RequireAuthorization("calendar.edit");

        group.MapPut("/bookings/{id:guid}",
            (Guid id, UpdateBookingRequest request, ICalendarEndpointHandler handler, CancellationToken ct) =>
                handler.UpdateBookingAsync(id, request, ct))
            .RequireAuthorization("calendar.edit");

        // Drag-to-move on the week grid sends less data than a full update, but shares the
        // conflict check and job cascade with it. See docs/bookings-crud.md.
        group.MapPut("/bookings/{id:guid}/move",
            (Guid id, MoveBookingRequest request, ICalendarEndpointHandler handler, CancellationToken ct) =>
                handler.MoveBookingAsync(id, request, ct))
            .RequireAuthorization("calendar.edit");

        group.MapPatch("/bookings/{id:guid}/status",
            (Guid id, UpdateBookingStatusRequest request, ICalendarEndpointHandler handler, CancellationToken ct) =>
                handler.UpdateBookingStatusAsync(id, request, ct))
            .RequireAuthorization("calendar.edit");

        group.MapDelete("/bookings/{id:guid}",
            (Guid id, ICalendarEndpointHandler handler, CancellationToken ct) =>
                handler.DeleteBookingAsync(id, ct))
            .RequireAuthorization("calendar.edit");
    }
}
