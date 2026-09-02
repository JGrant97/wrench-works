namespace WrenchWorks.Api.Features.Vehicles;

public static class VehicleEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/vehicles").WithTags("Vehicles").RequireAuthorization();

        group.MapGet("/search",
            (string q, IVehicleEndpointHandler handler, CancellationToken ct) =>
                handler.SearchAsync(q, ct))
            .RequireAuthorization("vehicles.view");

        group.MapPost("/",
            (CreateVehicleRequest request, IVehicleEndpointHandler handler, CancellationToken ct) =>
                handler.CreateAsync(request, ct))
            .RequireAuthorization("vehicles.manage");

        group.MapPut("/{id:guid}",
            (Guid id, UpdateVehicleRequest request, IVehicleEndpointHandler handler, CancellationToken ct) =>
                handler.UpdateAsync(id, request, ct))
            .RequireAuthorization("vehicles.manage");

        group.MapGet("/{id:guid}",
            (Guid id, IVehicleEndpointHandler handler, CancellationToken ct) =>
                handler.GetAsync(id, ct))
            .RequireAuthorization("vehicles.view");

        group.MapGet("/{id:guid}/history",
            (Guid id, IVehicleEndpointHandler handler, CancellationToken ct) =>
                handler.GetHistoryAsync(id, ct))
            .RequireAuthorization("vehicles.view");

        group.MapDelete("/{id:guid}",
            (Guid id, IVehicleEndpointHandler handler, CancellationToken ct) =>
                handler.DeleteAsync(id, ct))
            .RequireAuthorization("vehicles.manage");

        group.MapPost("/{id:guid}/archive",
            (Guid id, IVehicleEndpointHandler handler, CancellationToken ct) =>
                handler.ArchiveAsync(id, ct))
            .RequireAuthorization("vehicles.manage");

        group.MapPost("/{id:guid}/unarchive",
            (Guid id, IVehicleEndpointHandler handler, CancellationToken ct) =>
                handler.UnarchiveAsync(id, ct))
            .RequireAuthorization("vehicles.manage");
    }
}
