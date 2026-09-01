using Microsoft.AspNetCore.Http.HttpResults;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Features.Common;

namespace WrenchWorks.Api.Features.Messaging;

public static class MessagingEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/messaging").WithTags("Messaging").RequireAuthorization()
            .AddEndpointFilter(async (ctx, next) =>
            {
                var currentUser = ctx.HttpContext.RequestServices.GetRequiredService<CurrentUserService>();
                if (!currentUser.HasFeature("messaging"))
                    return TypedResults.Json(new { code = "feature_disabled", message = "Messaging is not enabled on your plan" }, statusCode: 403);
                return await next(ctx);
            });

        group.MapPost("/send", SendAsync).RequireAuthorization("messaging.send");
        group.MapGet("/", ListAsync).RequireAuthorization("messaging.view");
        group.MapPost("/{id:guid}/retry", RetryAsync).RequireAuthorization("messaging.send");
    }

    private static async Task<Created<MessageDto>> SendAsync(IMessagingService svc, SendMessageRequest request, CancellationToken ct)
    {
        var result = await svc.SendAsync(request, ct);
        return TypedResults.Created($"/api/messaging/{result.Id}", result);
    }

    private static async Task<Ok<PagedResult<MessageDto>>> ListAsync(IMessagingService svc, Guid? customerId = null, Guid? jobId = null, int page = 1, int pageSize = 25, CancellationToken ct = default) =>
        TypedResults.Ok(await svc.ListAsync(customerId, jobId, page, pageSize, ct));

    private static async Task<Ok<MessageStatusDto>> RetryAsync(IMessagingService svc, Guid id, CancellationToken ct) =>
        TypedResults.Ok(await svc.RetryAsync(id, ct));
}
