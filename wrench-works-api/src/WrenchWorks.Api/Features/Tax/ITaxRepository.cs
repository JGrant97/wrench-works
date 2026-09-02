using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Tax;

public interface ITaxRepository
{
    Task<List<TaxRate>> ListAsync(bool includeArchived, CancellationToken ct);
    Task<TaxRate?> FindAsync(Guid id, CancellationToken ct);
    Task<TaxRate?> FindWithComponentsAndCategoriesAsync(Guid id, CancellationToken ct);
    Task<List<TaxRateCategory>> GetCategoryMappingsAsync(Guid rateId, IEnumerable<TaxCategory> alsoClaiming, CancellationToken ct);
    Task<int> CountLabourLinesUsingAsync(Guid rateId, CancellationToken ct);
    Task<int> CountPartLinesUsingAsync(Guid rateId, CancellationToken ct);

    void Add(TaxRate rate);
    void Remove(TaxRate rate);
    void RemoveComponents(IEnumerable<TaxRateComponent> components);
    void RemoveCategoryMappings(IEnumerable<TaxRateCategory> mappings);
    void AddCategoryMapping(TaxRateCategory mapping);
    Task SaveChangesAsync(CancellationToken ct);
}
