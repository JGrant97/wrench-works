using Microsoft.EntityFrameworkCore;
using WrenchWorks.Api.Features.Common;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;
using Entities = WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Jobs;

/// <summary>
/// Data access for Jobs.
///
/// JobLaborLines, JobPartLines and StockMovements have DbSets but NO global query filter,
/// so querying them directly crosses tenants. Every line lookup here is therefore keyed on
/// JobId as well as the line id, and the service always resolves the parent job through
/// the filtered db.Jobs first. That indirect isolation is what TenantIsolationTests pins;
/// keep both halves if you refactor this.
/// </summary>
public class JobRepository(AppDbContext db) : IJobRepository
{
    public async Task<PagedResult<Job>> ListAsync(int page, int pageSize, string? status,
        string? search, bool includeArchived, CancellationToken ct)
    {
        var query = db.Jobs
            .Include(j => j.Customer)
            .Include(j => j.Vehicle)
            .Include(j => j.AssignedZone)
            .AsQueryable();

        if (!includeArchived) query = query.Where(j => j.ArchivedAtUtc == null);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<JobStatus>(status, true, out var st))
            query = query.Where(j => j.Status == st);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(j => j.Title.ToLower().Contains(s) || j.Customer.Name.ToLower().Contains(s));
        }

        var total = await query.CountAsync(ct);

        // The line sums are loaded with the job so the handler can total them without a
        // query per row. Missing index: Jobs(BusinessId, CreatedAtUtc) would cover this
        // ordering -- noted in the performance section of docs/review-findings.md.
        var items = await query
            .Include(j => j.LaborLines)
            .Include(j => j.PartLines)
            .OrderByDescending(j => j.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<Job>(items, total, page, pageSize);
    }

    public async Task<Job?> FindAsync(Guid id, CancellationToken ct) =>
        await db.Jobs.FindAsync([id], ct);

    public Task<Job?> FindWithLinesAsync(Guid id, CancellationToken ct) =>
        db.Jobs
          .Include(j => j.Customer)
          .Include(j => j.Vehicle)
          .Include(j => j.AssignedZone)
          .Include(j => j.LaborLines)
          .Include(j => j.PartLines).ThenInclude(pl => pl.InventoryItem)
          .FirstOrDefaultAsync(j => j.Id == id, ct);

    public async Task<Entities.Business?> FindBusinessAsync(Guid businessId, CancellationToken ct) =>
        await db.Businesses.FindAsync([businessId], ct);

    public async Task<Customer?> FindCustomerAsync(Guid customerId, CancellationToken ct) =>
        await db.Customers.FindAsync([customerId], ct);

    public async Task<Vehicle?> FindVehicleAsync(Guid vehicleId, CancellationToken ct) =>
        await db.Vehicles.FindAsync([vehicleId], ct);

    public async Task<InventoryItem?> FindInventoryItemAsync(Guid itemId, CancellationToken ct) =>
        await db.InventoryItems.FindAsync([itemId], ct);

    // Reading through db.Zones applies the global query filter, so another business's zone
    // simply is not found. See EnsureZoneIsOursAsync in JobService.
    public Task<bool> ZoneExistsAsync(Guid zoneId, CancellationToken ct) =>
        db.Zones.AnyAsync(z => z.Id == zoneId, ct);

    // Jobs and bookings cross-link with two independent nullable FKs and nothing keeps
    // them consistent, so both directions have to be checked.
    public async Task<Booking?> FindLinkedBookingAsync(Job job, CancellationToken ct) =>
        job.BookingId.HasValue
            ? await db.Bookings.FindAsync([job.BookingId.Value], ct)
            : await db.Bookings.FirstOrDefaultAsync(b => b.JobId == job.Id, ct);

    public Task<int> CountLabourLinesAsync(Guid jobId, CancellationToken ct) =>
        db.JobLaborLines.CountAsync(l => l.JobId == jobId, ct);

    public Task<int> CountPartLinesAsync(Guid jobId, CancellationToken ct) =>
        db.JobPartLines.CountAsync(p => p.JobId == jobId, ct);

    public Task<int> CountBookingsAsync(Guid jobId, CancellationToken ct) =>
        db.Bookings.CountAsync(b => b.JobId == jobId, ct);

    public Task<JobPartLine?> FindPartLineAsync(Guid jobId, Guid lineId, CancellationToken ct) =>
        db.JobPartLines
          .Include(l => l.InventoryItem)
          .FirstOrDefaultAsync(l => l.Id == lineId && l.JobId == jobId, ct);

    public Task<JobLaborLine?> FindLaborLineAsync(Guid jobId, Guid lineId, CancellationToken ct) =>
        db.JobLaborLines.FirstOrDefaultAsync(l => l.Id == lineId && l.JobId == jobId, ct);

    // Deliberately does not filter on ArchivedAtUtc: a rate that has since been retired
    // must still resolve, or a historical job loses the name of the tax it was charged.
    public Task<List<TaxRate>> GetTaxRatesWithComponentsAsync(List<Guid> rateIds, CancellationToken ct) =>
        db.TaxRates
          .Include(r => r.Components)
          .Where(r => rateIds.Contains(r.Id))
          .ToListAsync(ct);

    public Task<TaxRateCategory?> FindActiveTaxMappingAsync(TaxCategory category, CancellationToken ct) =>
        db.TaxRateCategories
          .Include(m => m.TaxRate)
          .Where(m => m.Category == category && m.TaxRate.ArchivedAtUtc == null)
          .FirstOrDefaultAsync(ct);

    public void AddJob(Job job) => db.Jobs.Add(job);
    public void RemoveJob(Job job) => db.Jobs.Remove(job);
    public void AddPartLine(JobPartLine line) => db.JobPartLines.Add(line);
    public void RemovePartLine(JobPartLine line) => db.JobPartLines.Remove(line);
    public void AddLaborLine(JobLaborLine line) => db.JobLaborLines.Add(line);
    public void RemoveLaborLine(JobLaborLine line) => db.JobLaborLines.Remove(line);
    public void AddStockMovement(StockMovement movement) => db.StockMovements.Add(movement);
    public void AddAuditLog(AuditLog log) => db.AuditLogs.Add(log);
    public void AddBooking(Booking booking) => db.Bookings.Add(booking);
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
