using Microsoft.AspNetCore.Http.HttpResults;

namespace WrenchWorks.Api.Features.Catalogue;

/// <summary>
/// Read-only vehicle catalogue used by the make → model → year → variant cascade.
///
/// Every endpoint returns ONLY valid next options, so an invalid combination is
/// unreachable rather than rejected after the fact. There are deliberately no write
/// endpoints: the catalogue is global reference data shared by every business, so a
/// tenant-facing write would let one workshop change what every other workshop sees
/// (the trap InventoryCategory already fell into). Curation happens through the vPIC
/// importer and VehicleCatalogueSeeder.
/// </summary>
public static class CatalogueEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/catalogue").WithTags("Catalogue").RequireAuthorization();

        group.MapGet("/makes", GetMakesAsync).RequireAuthorization("vehicles.view");
        group.MapGet("/makes/{makeId:guid}/models", GetModelsAsync).RequireAuthorization("vehicles.view");
        group.MapGet("/models/{modelId:guid}/years", GetYearsAsync).RequireAuthorization("vehicles.view");
        group.MapGet("/models/{modelId:guid}/variants", GetVariantsAsync).RequireAuthorization("vehicles.view");
        group.MapGet("/variants/{variantId:guid}", GetVariantAsync).RequireAuthorization("vehicles.view");
        group.MapGet("/colours", GetColoursAsync).RequireAuthorization("vehicles.view");
    }

    private static async Task<Ok<List<CatalogueMakeDto>>> GetMakesAsync(ICatalogueService svc, CancellationToken ct) =>
        TypedResults.Ok(await svc.GetMakesAsync(ct));

    private static async Task<Ok<List<CatalogueModelDto>>> GetModelsAsync(ICatalogueService svc, Guid makeId, CancellationToken ct) =>
        TypedResults.Ok(await svc.GetModelsAsync(makeId, ct));

    private static async Task<Ok<List<int>>> GetYearsAsync(ICatalogueService svc, Guid modelId, CancellationToken ct) =>
        TypedResults.Ok(await svc.GetYearsAsync(modelId, ct));

    private static async Task<Ok<List<CatalogueVariantDto>>> GetVariantsAsync(ICatalogueService svc, Guid modelId, int? year, CancellationToken ct) =>
        TypedResults.Ok(await svc.GetVariantsAsync(modelId, year, ct));

    private static async Task<Ok<CatalogueVariantDetailDto>> GetVariantAsync(ICatalogueService svc, Guid variantId, CancellationToken ct) =>
        TypedResults.Ok(await svc.GetVariantAsync(variantId, ct));

    private static async Task<Ok<List<CatalogueColourDto>>> GetColoursAsync(ICatalogueService svc, CancellationToken ct) =>
        TypedResults.Ok(await svc.GetColoursAsync(ct));
}
