using Microsoft.AspNetCore.Http.HttpResults;
using WrenchWorks.Api.Features.Common;

namespace WrenchWorks.Api.Features.Tax;

public interface ITaxEndpointHandler
{
    Task<Ok<List<TaxRateDto>>> ListAsync(bool includeArchived, CancellationToken ct);
    Task<Created<TaxRateDto>> CreateAsync(SaveTaxRateRequest request, CancellationToken ct);
    Task<Ok<TaxRateDto>> UpdateAsync(Guid id, SaveTaxRateRequest request, CancellationToken ct);
    Task<NoContent> DeleteAsync(Guid id, CancellationToken ct);
    Task<Ok<ArchiveResultDto>> ArchiveAsync(Guid id, CancellationToken ct);
    Task<Ok<ArchiveResultDto>> UnarchiveAsync(Guid id, CancellationToken ct);
}
