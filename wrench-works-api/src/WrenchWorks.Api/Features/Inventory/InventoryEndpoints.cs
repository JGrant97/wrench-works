using Microsoft.AspNetCore.Http.HttpResults;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Features.Common;

namespace WrenchWorks.Api.Features.Inventory;

public static class InventoryEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory").WithTags("Inventory").RequireAuthorization()
            .AddEndpointFilter(async (ctx, next) =>
            {
                var currentUser = ctx.HttpContext.RequestServices.GetRequiredService<CurrentUserService>();
                if (!currentUser.HasFeature("inventory"))
                    return TypedResults.Json(new { code = "feature_disabled", message = "Inventory is not enabled on your plan" }, statusCode: 403);
                return await next(ctx);
            });

        group.MapGet("/categories", ListCategoriesAsync).RequireAuthorization("inventory.view");
        group.MapPost("/categories", CreateCategoryAsync).RequireAuthorization("inventory.manage");
        group.MapGet("/items", ListItemsAsync).RequireAuthorization("inventory.view");
        group.MapGet("/items/{id:guid}", GetItemAsync).RequireAuthorization("inventory.view");
        group.MapPost("/items", CreateItemAsync).RequireAuthorization("inventory.manage");
        group.MapPut("/items/{id:guid}", UpdateItemAsync).RequireAuthorization("inventory.manage");
        group.MapPost("/items/{id:guid}/adjust", AdjustStockAsync).RequireAuthorization("inventory.manage");
        group.MapDelete("/items/{id:guid}", DeleteItemAsync).RequireAuthorization("inventory.manage");
        group.MapPost("/items/{id:guid}/archive", ArchiveItemAsync).RequireAuthorization("inventory.manage");
        group.MapPost("/items/{id:guid}/unarchive", UnarchiveItemAsync).RequireAuthorization("inventory.manage");
    }

    private static async Task<Ok<List<InventoryCategoryDto>>> ListCategoriesAsync(IInventoryService svc, CancellationToken ct) =>
        TypedResults.Ok(await svc.ListCategoriesAsync(ct));

    private static async Task<Created<InventoryCategoryDto>> CreateCategoryAsync(IInventoryService svc, CreateCategoryRequest request, CancellationToken ct)
    {
        var result = await svc.CreateCategoryAsync(request, ct);
        return TypedResults.Created($"/api/inventory/categories/{result.Id}", result);
    }

    private static async Task<Ok<PagedResult<InventoryItemDto>>> ListItemsAsync(IInventoryService svc, int page = 1, int pageSize = 25, string? search = null, Guid? categoryId = null, bool? lowStockOnly = null, bool includeArchived = false, CancellationToken ct = default) =>
        TypedResults.Ok(await svc.ListItemsAsync(page, pageSize, search, categoryId, lowStockOnly, includeArchived, ct));

    private static async Task<Ok<InventoryItemDto>> GetItemAsync(IInventoryService svc, Guid id, CancellationToken ct) =>
        TypedResults.Ok(await svc.GetItemAsync(id, ct));

    private static async Task<NoContent> DeleteItemAsync(IInventoryService svc, Guid id, CancellationToken ct)
    {
        await svc.DeleteItemAsync(id, ct);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<ArchiveResultDto>> ArchiveItemAsync(IInventoryService svc, Guid id, CancellationToken ct) =>
        TypedResults.Ok(await svc.ArchiveItemAsync(id, ct));

    private static async Task<Ok<ArchiveResultDto>> UnarchiveItemAsync(IInventoryService svc, Guid id, CancellationToken ct) =>
        TypedResults.Ok(await svc.UnarchiveItemAsync(id, ct));

    private static async Task<Created<InventoryItemCreatedDto>> CreateItemAsync(IInventoryService svc, CreateItemRequest request, CancellationToken ct)
    {
        var result = await svc.CreateItemAsync(request, ct);
        return TypedResults.Created($"/api/inventory/items/{result.Id}", result);
    }

    private static async Task<Ok<InventoryItemDto>> UpdateItemAsync(IInventoryService svc, Guid id, UpdateItemRequest request, CancellationToken ct) =>
        TypedResults.Ok(await svc.UpdateItemAsync(id, request, ct));

    private static async Task<Ok<StockLevelDto>> AdjustStockAsync(IInventoryService svc, Guid id, AdjustStockRequest request, CancellationToken ct) =>
        TypedResults.Ok(await svc.AdjustStockAsync(id, request, ct));
}
