using WrenchWorks.Api.Middleware;
using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Catalogue;

public class CatalogueService(ICatalogueRepository repository) : ICatalogueService
{
    public Task<List<VehicleMake>> GetMakesAsync(CancellationToken ct) =>
        repository.GetActiveMakesWithModelsAsync(ct);

    public async Task<List<VehicleModel>> GetModelsAsync(Guid makeId, CancellationToken ct)
    {
        if (!await repository.MakeExistsAsync(makeId, ct))
            throw new NotFoundException("Make not found");

        return await repository.GetActiveModelsAsync(makeId, ct);
    }

    /// <summary>
    /// The union of every active variant's year range for this model, flattened to a
    /// descending list of individual years. A model with no variants returns an empty
    /// list, which the UI must present as "not catalogued yet" rather than an error.
    /// </summary>
    public async Task<List<int>> GetYearsAsync(Guid modelId, CancellationToken ct)
    {
        if (!await repository.ModelExistsAsync(modelId, ct))
            throw new NotFoundException("Model not found");

        var ranges = await repository.GetVariantYearRangesAsync(modelId, ct);

        return ranges
            .SelectMany(r => Enumerable.Range(r.YearFrom, Math.Max(0, r.YearTo - r.YearFrom + 1)))
            .Distinct()
            .OrderByDescending(y => y)
            .ToList();
    }

    /// <summary>Variants whose production range contains the requested year.</summary>
    public async Task<List<VehicleVariant>> GetVariantsAsync(Guid modelId, int? year, CancellationToken ct)
    {
        if (!await repository.ModelExistsAsync(modelId, ct))
            throw new NotFoundException("Model not found");

        return await repository.GetActiveVariantsAsync(modelId, year, ct);
    }

    /// <summary>
    /// One variant by id, with its model and make. Returns inactive variants too: a
    /// vehicle may hold a retired variant, and an edit form must still be able to show
    /// what that vehicle is rather than silently blanking it.
    /// </summary>
    public async Task<VehicleVariant> GetVariantAsync(Guid variantId, CancellationToken ct) =>
        await repository.FindVariantWithModelAndMakeAsync(variantId, ct)
            ?? throw new NotFoundException("Variant not found");

    public Task<List<VehicleColour>> GetColoursAsync(CancellationToken ct) =>
        repository.GetActiveColoursAsync(ct);
}
