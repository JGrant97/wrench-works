using WrenchWorks.Api.Features.Common;
using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Inventory;

// The category plus how many items sit in it, counted in SQL.
public record CategoryWithItemCount(InventoryCategory Category, int ItemCount);

public interface IInventoryRepository
{
    Task<List<CategoryWithItemCount>> ListCategoriesAsync(CancellationToken ct);
    Task<bool> CategoryNameExistsAsync(string name, CancellationToken ct);
    void AddCategory(InventoryCategory category);

    Task<PagedResult<InventoryItem>> ListItemsAsync(int page, int pageSize, string? search,
        Guid? categoryId, bool? lowStockOnly, bool includeArchived, CancellationToken ct);
    Task<InventoryItem?> FindItemAsync(Guid id, CancellationToken ct);
    Task<InventoryItem?> FindItemWithCategoryAsync(Guid id, CancellationToken ct);
    Task<bool> SkuExistsAsync(string sku, Guid? excludeItemId, CancellationToken ct);
    Task<int> CountJobPartLinesAsync(Guid itemId, CancellationToken ct);
    Task<int> CountStockMovementsAsync(Guid itemId, CancellationToken ct);

    void AddItem(InventoryItem item);
    void RemoveItem(InventoryItem item);
    void AddStockMovement(StockMovement movement);
    Task SaveChangesAsync(CancellationToken ct);
}
