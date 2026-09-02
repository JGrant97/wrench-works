using WrenchWorks.Api.Auth;

namespace WrenchWorks.Api.Features.Messaging;

public static class MessagingEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        // Plan features are enforced by a group filter, not by the permission system --
        // a new plan-gated feature needs its own filter; nothing enforces this by
        // convention. See the authorization section of docs/app-flow.md.
        var group = app.MapGroup("/api/messaging").WithTags("Messaging").RequireAuthorization()
            .AddEndpointFilter(async (ctx, next) =>
            {
                var currentUser = ctx.HttpContext.RequestServices.GetRequiredService<CurrentUserService>();
                if (!currentUser.HasFeature("messaging"))
                    return TypedResults.Json(
                        new { code = "feature_disabled", message = "Messaging is not enabled on your plan" },
                        statusCode: 403);
                return await next(ctx);
            });

        group.MapPost("/send",
            (SendMessageRequest request, IMessagingEndpointHandler handler, CancellationToken ct) =>
                handler.SendAsync(request, ct))
            .RequireAuthorization("messaging.send");

        group.MapGet("/",
            (IMessagingEndpointHandler handler, CancellationToken ct,
             Guid? customerId = null, Guid? jobId = null, int page = 1, int pageSize = 25) =>
                handler.ListAsync(customerId, jobId, page < 1 ? 1 : page, pageSize < 1 ? 25 : pageSize, ct))
            .RequireAuthorization("messaging.view");

        group.MapPost("/{id:guid}/retry",
            (Guid id, IMessagingEndpointHandler handler, CancellationToken ct) =>
                handler.RetryAsync(id, ct))
            .RequireAuthorization("messaging.send");
    }
}
