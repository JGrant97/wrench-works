using FluentValidation;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Features.Common;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Inventory;

public class InventoryService(IInventoryRepository repository, CurrentUserService currentUser) : IInventoryService
{
    public Task<List<CategoryWithItemCount>> ListCategoriesAsync(CancellationToken ct) =>
        repository.ListCategoriesAsync(ct);

    public async Task<InventoryCategory> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("Name required");

        var name = request.Name.Trim();
        if (await repository.CategoryNameExistsAsync(name, ct))
            throw new ConflictException("Category already exists");

        var category = new InventoryCategory { Name = name };
        repository.AddCategory(category);
        await repository.SaveChangesAsync(ct);

        return category;
    }

    public Task<PagedResult<InventoryItem>> ListItemsAsync(int page, int pageSize, string? search,
        Guid? categoryId, bool? lowStockOnly, bool includeArchived, CancellationToken ct) =>
        repository.ListItemsAsync(page, pageSize, search, categoryId, lowStockOnly, includeArchived, ct);

    public async Task<InventoryItem> GetItemAsync(Guid id, CancellationToken ct) =>
        await repository.FindItemWithCategoryAsync(id, ct)
            ?? throw new NotFoundException("Inventory item not found");

    public async Task<InventoryItem> CreateItemAsync(CreateItemRequest request, CancellationToken ct)
    {
        await new CreateItemValidator().ValidateAndThrowAsync(request, ct);

        var businessId = currentUser.RequireBusinessId();

        if (!string.IsNullOrWhiteSpace(request.Sku)
            && await repository.SkuExistsAsync(request.Sku.Trim(), null, ct))
            throw new ConflictException("SKU already exists");

        var item = new InventoryItem
        {
            BusinessId = businessId,
            Name = request.Name.Trim(),
            Sku = request.Sku?.Trim(),
            CategoryId = request.CategoryId,
            UnitCost = request.UnitCost,
            RetailPrice = request.RetailPrice,
            StockOnHand = request.StockOnHand,
            IsConsumable = request.IsConsumable,
            ReorderThreshold = request.ReorderThreshold,
            CompatibilityTagsJson = request.CompatibilityTagsJson
        };
        repository.AddItem(item);

        // Opening stock is a movement like any other, so the trail reconstructs the level
        // from zero rather than starting from an unexplained number.
        if (request.StockOnHand > 0)
        {
            repository.AddStockMovement(new StockMovement
            {
                BusinessId = businessId,
                InventoryItemId = item.Id,
                QuantityDelta = request.StockOnHand,
                Reason = StockMovementReason.ManualAdjustment,
                Notes = "Initial stock",
                CreatedByUserId = currentUser.UserId
            });
        }

        await repository.SaveChangesAsync(ct);
        return item;
    }

    public async Task<InventoryItem> UpdateItemAsync(Guid id, UpdateItemRequest request, CancellationToken ct)
    {
        var item = await repository.FindItemAsync(id, ct)
            ?? throw new NotFoundException("Inventory item not found");

        if (!string.IsNullOrWhiteSpace(request.Sku) && request.Sku.Trim() != item.Sku
            && await repository.SkuExistsAsync(request.Sku.Trim(), id, ct))
            throw new ConflictException("SKU already exists");

        item.Name = request.Name.Trim();
        item.Sku = request.Sku?.Trim();
        item.CategoryId = request.CategoryId;
        item.UnitCost = request.UnitCost;
        item.RetailPrice = request.RetailPrice;
        item.ReorderThreshold = request.ReorderThreshold;
        // Only affects which tax category a job line takes; consumables still come from
        // stock and still bill as a part line. See docs/tax.md.
        item.IsConsumable = request.IsConsumable;

        await repository.SaveChangesAsync(ct);
        return item;
    }

    public async Task<InventoryItem> AdjustStockAsync(Guid id, AdjustStockRequest request, CancellationToken ct)
    {
        var item = await repository.FindItemAsync(id, ct)
            ?? throw new NotFoundException("Inventory item not found");

        // The dropdown is generated from this enum, so an invalid reason means a
        // hand-crafted request rather than a stale UI.
        if (!Enum.TryParse<StockMovementReason>(request.Reason, true, out var reason))
            throw new ValidationException("Invalid reason");

        if (item.StockOnHand + request.QuantityDelta < 0)
            throw new ConflictException("Stock cannot go below zero");

        // Read-then-write, but InventoryItem now maps IsRowVersion, so a concurrent write
        // raises a 409 rather than silently losing one of the two adjustments.
        item.StockOnHand += request.QuantityDelta;

        repository.AddStockMovement(new StockMovement
        {
            BusinessId = item.BusinessId,
            InventoryItemId = id,
            QuantityDelta = request.QuantityDelta,
            Reason = reason,
            Notes = request.Notes,
            CreatedByUserId = currentUser.UserId
        });

        await repository.SaveChangesAsync(ct);
        return item;
    }

    public async Task DeleteItemAsync(Guid id, CancellationToken ct)
    {
        var item = await repository.FindItemAsync(id, ct)
            ?? throw new NotFoundException("Item not found");

        Archiving.EnsureDeletable("item",
            new Dependent("job part lines", await repository.CountJobPartLinesAsync(id, ct)),
            new Dependent("stock movements", await repository.CountStockMovementsAsync(id, ct)));

        repository.RemoveItem(item);
        await repository.SaveChangesAsync(ct);
    }

    public async Task<ArchiveResultDto> ArchiveItemAsync(Guid id, CancellationToken ct)
    {
        var item = await repository.FindItemAsync(id, ct)
            ?? throw new NotFoundException("Item not found");

        var result = Archiving.Archive(item, id);
        await repository.SaveChangesAsync(ct);
        return result;
    }

    public async Task<ArchiveResultDto> UnarchiveItemAsync(Guid id, CancellationToken ct)
    {
        var item = await repository.FindItemAsync(id, ct)
            ?? throw new NotFoundException("Item not found");

        var result = Archiving.Unarchive(item, id);
        await repository.SaveChangesAsync(ct);
        return result;
    }
}
