using Microsoft.AspNetCore.Http.HttpResults;
using WrenchWorks.Api.Features.Common;

namespace WrenchWorks.Api.Features.Vehicles;

public static class VehicleEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/vehicles").WithTags("Vehicles").RequireAuthorization();

        group.MapGet("/search", SearchAsync).RequireAuthorization("vehicles.view");
        group.MapPost("/", CreateAsync).RequireAuthorization("vehicles.manage");
        group.MapPut("/{id:guid}", UpdateAsync).RequireAuthorization("vehicles.manage");
        group.MapGet("/{id:guid}", GetAsync).RequireAuthorization("vehicles.view");
        group.MapGet("/{id:guid}/history", GetHistoryAsync).RequireAuthorization("vehicles.view");
        group.MapDelete("/{id:guid}", DeleteAsync).RequireAuthorization("vehicles.manage");
        group.MapPost("/{id:guid}/archive", ArchiveAsync).RequireAuthorization("vehicles.manage");
        group.MapPost("/{id:guid}/unarchive", UnarchiveAsync).RequireAuthorization("vehicles.manage");
    }

    private static async Task<Ok<List<VehicleSearchResultDto>>> SearchAsync(IVehicleService svc, string q, CancellationToken ct) =>
        TypedResults.Ok(await svc.SearchAsync(q, ct));

    private static async Task<NoContent> DeleteAsync(IVehicleService svc, Guid id, CancellationToken ct)
    {
        await svc.DeleteAsync(id, ct);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<ArchiveResultDto>> ArchiveAsync(IVehicleService svc, Guid id, CancellationToken ct) =>
        TypedResults.Ok(await svc.ArchiveAsync(id, ct));

    private static async Task<Ok<ArchiveResultDto>> UnarchiveAsync(IVehicleService svc, Guid id, CancellationToken ct) =>
        TypedResults.Ok(await svc.UnarchiveAsync(id, ct));

    private static async Task<Created<VehicleDto>> CreateAsync(IVehicleService svc, CreateVehicleRequest request, CancellationToken ct)
    {
        var result = await svc.CreateAsync(request, ct);
        return TypedResults.Created($"/api/vehicles/{result.Id}", result);
    }

    private static async Task<Ok<VehicleDto>> UpdateAsync(IVehicleService svc, Guid id, UpdateVehicleRequest request, CancellationToken ct) =>
        TypedResults.Ok(await svc.UpdateAsync(id, request, ct));

    private static async Task<Ok<VehicleDto>> GetAsync(IVehicleService svc, Guid id, CancellationToken ct) =>
        TypedResults.Ok(await svc.GetAsync(id, ct));

    private static async Task<Ok<List<VehicleHistoryItemDto>>> GetHistoryAsync(IVehicleService svc, Guid id, CancellationToken ct) =>
        TypedResults.Ok(await svc.GetHistoryAsync(id, ct));
}
