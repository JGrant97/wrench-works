using WrenchWorks.Api.Features.Common;

namespace WrenchWorks.Api.Features.Tax;

// The Tax slice behind an interface: the endpoints become a thin HTTP shell.
// Methods return DTOs, not IResult -- failures are thrown and mapped by
// ErrorHandlingMiddleware, so nothing here needs to know about status codes.
public interface ITaxService
{
    Task<List<TaxRateDto>> ListAsync(bool includeArchived = false, CancellationToken ct = default);
    Task<TaxRateDto> CreateAsync(SaveTaxRateRequest request, CancellationToken ct);
    Task<TaxRateDto> UpdateAsync(Guid id, SaveTaxRateRequest request, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
    Task<ArchiveResultDto> ArchiveAsync(Guid id, CancellationToken ct);
    Task<ArchiveResultDto> UnarchiveAsync(Guid id, CancellationToken ct);
}
