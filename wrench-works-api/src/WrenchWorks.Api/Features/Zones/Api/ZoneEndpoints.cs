namespace WrenchWorks.Api.Features.Zones;

/// <summary>
/// Routes only. Every route resolves IZoneEndpointHandler from DI and calls one method on
/// it, so there is nothing here to unit test and nowhere for logic to accumulate.
/// </summary>
public static class ZoneEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/zones").WithTags("Zones").RequireAuthorization();

        group.MapGet("/",
            (IZoneEndpointHandler handler, CancellationToken ct) =>
                handler.ListAsync(ct))
            .RequireAuthorization("calendar.view");

        group.MapPost("/",
            (CreateZoneRequest request, IZoneEndpointHandler handler, CancellationToken ct) =>
                handler.CreateAsync(request, ct))
            .RequireAuthorization("settings.manage");

        group.MapPut("/{id:guid}",
            (Guid id, UpdateZoneRequest request, IZoneEndpointHandler handler, CancellationToken ct) =>
                handler.UpdateAsync(id, request, ct))
            .RequireAuthorization("settings.manage");

        group.MapDelete("/{id:guid}",
            (Guid id, IZoneEndpointHandler handler, CancellationToken ct) =>
                handler.DeleteAsync(id, ct))
            .RequireAuthorization("settings.manage");
    }
}
