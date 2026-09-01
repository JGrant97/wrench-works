using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.HttpResults;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Catalogue;

public class CatalogueService(AppDbContext db) : ICatalogueService
{
    /// <summary>Only makes that actually have models — an empty make is a dead end in the cascade.</summary>
    public async Task<List<CatalogueMakeDto>> GetMakesAsync(CancellationToken ct)
    {
        var makes = await db.VehicleMakes
            .Where(m => m.IsActive && m.Models.Any(mo => mo.IsActive))
            .OrderBy(m => m.Name)
            .Select(m => new CatalogueMakeDto(m.Id, m.Name))
            .ToListAsync(ct);

        return makes;
    }

    public async Task<List<CatalogueModelDto>> GetModelsAsync(Guid makeId, CancellationToken ct)
    {
        var makeExists = await db.VehicleMakes.AnyAsync(m => m.Id == makeId, ct);
        if (!makeExists) throw new NotFoundException("Make not found");

        var models = await db.VehicleModels
            .Where(m => m.MakeId == makeId && m.IsActive)
            .OrderBy(m => m.Name)
            .Select(m => new CatalogueModelDto(m.Id, m.Name))
            .ToListAsync(ct);

        return models;
    }

    /// <summary>
    /// The union of every active variant's year range for this model, flattened to a
    /// descending list of individual years. A model with no variants returns an empty
    /// list, which the UI must present as "not catalogued yet" rather than an error.
    /// </summary>
    public async Task<List<int>> GetYearsAsync(Guid modelId, CancellationToken ct)
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

        return years;
    }

    /// <summary>Variants whose production range contains the requested year.</summary>
    public async Task<List<CatalogueVariantDto>> GetVariantsAsync(Guid modelId, int? year, CancellationToken ct)
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
            v.FuelType.ToString(), v.Transmission.ToString(), v.DriveType?.ToString(), v.Market.ToString())).ToList();

        return dtos;
    }

    /// <summary>
    /// One variant by id, with its model and make. Returns inactive variants too: a
    /// vehicle may hold a retired variant, and an edit form must still be able to show
    /// what that vehicle is rather than silently blanking it.
    /// </summary>
    public async Task<CatalogueVariantDetailDto> GetVariantAsync(Guid variantId, CancellationToken ct)
    {
        var variant = await db.VehicleVariants
            .Include(v => v.Model).ThenInclude(m => m.Make)
            .FirstOrDefaultAsync(v => v.Id == variantId, ct)
            ?? throw new NotFoundException("Variant not found");

        return new CatalogueVariantDetailDto(
            variant.Id,
            variant.ModelId, variant.Model.Name,
            variant.Model.MakeId, variant.Model.Make.Name,
            variant.Describe(), variant.YearFrom, variant.YearTo,
            variant.Trim, variant.BodyStyle,
            variant.EngineDisplacementL, variant.EngineCylinders,
            variant.FuelType.ToString(), variant.Transmission.ToString(),
            variant.DriveType?.ToString(), variant.Market.ToString());
    }

    public async Task<List<CatalogueColourDto>> GetColoursAsync(CancellationToken ct)
    {
        var colours = await db.VehicleColours
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new CatalogueColourDto(c.Id, c.Name, c.HexCode))
            .ToListAsync(ct);

        return colours;
    }
}
