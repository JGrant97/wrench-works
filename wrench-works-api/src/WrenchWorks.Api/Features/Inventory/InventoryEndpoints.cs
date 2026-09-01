using FluentValidation;
using Microsoft.EntityFrameworkCore;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Features.Common;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Inventory;

// DTOs
public record CreateCategoryRequest(string Name);
public record CreateItemRequest(string Name, string? Sku, Guid? CategoryId, decimal UnitCost, decimal? RetailPrice, int StockOnHand, int ReorderThreshold, string? CompatibilityTagsJson, bool IsConsumable = false);
public record UpdateItemRequest(string Name, string? Sku, Guid? CategoryId, decimal UnitCost, decimal? RetailPrice, int ReorderThreshold, bool IsConsumable = false);
public record AdjustStockRequest(int QuantityDelta, string Reason, string? Notes);
public record InventoryCategoryDto(Guid Id, string Name, int ItemCount);
public record InventoryItemDto(Guid Id, string Name, string? Sku, Guid? CategoryId, string? CategoryName, decimal UnitCost, decimal? RetailPrice, int StockOnHand, int ReorderThreshold, bool LowStock, bool IsConsumable, DateTime CreatedAtUtc);

public class CreateItemValidator : AbstractValidator<CreateItemRequest>
{
    public CreateItemValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Sku).MaximumLength(100);
        RuleFor(x => x.UnitCost).GreaterThanOrEqualTo(0);
        RuleFor(x => x.StockOnHand).GreaterThanOrEqualTo(0);
    }
}

public static class InventoryEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory").WithTags("Inventory").RequireAuthorization()
            .AddEndpointFilter(async (ctx, next) =>
            {
                var currentUser = ctx.HttpContext.RequestServices.GetRequiredService<CurrentUserService>();
                if (!currentUser.HasFeature("inventory"))
                    return Results.Json(new { code = "feature_disabled", message = "Inventory is not enabled on your plan" }, statusCode: 403);
                return await next(ctx);
            });

        group.MapGet("/categories", ListCategoriesAsync).RequireAuthorization("inventory.view").Produces<List<InventoryCategoryDto>>();
        group.MapPost("/categories", CreateCategoryAsync).RequireAuthorization("inventory.manage");
        group.MapGet("/items", ListItemsAsync).RequireAuthorization("inventory.view").Produces<PagedResult<InventoryItemDto>>();
        group.MapGet("/items/{id:guid}", GetItemAsync).RequireAuthorization("inventory.view").Produces<InventoryItemDto>();
        group.MapPost("/items", CreateItemAsync).RequireAuthorization("inventory.manage");
        group.MapPut("/items/{id:guid}", UpdateItemAsync).RequireAuthorization("inventory.manage");
        group.MapPost("/items/{id:guid}/adjust", AdjustStockAsync).RequireAuthorization("inventory.manage");
        group.MapDelete("/items/{id:guid}", DeleteItemAsync).RequireAuthorization("inventory.manage");
        group.MapPost("/items/{id:guid}/archive", ArchiveItemAsync).RequireAuthorization("inventory.manage")
             .Produces<ArchiveResultDto>();
        group.MapPost("/items/{id:guid}/unarchive", UnarchiveItemAsync).RequireAuthorization("inventory.manage")
             .Produces<ArchiveResultDto>();
    }

    private static async Task<IResult> ListCategoriesAsync(AppDbContext db, CancellationToken ct)
    {
        var categories = await db.InventoryCategories
            .OrderBy(c => c.Name)
            .Select(c => new InventoryCategoryDto(c.Id, c.Name, c.Items.Count))
            .ToListAsync(ct);
        return Results.Ok(categories);
    }

    private static async Task<IResult> CreateCategoryAsync(
        CreateCategoryRequest request,
        AppDbContext db,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { code = "validation_error", message = "Name required" });

        var exists = await db.InventoryCategories.IgnoreQueryFilters().AnyAsync(c => c.Name == request.Name.Trim(), ct);
        if (exists) throw new ConflictException("Category already exists");

        var cat = new InventoryCategory { Name = request.Name.Trim() };
        db.InventoryCategories.Add(cat);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/inventory/categories/{cat.Id}", new InventoryCategoryDto(cat.Id, cat.Name, 0));
    }

    private static async Task<IResult> ListItemsAsync(
        AppDbContext db,
        int page = 1, int pageSize = 25,
        string? search = null, Guid? categoryId = null,
        bool? lowStockOnly = null,
        bool includeArchived = false,
        CancellationToken ct = default)
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

        return Results.Ok(new PagedResult<InventoryItemDto>(items, total, page, pageSize));
    }

    private static async Task<IResult> GetItemAsync(Guid id, AppDbContext db, CancellationToken ct)
    {
        var item = await db.InventoryItems.Include(i => i.Category).FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new NotFoundException("Inventory item not found");

        return Results.Ok(new InventoryItemDto(item.Id, item.Name, item.Sku, item.CategoryId, item.Category?.Name, item.UnitCost, item.RetailPrice, item.StockOnHand, item.ReorderThreshold, item.StockOnHand <= item.ReorderThreshold, item.IsConsumable, item.CreatedAtUtc));
    }

    private static async Task<IResult> DeleteItemAsync(Guid id, AppDbContext db, CancellationToken ct)
    {
        var item = await db.InventoryItems.FindAsync([id], ct)
            ?? throw new NotFoundException("Item not found");

        Archiving.EnsureDeletable("item",
            new Dependent("job part lines", await db.JobPartLines.CountAsync(p => p.InventoryItemId == id, ct)),
            new Dependent("stock movements", await db.StockMovements.CountAsync(m => m.InventoryItemId == id, ct)));

        db.InventoryItems.Remove(item);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ArchiveItemAsync(Guid id, AppDbContext db, CancellationToken ct)
    {
        var item = await db.InventoryItems.FindAsync([id], ct)
            ?? throw new NotFoundException("Item not found");
        var result = Archiving.Archive(item, id);
        await db.SaveChangesAsync(ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> UnarchiveItemAsync(Guid id, AppDbContext db, CancellationToken ct)
    {
        var item = await db.InventoryItems.FindAsync([id], ct)
            ?? throw new NotFoundException("Item not found");
        var result = Archiving.Unarchive(item, id);
        await db.SaveChangesAsync(ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> CreateItemAsync(
        CreateItemRequest request,
        AppDbContext db,
        CurrentUserService currentUser,
        CancellationToken ct)
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
        return Results.Created($"/api/inventory/items/{item.Id}", new { item.Id, item.Name, item.Sku });
    }

    private static async Task<IResult> UpdateItemAsync(
        Guid id,
        UpdateItemRequest request,
        AppDbContext db,
        CancellationToken ct)
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
        return Results.Ok(new InventoryItemDto(item.Id, item.Name, item.Sku, item.CategoryId, null, item.UnitCost, item.RetailPrice, item.StockOnHand, item.ReorderThreshold, item.StockOnHand <= item.ReorderThreshold, item.IsConsumable, item.CreatedAtUtc));
    }

    private static async Task<IResult> AdjustStockAsync(
        Guid id,
        AdjustStockRequest request,
        AppDbContext db,
        CurrentUserService currentUser,
        CancellationToken ct)
    {
        var item = await db.InventoryItems.FindAsync([id], ct)
            ?? throw new NotFoundException("Inventory item not found");

        if (!Enum.TryParse<StockMovementReason>(request.Reason, true, out var reason))
            return Results.BadRequest(new { code = "validation_error", message = "Invalid reason" });

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
        return Results.Ok(new { item.Id, item.StockOnHand });
    }
}
