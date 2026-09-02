using Microsoft.AspNetCore.Http.HttpResults;

namespace WrenchWorks.Api.Features.Catalogue;

// The HTTP layer for the catalogue cascade: the only place catalogue entities become DTOs.
public interface ICatalogueEndpointHandler
{
    Task<Ok<List<CatalogueMakeDto>>> GetMakesAsync(CancellationToken ct);
    Task<Ok<List<CatalogueModelDto>>> GetModelsAsync(Guid makeId, CancellationToken ct);
    Task<Ok<List<int>>> GetYearsAsync(Guid modelId, CancellationToken ct);
    Task<Ok<List<CatalogueVariantDto>>> GetVariantsAsync(Guid modelId, int? year, CancellationToken ct);
    Task<Ok<CatalogueVariantDetailDto>> GetVariantAsync(Guid variantId, CancellationToken ct);
    Task<Ok<List<CatalogueColourDto>>> GetColoursAsync(CancellationToken ct);
}
