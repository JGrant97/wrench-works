using Microsoft.AspNetCore.Http.HttpResults;
using WrenchWorks.Api.Features.Common;

namespace WrenchWorks.Api.Features.Inventory;

public interface IInventoryEndpointHandler
{
    Task<Ok<List<InventoryCategoryDto>>> ListCategoriesAsync(CancellationToken ct);
    Task<Created<InventoryCategoryDto>> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken ct);
    Task<Ok<PagedResult<InventoryItemDto>>> ListItemsAsync(int page, int pageSize, string? search,
        Guid? categoryId, bool? lowStockOnly, bool includeArchived, CancellationToken ct);
    Task<Ok<InventoryItemDto>> GetItemAsync(Guid id, CancellationToken ct);
    Task<Created<InventoryItemCreatedDto>> CreateItemAsync(CreateItemRequest request, CancellationToken ct);
    Task<Ok<InventoryItemDto>> UpdateItemAsync(Guid id, UpdateItemRequest request, CancellationToken ct);
    Task<Ok<StockLevelDto>> AdjustStockAsync(Guid id, AdjustStockRequest request, CancellationToken ct);
    Task<NoContent> DeleteItemAsync(Guid id, CancellationToken ct);
    Task<Ok<ArchiveResultDto>> ArchiveItemAsync(Guid id, CancellationToken ct);
    Task<Ok<ArchiveResultDto>> UnarchiveItemAsync(Guid id, CancellationToken ct);
}
