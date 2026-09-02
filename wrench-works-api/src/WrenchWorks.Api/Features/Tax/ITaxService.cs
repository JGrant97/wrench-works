using WrenchWorks.Api.Features.Common;
using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Tax;

public interface ITaxService
{
    Task<List<TaxRate>> ListAsync(bool includeArchived, CancellationToken ct);
    Task<TaxRate> CreateAsync(SaveTaxRateRequest request, CancellationToken ct);
    Task<TaxRate> UpdateAsync(Guid id, SaveTaxRateRequest request, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
    Task<ArchiveResultDto> ArchiveAsync(Guid id, CancellationToken ct);
    Task<ArchiveResultDto> UnarchiveAsync(Guid id, CancellationToken ct);
}
