using WrenchWorks.Api.Features.Common;
using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Jobs;

/// <summary>
/// The money on a job, computed once in the service because the inclusive-vs-exclusive
/// arithmetic is a domain rule rather than a formatting choice. See docs/tax.md.
/// </summary>
public record JobTotals(decimal LaborTotal, decimal PartsTotal, decimal SubTotal,
    decimal TaxTotal, decimal GrandTotal);

// The job tax grouped by the rate each line was charged at, so an invoice can show a named
// rate and its amount rather than one undifferentiated number.
public record JobTaxGroup(string RateName, decimal Percent, decimal Amount,
    List<TaxRateComponent> Components);

// Everything the job detail page needs. The business settings ride along because the tax
// label and the inclusive-pricing flag both come from there.
public record JobDetail(Job Job, JobTotals Totals, string TaxLabel, bool PricesIncludeTax,
    List<JobTaxGroup> TaxBreakdown);

public interface IJobService
{
    Task<PagedResult<Job>> ListAsync(int page, int pageSize, string? status, string? search, bool includeArchived, CancellationToken ct);
    Task<JobDetail> GetAsync(Guid id, CancellationToken ct);
    Task<Job> CreateAsync(CreateJobRequest request, CancellationToken ct);
    Task<Job> UpdateJobAsync(Guid id, UpdateJobRequest request, CancellationToken ct);
    Task<Job> UpdateStatusAsync(Guid id, UpdateJobStatusRequest request, CancellationToken ct);
    Task<JobPartLine> AddPartAsync(Guid id, AddPartToJobRequest request, CancellationToken ct);
    Task<JobLaborLine> AddLaborAsync(Guid id, AddLaborLineRequest request, CancellationToken ct);
    Task RemovePartAsync(Guid id, Guid lineId, CancellationToken ct);
    Task RemoveLaborAsync(Guid id, Guid lineId, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
    Task<ArchiveResultDto> ArchiveAsync(Guid id, CancellationToken ct);
    Task<ArchiveResultDto> UnarchiveAsync(Guid id, CancellationToken ct);
}
