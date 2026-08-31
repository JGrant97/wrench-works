using Microsoft.EntityFrameworkCore;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Catalogue;

// DTOs
public record CatalogueMakeDto(Guid Id, string Name);
public record CatalogueModelDto(Guid Id, string Name);
public record CatalogueVariantDto(
    Guid Id, string Label, int YearFrom, int YearTo,
    string? Trim, string? BodyStyle,
    decimal? EngineDisplacementL, int? EngineCylinders,
    string FuelType, string Transmission, string? DriveType, string Market);
/// <summary>
/// A single variant with its place in the cascade (make and model) attached.
///
/// This exists so an edit form holding only a VariantId can rebuild the whole
/// make -> model -> year -> facet selection without the client guessing. Without it the
/// picker cannot show what a vehicle already is.
/// </summary>
public record CatalogueVariantDetailDto(
    Guid Id, Guid ModelId, string ModelName, Guid MakeId, string MakeName,
    string Label, int YearFrom, int YearTo,
    string? Trim, string? BodyStyle,
    decimal? EngineDisplacementL, int? EngineCylinders,
    string FuelType, string Transmission, string? DriveType, string Market);

public record CatalogueColourDto(Guid Id, string Name, string? HexCode);

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

        group.MapGet("/makes", GetMakesAsync).RequireAuthorization("vehicles.view")
             .Produces<List<CatalogueMakeDto>>();
        group.MapGet("/makes/{makeId:guid}/models", GetModelsAsync).RequireAuthorization("vehicles.view")
             .Produces<List<CatalogueModelDto>>();
        group.MapGet("/models/{modelId:guid}/years", GetYearsAsync).RequireAuthorization("vehicles.view")
             .Produces<List<int>>();
        group.MapGet("/models/{modelId:guid}/variants", GetVariantsAsync).RequireAuthorization("vehicles.view")
             .Produces<List<CatalogueVariantDto>>();
        group.MapGet("/variants/{variantId:guid}", GetVariantAsync).RequireAuthorization("vehicles.view")
             .Produces<CatalogueVariantDetailDto>();
        group.MapGet("/colours", GetColoursAsync).RequireAuthorization("vehicles.view")
             .Produces<List<CatalogueColourDto>>();
    }

    /// <summary>Only makes that actually have models — an empty make is a dead end in the cascade.</summary>
    private static async Task<IResult> GetMakesAsync(AppDbContext db, CancellationToken ct)
    {
        var makes = await db.VehicleMakes
            .Where(m => m.IsActive && m.Models.Any(mo => mo.IsActive))
            .OrderBy(m => m.Name)
            .Select(m => new CatalogueMakeDto(m.Id, m.Name))
            .ToListAsync(ct);

        return Results.Ok(makes);
    }

    private static async Task<IResult> GetModelsAsync(Guid makeId, AppDbContext db, CancellationToken ct)
    {
        var makeExists = await db.VehicleMakes.AnyAsync(m => m.Id == makeId, ct);
        if (!makeExists) throw new NotFoundException("Make not found");

        var models = await db.VehicleModels
            .Where(m => m.MakeId == makeId && m.IsActive)
            .OrderBy(m => m.Name)
            .Select(m => new CatalogueModelDto(m.Id, m.Name))
            .ToListAsync(ct);

        return Results.Ok(models);
    }

    /// <summary>
    /// The union of every active variant's year range for this model, flattened to a
    /// descending list of individual years. A model with no variants returns an empty
    /// list, which the UI must present as "not catalogued yet" rather than an error.
    /// </summary>
    private static async Task<IResult> GetYearsAsync(Guid modelId, AppDbContext db, CancellationToken ct)
    {
        var modelExists = await db.VehicleModels.AnyAsync(m => m.Id == modelId, ct);
        if (!modelExists) throw new NotFoundException("Model not found");

        var ranges = await db.VehicleVariants
            .Where(v => v.ModelId == modelId && v.IsActive)
            .Select(v => new { v.YearFrom, v.YearTo })
            .ToListAsync(ct);

        var years = ranges
            .SelectMany(r => Enumerable.Range(r.YearFrom, Math.Max(0, r.YearTo - r.YearFrom + 1)))
            .Distinct()
            .OrderByDescending(y => y)
            .ToList();

        return Results.Ok(years);
    }

    /// <summary>Variants whose production range contains the requested year.</summary>
    private static async Task<IResult> GetVariantsAsync(
        Guid modelId,
        AppDbContext db,
        int? year,
        CancellationToken ct)
    {
        var modelExists = await db.VehicleModels.AnyAsync(m => m.Id == modelId, ct);
        if (!modelExists) throw new NotFoundException("Model not found");

        var query = db.VehicleVariants.Where(v => v.ModelId == modelId && v.IsActive);

        if (year.HasValue)
            query = query.Where(v => v.YearFrom <= year.Value && v.YearTo >= year.Value);

        var variants = await query
            .OrderBy(v => v.Trim).ThenBy(v => v.BodyStyle)
            .ToListAsync(ct);

        var dtos = variants.Select(v => new CatalogueVariantDto(
            v.Id, v.Describe(), v.YearFrom, v.YearTo,
            v.Trim, v.BodyStyle, v.EngineDisplacementL, v.EngineCylinders,
            v.FuelType.ToString(), v.Transmission.ToString(), v.DriveType?.ToString(), v.Market.ToString()));

        return Results.Ok(dtos);
    }

    /// <summary>
    /// One variant by id, with its model and make. Returns inactive variants too: a
    /// vehicle may hold a retired variant, and an edit form must still be able to show
    /// what that vehicle is rather than silently blanking it.
    /// </summary>
    private static async Task<IResult> GetVariantAsync(Guid variantId, AppDbContext db, CancellationToken ct)
    {
        var variant = await db.VehicleVariants
            .Include(v => v.Model).ThenInclude(m => m.Make)
            .FirstOrDefaultAsync(v => v.Id == variantId, ct)
            ?? throw new NotFoundException("Variant not found");

        return Results.Ok(new CatalogueVariantDetailDto(
            variant.Id,
            variant.ModelId, variant.Model.Name,
            variant.Model.MakeId, variant.Model.Make.Name,
            variant.Describe(), variant.YearFrom, variant.YearTo,
            variant.Trim, variant.BodyStyle,
            variant.EngineDisplacementL, variant.EngineCylinders,
            variant.FuelType.ToString(), variant.Transmission.ToString(),
            variant.DriveType?.ToString(), variant.Market.ToString()));
    }

    private static async Task<IResult> GetColoursAsync(AppDbContext db, CancellationToken ct)
    {
        var colours = await db.VehicleColours
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new CatalogueColourDto(c.Id, c.Name, c.HexCode))
            .ToListAsync(ct);

        return Results.Ok(colours);
    }
}
