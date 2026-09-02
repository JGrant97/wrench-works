using Microsoft.EntityFrameworkCore;
using WrenchWorks.Api.Features.Common;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Customers;

public class CustomerRepository(AppDbContext db) : ICustomerRepository
{
    // Leading-wildcard Contains, which no btree index can serve. Fine at this volume;
    // needs pg_trgm + GIN if it grows. See the performance notes in review-findings.md.
    private static IQueryable<Customer> ApplySearch(IQueryable<Customer> query, string term)
    {
        var s = term.ToLower();
        return query.Where(c =>
            c.Name.ToLower().Contains(s) ||
            (c.Phone != null && c.Phone.Contains(s)) ||
            (c.Email != null && c.Email.ToLower().Contains(s)));
    }

    public async Task<PagedResult<CustomerWithVehicleCount>> ListAsync(
        int page, int pageSize, string? search, bool includeArchived, CancellationToken ct)
    {
        // Archived customers stay out of lists and pickers but remain resolvable by id,
        // so a historical job still renders the name of the customer it was for.
        var query = db.Customers.AsQueryable();
        if (!includeArchived) query = query.Where(c => c.ArchivedAtUtc == null);
        if (!string.IsNullOrWhiteSpace(search)) query = ApplySearch(query, search);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CustomerWithVehicleCount(c, c.Vehicles.Count))
            .ToListAsync(ct);

        return new PagedResult<CustomerWithVehicleCount>(items, total, page, pageSize);
    }

    public async Task<Customer?> FindAsync(Guid id, CancellationToken ct) =>
        await db.Customers.FindAsync([id], ct);

    public Task<Customer?> FindWithVehiclesAsync(Guid id, CancellationToken ct) =>
        db.Customers
          .Include(c => c.Vehicles).ThenInclude(v => v.Colour)
          .FirstOrDefaultAsync(c => c.Id == id, ct);

    // Queried separately rather than Include'd: a customer's whole job history with line
    // items would be a large graph to materialise just to show the last few.
    public Task<List<CustomerRecentJob>> GetRecentJobsAsync(Guid customerId, int take, CancellationToken ct) =>
        db.Jobs
          .Where(j => j.CustomerId == customerId)
          .OrderByDescending(j => j.CreatedAtUtc)
          .Take(take)
          .Select(j => new CustomerRecentJob(
              j.Id, j.Title, j.Status, j.Vehicle.DisplayName,
              j.LaborLines.Sum(l => l.Hours * l.Rate) + j.PartLines.Sum(p => p.Quantity * p.UnitPrice),
              j.CreatedAtUtc))
          .ToListAsync(ct);

    public Task<List<Customer>> SearchAsync(string term, int take, CancellationToken ct) =>
        ApplySearch(db.Customers, term).Take(take).ToListAsync(ct);

    public Task<bool> PhoneExistsAsync(string phone, CancellationToken ct) =>
        db.Customers.AnyAsync(c => c.Phone == phone, ct);

    public Task<int> CountVehiclesAsync(Guid customerId, CancellationToken ct) =>
        db.Vehicles.CountAsync(v => v.CustomerId == customerId, ct);

    public Task<int> CountJobsAsync(Guid customerId, CancellationToken ct) =>
        db.Jobs.CountAsync(j => j.CustomerId == customerId, ct);

    public Task<int> CountBookingsAsync(Guid customerId, CancellationToken ct) =>
        db.Bookings.CountAsync(b => b.CustomerId == customerId, ct);

    public void Add(Customer customer) => db.Customers.Add(customer);
    public void Remove(Customer customer) => db.Customers.Remove(customer);
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
