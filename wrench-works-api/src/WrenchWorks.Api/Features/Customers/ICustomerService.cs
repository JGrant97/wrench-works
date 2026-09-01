using WrenchWorks.Api.Features.Common;

namespace WrenchWorks.Api.Features.Customers;

// The Customer slice behind an interface: the endpoints become a thin HTTP shell.
// Methods return DTOs, not IResult -- failures are thrown and mapped by
// ErrorHandlingMiddleware, so nothing here needs to know about status codes.
public interface ICustomerService
{
    Task<PagedResult<CustomerDto>> ListAsync(int page = 1, int pageSize = 25, string? search = null, bool includeArchived = false, CancellationToken ct = default);
    Task<CustomerDetailDto> GetAsync(Guid id, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
    Task<ArchiveResultDto> ArchiveAsync(Guid id, CancellationToken ct);
    Task<ArchiveResultDto> UnarchiveAsync(Guid id, CancellationToken ct);
    Task<CustomerDto> CreateAsync(CreateCustomerRequest request, CancellationToken ct);
    Task<CustomerDto> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken ct);
    Task<List<CustomerSearchResultDto>> SearchAsync(string q, CancellationToken ct);
}
