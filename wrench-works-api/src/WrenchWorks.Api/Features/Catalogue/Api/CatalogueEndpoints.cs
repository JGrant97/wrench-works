namespace WrenchWorks.Api.Features.Catalogue;

/// <summary>
/// Routes only. Each step of the make -> model -> year -> variant cascade returns just the
/// options that still lead to a real vehicle, so an invalid combination is unreachable
/// rather than rejected. See docs/vehicle-catalogue.md.
/// </summary>
public static class CatalogueEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/catalogue").WithTags("Catalogue").RequireAuthorization();

        group.MapGet("/makes",
            (ICatalogueEndpointHandler handler, CancellationToken ct) =>
                handler.GetMakesAsync(ct))
            .RequireAuthorization("vehicles.view");

        group.MapGet("/makes/{makeId:guid}/models",
            (Guid makeId, ICatalogueEndpointHandler handler, CancellationToken ct) =>
                handler.GetModelsAsync(makeId, ct))
            .RequireAuthorization("vehicles.view");

        group.MapGet("/models/{modelId:guid}/years",
            (Guid modelId, ICatalogueEndpointHandler handler, CancellationToken ct) =>
                handler.GetYearsAsync(modelId, ct))
            .RequireAuthorization("vehicles.view");

        group.MapGet("/models/{modelId:guid}/variants",
            (Guid modelId, int? year, ICatalogueEndpointHandler handler, CancellationToken ct) =>
                handler.GetVariantsAsync(modelId, year, ct))
            .RequireAuthorization("vehicles.view");

        group.MapGet("/variants/{variantId:guid}",
            (Guid variantId, ICatalogueEndpointHandler handler, CancellationToken ct) =>
                handler.GetVariantAsync(variantId, ct))
            .RequireAuthorization("vehicles.view");

        group.MapGet("/colours",
            (ICatalogueEndpointHandler handler, CancellationToken ct) =>
                handler.GetColoursAsync(ct))
            .RequireAuthorization("vehicles.view");
    }
}
