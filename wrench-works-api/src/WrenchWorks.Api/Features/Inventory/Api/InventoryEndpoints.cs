using WrenchWorks.Api.Auth;

namespace WrenchWorks.Api.Features.Inventory;

public static class InventoryEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        // Plan features are enforced by a group filter, not by the permission system --
        // a new plan-gated feature needs its own filter; nothing enforces this by
        // convention. See the authorization section of docs/app-flow.md.
        var group = app.MapGroup("/api/inventory").WithTags("Inventory").RequireAuthorization()
            .AddEndpointFilter(async (ctx, next) =>
            {
                var currentUser = ctx.HttpContext.RequestServices.GetRequiredService<CurrentUserService>();
                if (!currentUser.HasFeature("inventory"))
                    return TypedResults.Json(
                        new { code = "feature_disabled", message = "Inventory is not enabled on your plan" },
                        statusCode: 403);
                return await next(ctx);
            });

        group.MapGet("/categories",
            (IInventoryEndpointHandler handler, CancellationToken ct) =>
                handler.ListCategoriesAsync(ct))
            .RequireAuthorization("inventory.view");

        group.MapPost("/categories",
            (CreateCategoryRequest request, IInventoryEndpointHandler handler, CancellationToken ct) =>
                handler.CreateCategoryAsync(request, ct))
            .RequireAuthorization("inventory.manage");

        group.MapGet("/items",
            (IInventoryEndpointHandler handler, CancellationToken ct,
             int page = 1, int pageSize = 25, string? search = null,
             Guid? categoryId = null, bool? lowStockOnly = null, bool includeArchived = false) =>
                handler.ListItemsAsync(page, pageSize, search, categoryId, lowStockOnly, includeArchived, ct))
            .RequireAuthorization("inventory.view");

        group.MapGet("/items/{id:guid}",
            (Guid id, IInventoryEndpointHandler handler, CancellationToken ct) =>
                handler.GetItemAsync(id, ct))
            .RequireAuthorization("inventory.view");

        group.MapPost("/items",
            (CreateItemRequest request, IInventoryEndpointHandler handler, CancellationToken ct) =>
                handler.CreateItemAsync(request, ct))
            .RequireAuthorization("inventory.manage");

        group.MapPut("/items/{id:guid}",
            (Guid id, UpdateItemRequest request, IInventoryEndpointHandler handler, CancellationToken ct) =>
                handler.UpdateItemAsync(id, request, ct))
            .RequireAuthorization("inventory.manage");

        group.MapPost("/items/{id:guid}/adjust",
            (Guid id, AdjustStockRequest request, IInventoryEndpointHandler handler, CancellationToken ct) =>
                handler.AdjustStockAsync(id, request, ct))
            .RequireAuthorization("inventory.manage");

        group.MapDelete("/items/{id:guid}",
            (Guid id, IInventoryEndpointHandler handler, CancellationToken ct) =>
                handler.DeleteItemAsync(id, ct))
            .RequireAuthorization("inventory.manage");

        group.MapPost("/items/{id:guid}/archive",
            (Guid id, IInventoryEndpointHandler handler, CancellationToken ct) =>
                handler.ArchiveItemAsync(id, ct))
            .RequireAuthorization("inventory.manage");

        group.MapPost("/items/{id:guid}/unarchive",
            (Guid id, IInventoryEndpointHandler handler, CancellationToken ct) =>
                handler.UnarchiveItemAsync(id, ct))
            .RequireAuthorization("inventory.manage");
    }
}
