using Microsoft.EntityFrameworkCore;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Tax;

public class TaxRepository(AppDbContext db) : ITaxRepository
{
    public Task<List<TaxRate>> ListAsync(bool includeArchived, CancellationToken ct)
    {
        var query = db.TaxRates.Include(r => r.Components).Include(r => r.Categories).AsQueryable();

        // Archived rates are excluded by the list, not by a query filter: a historical
        // line still references its rate and must stay resolvable by id.
        if (!includeArchived) query = query.Where(r => r.ArchivedAtUtc == null);

        return query.OrderBy(r => r.Name).ToListAsync(ct);
    }

    public Task<TaxRate?> FindAsync(Guid id, CancellationToken ct) =>
        db.TaxRates.FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<TaxRate?> FindWithComponentsAndCategoriesAsync(Guid id, CancellationToken ct) =>
        db.TaxRates
          .Include(r => r.Components)
          .Include(r => r.Categories)
          .FirstOrDefaultAsync(r => r.Id == id, ct);

    // Everything currently pointing at this rate, plus anything pointing elsewhere that
    // this rate is about to claim.
    public Task<List<TaxRateCategory>> GetCategoryMappingsAsync(
        Guid rateId, IEnumerable<TaxCategory> alsoClaiming, CancellationToken ct)
    {
        var claiming = alsoClaiming.ToList();
        return db.TaxRateCategories
                 .Where(m => m.TaxRateId == rateId || claiming.Contains(m.Category))
                 .ToListAsync(ct);
    }

    public Task<int> CountLabourLinesUsingAsync(Guid rateId, CancellationToken ct) =>
        db.JobLaborLines.CountAsync(l => l.TaxRateId == rateId, ct);

    public Task<int> CountPartLinesUsingAsync(Guid rateId, CancellationToken ct) =>
        db.JobPartLines.CountAsync(p => p.TaxRateId == rateId, ct);

    public void Add(TaxRate rate) => db.TaxRates.Add(rate);
    public void Remove(TaxRate rate) => db.TaxRates.Remove(rate);
    public void RemoveComponents(IEnumerable<TaxRateComponent> components) => db.TaxRateComponents.RemoveRange(components);
    public void RemoveCategoryMappings(IEnumerable<TaxRateCategory> mappings) => db.TaxRateCategories.RemoveRange(mappings);
    public void AddCategoryMapping(TaxRateCategory mapping) => db.TaxRateCategories.Add(mapping);
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
