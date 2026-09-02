using Microsoft.AspNetCore.Http.HttpResults;
using WrenchWorks.Api.Features.Common;
using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Inventory;

public class InventoryEndpointHandler(IInventoryService service) : IInventoryEndpointHandler
{
    // LowStock is derived here rather than stored: it is a comparison of two columns the
    // DTO already carries, so persisting it would be a third thing to keep in step.
    private static InventoryItemDto ToDto(InventoryItem i) =>
        new(i.Id, i.Name, i.Sku, i.CategoryId, i.Category?.Name,
            i.UnitCost, i.RetailPrice, i.StockOnHand, i.ReorderThreshold,
            i.StockOnHand <= i.ReorderThreshold, i.IsConsumable, i.CreatedAtUtc);

    public async Task<Ok<List<InventoryCategoryDto>>> ListCategoriesAsync(CancellationToken ct)
    {
        var categories = await service.ListCategoriesAsync(ct);
        return TypedResults.Ok(categories
            .Select(c => new InventoryCategoryDto(c.Category.Id, c.Category.Name, c.ItemCount))
            .ToList());
    }

    public async Task<Created<InventoryCategoryDto>> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken ct)
    {
        var category = await service.CreateCategoryAsync(request, ct);
        return TypedResults.Created($"/api/inventory/categories/{category.Id}",
            new InventoryCategoryDto(category.Id, category.Name, 0));
    }

    public async Task<Ok<PagedResult<InventoryItemDto>>> ListItemsAsync(int page, int pageSize, string? search,
        Guid? categoryId, bool? lowStockOnly, bool includeArchived, CancellationToken ct)
    {
        var result = await service.ListItemsAsync(page, pageSize, search, categoryId, lowStockOnly, includeArchived, ct);
        return TypedResults.Ok(new PagedResult<InventoryItemDto>(
            result.Items.Select(ToDto).ToList(), result.Total, result.Page, result.PageSize));
    }

    public async Task<Ok<InventoryItemDto>> GetItemAsync(Guid id, CancellationToken ct) =>
        TypedResults.Ok(ToDto(await service.GetItemAsync(id, ct)));

    public async Task<Created<InventoryItemCreatedDto>> CreateItemAsync(CreateItemRequest request, CancellationToken ct)
    {
        var item = await service.CreateItemAsync(request, ct);
        return TypedResults.Created($"/api/inventory/items/{item.Id}",
            new InventoryItemCreatedDto(item.Id, item.Name, item.Sku));
    }

    public async Task<Ok<InventoryItemDto>> UpdateItemAsync(Guid id, UpdateItemRequest request, CancellationToken ct) =>
        TypedResults.Ok(ToDto(await service.UpdateItemAsync(id, request, ct)));

    public async Task<Ok<StockLevelDto>> AdjustStockAsync(Guid id, AdjustStockRequest request, CancellationToken ct)
    {
        var item = await service.AdjustStockAsync(id, request, ct);
        return TypedResults.Ok(new StockLevelDto(item.Id, item.StockOnHand));
    }

    public async Task<NoContent> DeleteItemAsync(Guid id, CancellationToken ct)
    {
        await service.DeleteItemAsync(id, ct);
        return TypedResults.NoContent();
    }

    public async Task<Ok<ArchiveResultDto>> ArchiveItemAsync(Guid id, CancellationToken ct) =>
        TypedResults.Ok(await service.ArchiveItemAsync(id, ct));

    public async Task<Ok<ArchiveResultDto>> UnarchiveItemAsync(Guid id, CancellationToken ct) =>
        TypedResults.Ok(await service.UnarchiveItemAsync(id, ct));
}
