namespace WrenchWorks.Api.Features.Catalogue;

// The Catalogue slice behind an interface: the endpoints become a thin HTTP shell.
// Methods return DTOs, not IResult -- failures are thrown and mapped by
// ErrorHandlingMiddleware, so nothing here needs to know about status codes.
public interface ICatalogueService
{
    Task<List<CatalogueMakeDto>> GetMakesAsync(CancellationToken ct);
    Task<List<CatalogueModelDto>> GetModelsAsync(Guid makeId, CancellationToken ct);
    Task<List<int>> GetYearsAsync(Guid modelId, CancellationToken ct);
    Task<List<CatalogueVariantDto>> GetVariantsAsync(Guid modelId, int? year, CancellationToken ct);
    Task<CatalogueVariantDetailDto> GetVariantAsync(Guid variantId, CancellationToken ct);
    Task<List<CatalogueColourDto>> GetColoursAsync(CancellationToken ct);
}
