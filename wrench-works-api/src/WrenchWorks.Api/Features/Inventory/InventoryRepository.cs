using Microsoft.EntityFrameworkCore;
using WrenchWorks.Api.Features.Common;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Inventory;

public class InventoryRepository(AppDbContext db) : IInventoryRepository
{
    public Task<List<CategoryWithItemCount>> ListCategoriesAsync(CancellationToken ct) =>
        db.InventoryCategories
          .OrderBy(c => c.Name)
          .Select(c => new CategoryWithItemCount(c, c.Items.Count))
          .ToListAsync(ct);

    // OPEN QUESTION: InventoryCategory is global, not tenant-scoped, and this uniqueness
    // check is global too -- so once any business creates "Brakes", every other business
    // gets a 409 and can never create their own. Undecided whether the shared taxonomy is
    // deliberate; do not build on it without asking. See CLAUDE.md.
    public Task<bool> CategoryNameExistsAsync(string name, CancellationToken ct) =>
        db.InventoryCategories.IgnoreQueryFilters().AnyAsync(c => c.Name == name, ct);

    public void AddCategory(InventoryCategory category) => db.InventoryCategories.Add(category);

    public async Task<PagedResult<InventoryItem>> ListItemsAsync(int page, int pageSize, string? search,
        Guid? categoryId, bool? lowStockOnly, bool includeArchived, CancellationToken ct)
    {
        var query = db.InventoryItems.Include(i => i.Category).AsQueryable();

        // A discontinued part stays out of the picker but keeps its movement history.
        if (!includeArchived) query = query.Where(i => i.ArchivedAtUtc == null);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(i => i.Name.ToLower().Contains(s) || (i.Sku != null && i.Sku.ToLower().Contains(s)));
        }
        if (categoryId.HasValue) query = query.Where(i => i.CategoryId == categoryId.Value);
        if (lowStockOnly == true) query = query.Where(i => i.StockOnHand <= i.ReorderThreshold);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(i => i.Name)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<InventoryItem>(items, total, page, pageSize);
    }

    public async Task<InventoryItem?> FindItemAsync(Guid id, CancellationToken ct) =>
        await db.InventoryItems.FindAsync([id], ct);

    public Task<InventoryItem?> FindItemWithCategoryAsync(Guid id, CancellationToken ct) =>
        db.InventoryItems.Include(i => i.Category).FirstOrDefaultAsync(i => i.Id == id, ct);

    public Task<bool> SkuExistsAsync(string sku, Guid? excludeItemId, CancellationToken ct) =>
        db.InventoryItems.AnyAsync(i => i.Sku == sku && (excludeItemId == null || i.Id != excludeItemId), ct);

    public Task<int> CountJobPartLinesAsync(Guid itemId, CancellationToken ct) =>
        db.JobPartLines.CountAsync(p => p.InventoryItemId == itemId, ct);

    public Task<int> CountStockMovementsAsync(Guid itemId, CancellationToken ct) =>
        db.StockMovements.CountAsync(m => m.InventoryItemId == itemId, ct);

    public void AddItem(InventoryItem item) => db.InventoryItems.Add(item);
    public void RemoveItem(InventoryItem item) => db.InventoryItems.Remove(item);
    public void AddStockMovement(StockMovement movement) => db.StockMovements.Add(movement);
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
