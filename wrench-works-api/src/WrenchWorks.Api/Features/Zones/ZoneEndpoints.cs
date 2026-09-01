using Microsoft.AspNetCore.Http.HttpResults;

namespace WrenchWorks.Api.Features.Zones;

public static class ZoneEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/zones").WithTags("Zones").RequireAuthorization();

        group.MapGet("/", ListAsync).RequireAuthorization("calendar.view");
        group.MapPost("/", CreateAsync).RequireAuthorization("settings.manage");
        group.MapPut("/{id:guid}", UpdateAsync).RequireAuthorization("settings.manage");
        group.MapDelete("/{id:guid}", DeleteAsync).RequireAuthorization("settings.manage");
    }

    private static async Task<Ok<List<ZoneDto>>> ListAsync(IZoneService zones, CancellationToken ct) =>
        TypedResults.Ok(await zones.ListAsync(ct));

    private static async Task<Created<ZoneDto>> CreateAsync(
        CreateZoneRequest request, IZoneService zones, CancellationToken ct)
    {
        var zone = await zones.CreateAsync(request, ct);
        return TypedResults.Created($"/api/zones/{zone.Id}", zone);
    }

    private static async Task<Ok<ZoneDto>> UpdateAsync(
        Guid id, UpdateZoneRequest request, IZoneService zones, CancellationToken ct) =>
        TypedResults.Ok(await zones.UpdateAsync(id, request, ct));

    private static async Task<NoContent> DeleteAsync(Guid id, IZoneService zones, CancellationToken ct)
    {
        await zones.DeleteAsync(id, ct);
        return TypedResults.NoContent();
    }
}
