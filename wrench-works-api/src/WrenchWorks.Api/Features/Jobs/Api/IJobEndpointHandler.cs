using Microsoft.AspNetCore.Http.HttpResults;
using WrenchWorks.Api.Features.Common;

namespace WrenchWorks.Api.Features.Jobs;

public interface IJobEndpointHandler
{
    Task<Ok<PagedResult<JobListItemDto>>> ListAsync(int page, int pageSize, string? status, string? search, bool includeArchived, CancellationToken ct);
    Task<Ok<JobDetailDto>> GetAsync(Guid id, CancellationToken ct);
    Task<Created<JobCreatedDto>> CreateAsync(CreateJobRequest request, CancellationToken ct);
    Task<Ok<JobSummaryDto>> UpdateJobAsync(Guid id, UpdateJobRequest request, CancellationToken ct);
    Task<Ok<JobStatusDto>> UpdateStatusAsync(Guid id, UpdateJobStatusRequest request, CancellationToken ct);
    Task<Created<PartLineDto>> AddPartAsync(Guid id, AddPartToJobRequest request, CancellationToken ct);
    Task<Created<LaborLineDto>> AddLaborAsync(Guid id, AddLaborLineRequest request, CancellationToken ct);
    Task<NoContent> RemovePartAsync(Guid id, Guid lineId, CancellationToken ct);
    Task<NoContent> RemoveLaborAsync(Guid id, Guid lineId, CancellationToken ct);
    Task<NoContent> DeleteAsync(Guid id, CancellationToken ct);
    Task<Ok<ArchiveResultDto>> ArchiveAsync(Guid id, CancellationToken ct);
    Task<Ok<ArchiveResultDto>> UnarchiveAsync(Guid id, CancellationToken ct);
}
