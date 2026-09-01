using WrenchWorks.Api.Features.Common;
using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Jobs;

// The Job slice behind an interface: the endpoints become a thin HTTP shell.
// Methods return DTOs, not IResult -- failures are thrown and mapped by
// ErrorHandlingMiddleware, so nothing here needs to know about status codes.
public interface IJobService
{
    Task<PagedResult<JobListItemDto>> ListAsync(int page = 1, int pageSize = 25, string? status = null, string? search = null, bool includeArchived = false, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct);
    Task<ArchiveResultDto> ArchiveAsync(Guid id, CancellationToken ct);
    Task<ArchiveResultDto> UnarchiveAsync(Guid id, CancellationToken ct);
    Task<JobDetailDto> GetAsync(Guid id, CancellationToken ct);
    Task<JobCreatedDto> CreateAsync(CreateJobRequest request, CancellationToken ct);
    Task<JobSummaryDto> UpdateJobAsync(Guid id, UpdateJobRequest request, CancellationToken ct);
    Task<JobStatusDto> UpdateStatusAsync(Guid id, UpdateJobStatusRequest request, CancellationToken ct);
    Task<PartLineDto> AddPartAsync(Guid id, AddPartToJobRequest request, CancellationToken ct);
    Task<LaborLineDto> AddLaborAsync(Guid id, AddLaborLineRequest request, CancellationToken ct);
    Task RemovePartAsync(Guid id, Guid lineId, CancellationToken ct);
    Task RemoveLaborAsync(Guid id, Guid lineId, CancellationToken ct);
}
