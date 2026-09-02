using Microsoft.AspNetCore.Http.HttpResults;
using WrenchWorks.Api.Features.Common;

namespace WrenchWorks.Api.Features.Customers;

public interface ICustomerEndpointHandler
{
    Task<Ok<PagedResult<CustomerDto>>> ListAsync(int page, int pageSize, string? search, bool includeArchived, CancellationToken ct);
    Task<Ok<CustomerDetailDto>> GetAsync(Guid id, CancellationToken ct);
    Task<NoContent> DeleteAsync(Guid id, CancellationToken ct);
    Task<Ok<ArchiveResultDto>> ArchiveAsync(Guid id, CancellationToken ct);
    Task<Ok<ArchiveResultDto>> UnarchiveAsync(Guid id, CancellationToken ct);
    Task<Created<CustomerDto>> CreateAsync(CreateCustomerRequest request, CancellationToken ct);
    Task<Ok<CustomerDto>> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken ct);
    Task<Ok<List<CustomerSearchResultDto>>> SearchAsync(string q, CancellationToken ct);
}
