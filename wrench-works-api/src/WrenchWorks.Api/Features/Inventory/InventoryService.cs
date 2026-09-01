using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Features.Common;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Inventory;

public class InventoryService(AppDbContext db, CurrentUserService currentUser) : IInventoryService
{
    public async Task<List<InventoryCategoryDto>> ListCategoriesAsync(CancellationToken ct)
    {
        var categories = await db.InventoryCategories
            .OrderBy(c => c.Name)
            .Select(c => new InventoryCategoryDto(c.Id, c.Name, c.Items.Count))
            .ToListAsync(ct);
        return categories;
    }

    public async Task<InventoryCategoryDto> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("Name required");

        var exists = await db.InventoryCategories.IgnoreQueryFilters().AnyAsync(c => c.Name == request.Name.Trim(), ct);
        if (exists) throw new ConflictException("Category already exists");

        var cat = new InventoryCategory { Name = request.Name.Trim() };
        db.InventoryCategories.Add(cat);
        await db.SaveChangesAsync(ct);

        return new InventoryCategoryDto(cat.Id, cat.Name, 0);
    }

    public async Task<PagedResult<InventoryItemDto>> ListItemsAsync(int page = 1, int pageSize = 25, string? search = null, Guid? categoryId = null, bool? lowStockOnly = null, bool includeArchived = false, CancellationToken ct = default)
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
            .Select(i => new InventoryItemDto(i.Id, i.Name, i.Sku, i.CategoryId, i.Category != null ? i.Category.Name : null, i.UnitCost, i.RetailPrice, i.StockOnHand, i.ReorderThreshold, i.StockOnHand <= i.ReorderThreshold, i.IsConsumable, i.CreatedAtUtc))
            .ToListAsync(ct);

        return new PagedResult<InventoryItemDto>(items, total, page, pageSize);
    }

    public async Task<InventoryItemDto> GetItemAsync(Guid id, CancellationToken ct)
    {
        var item = await db.InventoryItems.Include(i => i.Category).FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new NotFoundException("Inventory item not found");

        return new InventoryItemDto(item.Id, item.Name, item.Sku, item.CategoryId, item.Category?.Name, item.UnitCost, item.RetailPrice, item.StockOnHand, item.ReorderThreshold, item.StockOnHand <= item.ReorderThreshold, item.IsConsumable, item.CreatedAtUtc);
    }

    public async Task DeleteItemAsync(Guid id, CancellationToken ct)
    {
        var item = await db.InventoryItems.FindAsync([id], ct)
            ?? throw new NotFoundException("Item not found");

        Archiving.EnsureDeletable("item",
            new Dependent("job part lines", await db.JobPartLines.CountAsync(p => p.InventoryItemId == id, ct)),
            new Dependent("stock movements", await db.StockMovements.CountAsync(m => m.InventoryItemId == id, ct)));

        db.InventoryItems.Remove(item);
        await db.SaveChangesAsync(ct);
        return;
    }

    public async Task<ArchiveResultDto> ArchiveItemAsync(Guid id, CancellationToken ct)
    {
        var item = await db.InventoryItems.FindAsync([id], ct)
            ?? throw new NotFoundException("Item not found");
        var result = Archiving.Archive(item, id);
        await db.SaveChangesAsync(ct);
        return result;
    }

    public async Task<ArchiveResultDto> UnarchiveItemAsync(Guid id, CancellationToken ct)
    {
        var item = await db.InventoryItems.FindAsync([id], ct)
            ?? throw new NotFoundException("Item not found");
        var result = Archiving.Unarchive(item, id);
        await db.SaveChangesAsync(ct);
        return result;
    }

    public async Task<InventoryItemCreatedDto> CreateItemAsync(CreateItemRequest request, CancellationToken ct)
    {
        await new CreateItemValidator().ValidateAndThrowAsync(request, ct);

        var businessId = currentUser.RequireBusinessId();

        if (!string.IsNullOrWhiteSpace(request.Sku))
        {
            var skuExists = await db.InventoryItems.AnyAsync(i => i.Sku == request.Sku.Trim(), ct);
            if (skuExists) throw new ConflictException("SKU already exists");
        }

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
        db.InventoryItems.Add(item);

        if (request.StockOnHand > 0)
        {
            db.StockMovements.Add(new StockMovement
            {
                BusinessId = businessId,
                InventoryItemId = item.Id,
                QuantityDelta = request.StockOnHand,
                Reason = StockMovementReason.ManualAdjustment,
                Notes = "Initial stock",
                CreatedByUserId = currentUser.UserId
            });
        }

        await db.SaveChangesAsync(ct);
        return new InventoryItemCreatedDto(item.Id, item.Name, item.Sku);
    }

    public async Task<InventoryItemDto> UpdateItemAsync(Guid id, UpdateItemRequest request, CancellationToken ct)
    {
        var item = await db.InventoryItems.FindAsync([id], ct)
            ?? throw new NotFoundException("Inventory item not found");

        if (!string.IsNullOrWhiteSpace(request.Sku) && request.Sku.Trim() != item.Sku)
        {
            var skuExists = await db.InventoryItems.AnyAsync(i => i.Sku == request.Sku.Trim() && i.Id != id, ct);
            if (skuExists) throw new ConflictException("SKU already exists");
        }

        item.Name = request.Name.Trim();
        item.Sku = request.Sku?.Trim();
        item.CategoryId = request.CategoryId;
        item.UnitCost = request.UnitCost;
        item.RetailPrice = request.RetailPrice;
        item.ReorderThreshold = request.ReorderThreshold;
        item.IsConsumable = request.IsConsumable;

        await db.SaveChangesAsync(ct);
        return new InventoryItemDto(item.Id, item.Name, item.Sku, item.CategoryId, null, item.UnitCost, item.RetailPrice, item.StockOnHand, item.ReorderThreshold, item.StockOnHand <= item.ReorderThreshold, item.IsConsumable, item.CreatedAtUtc);
    }

    public async Task<StockLevelDto> AdjustStockAsync(Guid id, AdjustStockRequest request, CancellationToken ct)
    {
        var item = await db.InventoryItems.FindAsync([id], ct)
            ?? throw new NotFoundException("Inventory item not found");

        if (!Enum.TryParse<StockMovementReason>(request.Reason, true, out var reason))
            throw new ValidationException("Invalid reason");

        if (item.StockOnHand + request.QuantityDelta < 0)
            throw new ConflictException("Stock cannot go below zero");

        item.StockOnHand += request.QuantityDelta;

        db.StockMovements.Add(new StockMovement
        {
            BusinessId = item.BusinessId,
            InventoryItemId = id,
            QuantityDelta = request.QuantityDelta,
            Reason = reason,
            Notes = request.Notes,
            CreatedByUserId = currentUser.UserId
        });

        await db.SaveChangesAsync(ct);
        return new StockLevelDto(item.Id, item.StockOnHand);
    }
}
