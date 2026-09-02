using WrenchWorks.Api.Features.Common;
using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Customers;

// A customer plus the history the detail page shows. The vehicles ride on the entity;
// the jobs are fetched separately because the full history would be a large graph.
public record CustomerDetail(Customer Customer, List<CustomerRecentJob> RecentJobs);

public interface ICustomerService
{
    Task<PagedResult<CustomerWithVehicleCount>> ListAsync(int page, int pageSize, string? search, bool includeArchived, CancellationToken ct);
    Task<CustomerDetail> GetAsync(Guid id, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
    Task<ArchiveResultDto> ArchiveAsync(Guid id, CancellationToken ct);
    Task<ArchiveResultDto> UnarchiveAsync(Guid id, CancellationToken ct);
    Task<Customer> CreateAsync(CreateCustomerRequest request, CancellationToken ct);
    Task<Customer> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken ct);
    Task<List<Customer>> SearchAsync(string q, CancellationToken ct);
}
