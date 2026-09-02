using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Catalogue;

// A variant's production span. A repository read model, not an API DTO: only YearFrom and
// YearTo are needed to flatten the year list, so loading whole variants would be waste.
public record VariantYearRange(int YearFrom, int YearTo);

/// <summary>
/// Data access for the shared vehicle catalogue. These tables are global reference data,
/// not tenant-scoped, so nothing here is filtered by BusinessId.
/// </summary>
public interface ICatalogueRepository
{
    Task<List<VehicleMake>> GetActiveMakesWithModelsAsync(CancellationToken ct);
    Task<bool> MakeExistsAsync(Guid makeId, CancellationToken ct);
    Task<List<VehicleModel>> GetActiveModelsAsync(Guid makeId, CancellationToken ct);
    Task<bool> ModelExistsAsync(Guid modelId, CancellationToken ct);
    Task<List<VariantYearRange>> GetVariantYearRangesAsync(Guid modelId, CancellationToken ct);
    Task<List<VehicleVariant>> GetActiveVariantsAsync(Guid modelId, int? year, CancellationToken ct);
    Task<VehicleVariant?> FindVariantWithModelAndMakeAsync(Guid variantId, CancellationToken ct);
    Task<List<VehicleColour>> GetActiveColoursAsync(CancellationToken ct);
}
