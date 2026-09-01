using WrenchWorks.Api.Features.Common;

namespace WrenchWorks.Api.Features.Inventory;

// The Inventory slice behind an interface: the endpoints become a thin HTTP shell.
// Methods return DTOs, not IResult -- failures are thrown and mapped by
// ErrorHandlingMiddleware, so nothing here needs to know about status codes.
public interface IInventoryService
{
    Task<List<InventoryCategoryDto>> ListCategoriesAsync(CancellationToken ct);
    Task<InventoryCategoryDto> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken ct);
    Task<PagedResult<InventoryItemDto>> ListItemsAsync(int page = 1, int pageSize = 25, string? search = null, Guid? categoryId = null, bool? lowStockOnly = null, bool includeArchived = false, CancellationToken ct = default);
    Task<InventoryItemDto> GetItemAsync(Guid id, CancellationToken ct);
    Task DeleteItemAsync(Guid id, CancellationToken ct);
    Task<ArchiveResultDto> ArchiveItemAsync(Guid id, CancellationToken ct);
    Task<ArchiveResultDto> UnarchiveItemAsync(Guid id, CancellationToken ct);
    Task<InventoryItemCreatedDto> CreateItemAsync(CreateItemRequest request, CancellationToken ct);
    Task<InventoryItemDto> UpdateItemAsync(Guid id, UpdateItemRequest request, CancellationToken ct);
    Task<StockLevelDto> AdjustStockAsync(Guid id, AdjustStockRequest request, CancellationToken ct);
}
