using WrenchWorks.Api.Features.Common;
using WrenchWorks.Domain.Entities;
using Entities = WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Jobs;

public interface IJobRepository
{
    Task<PagedResult<Job>> ListAsync(int page, int pageSize, string? status, string? search, bool includeArchived, CancellationToken ct);
    Task<Job?> FindAsync(Guid id, CancellationToken ct);
    Task<Job?> FindWithLinesAsync(Guid id, CancellationToken ct);
    Task<Entities.Business?> FindBusinessAsync(Guid businessId, CancellationToken ct);
    Task<Customer?> FindCustomerAsync(Guid customerId, CancellationToken ct);
    Task<Vehicle?> FindVehicleAsync(Guid vehicleId, CancellationToken ct);
    Task<InventoryItem?> FindInventoryItemAsync(Guid itemId, CancellationToken ct);
    Task<bool> ZoneExistsAsync(Guid zoneId, CancellationToken ct);
    Task<Booking?> FindLinkedBookingAsync(Job job, CancellationToken ct);

    Task<int> CountLabourLinesAsync(Guid jobId, CancellationToken ct);
    Task<int> CountPartLinesAsync(Guid jobId, CancellationToken ct);
    Task<int> CountBookingsAsync(Guid jobId, CancellationToken ct);

    Task<JobPartLine?> FindPartLineAsync(Guid jobId, Guid lineId, CancellationToken ct);
    Task<JobLaborLine?> FindLaborLineAsync(Guid jobId, Guid lineId, CancellationToken ct);

    Task<List<TaxRate>> GetTaxRatesWithComponentsAsync(List<Guid> rateIds, CancellationToken ct);
    Task<TaxRateCategory?> FindActiveTaxMappingAsync(TaxCategory category, CancellationToken ct);

    void AddJob(Job job);
    void RemoveJob(Job job);
    void AddPartLine(JobPartLine line);
    void RemovePartLine(JobPartLine line);
    void AddLaborLine(JobLaborLine line);
    void RemoveLaborLine(JobLaborLine line);
    void AddStockMovement(StockMovement movement);
    void AddAuditLog(AuditLog log);
    void AddBooking(Booking booking);
    Task SaveChangesAsync(CancellationToken ct);
}
