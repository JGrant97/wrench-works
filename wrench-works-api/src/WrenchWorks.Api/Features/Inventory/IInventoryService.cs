using WrenchWorks.Api.Features.Common;
using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Inventory;

public interface IInventoryService
{
    Task<List<CategoryWithItemCount>> ListCategoriesAsync(CancellationToken ct);
    Task<InventoryCategory> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken ct);
    Task<PagedResult<InventoryItem>> ListItemsAsync(int page, int pageSize, string? search,
        Guid? categoryId, bool? lowStockOnly, bool includeArchived, CancellationToken ct);
    Task<InventoryItem> GetItemAsync(Guid id, CancellationToken ct);
    Task<InventoryItem> CreateItemAsync(CreateItemRequest request, CancellationToken ct);
    Task<InventoryItem> UpdateItemAsync(Guid id, UpdateItemRequest request, CancellationToken ct);
    Task<InventoryItem> AdjustStockAsync(Guid id, AdjustStockRequest request, CancellationToken ct);
    Task DeleteItemAsync(Guid id, CancellationToken ct);
    Task<ArchiveResultDto> ArchiveItemAsync(Guid id, CancellationToken ct);
    Task<ArchiveResultDto> UnarchiveItemAsync(Guid id, CancellationToken ct);
}
