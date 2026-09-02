using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Catalogue;

/// <summary>
/// The cascade rules: each step verifies its parent exists and returns only options that
/// still lead to a real vehicle. Returns entities; CatalogueEndpointHandler maps to DTOs.
/// </summary>
public interface ICatalogueService
{
    Task<List<VehicleMake>> GetMakesAsync(CancellationToken ct);
    Task<List<VehicleModel>> GetModelsAsync(Guid makeId, CancellationToken ct);
    Task<List<int>> GetYearsAsync(Guid modelId, CancellationToken ct);
    Task<List<VehicleVariant>> GetVariantsAsync(Guid modelId, int? year, CancellationToken ct);
    Task<VehicleVariant> GetVariantAsync(Guid variantId, CancellationToken ct);
    Task<List<VehicleColour>> GetColoursAsync(CancellationToken ct);
}
