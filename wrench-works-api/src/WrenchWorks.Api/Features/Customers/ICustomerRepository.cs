using WrenchWorks.Api.Features.Common;
using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Customers;

// The entity plus a count computed in SQL. Projecting the entity alongside the count keeps
// the list off the vehicles table without duplicating every customer column in a read model.
public record CustomerWithVehicleCount(Customer Customer, int VehicleCount);

// A job as the customer detail page shows it: the total is summed in the database rather
// than by materialising every line item of every historical job.
public record CustomerRecentJob(Guid Id, string Title, JobStatus Status,
    string VehicleDisplay, decimal Total, DateTime CreatedAtUtc);

public interface ICustomerRepository
{
    Task<PagedResult<CustomerWithVehicleCount>> ListAsync(int page, int pageSize, string? search, bool includeArchived, CancellationToken ct);
    Task<Customer?> FindAsync(Guid id, CancellationToken ct);
    Task<Customer?> FindWithVehiclesAsync(Guid id, CancellationToken ct);
    Task<List<CustomerRecentJob>> GetRecentJobsAsync(Guid customerId, int take, CancellationToken ct);
    Task<List<Customer>> SearchAsync(string term, int take, CancellationToken ct);
    Task<bool> PhoneExistsAsync(string phone, CancellationToken ct);
    Task<int> CountVehiclesAsync(Guid customerId, CancellationToken ct);
    Task<int> CountJobsAsync(Guid customerId, CancellationToken ct);
    Task<int> CountBookingsAsync(Guid customerId, CancellationToken ct);

    void Add(Customer customer);
    void Remove(Customer customer);
    Task SaveChangesAsync(CancellationToken ct);
}
