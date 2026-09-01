using Microsoft.AspNetCore.Http.HttpResults;

namespace WrenchWorks.Api.Features.Billing;

public static class BillingEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/billing").WithTags("Billing").RequireAuthorization();

        group.MapGet("/subscription", GetSubscriptionAsync).RequireAuthorization();
        group.MapPost("/checkout", CreateCheckoutAsync).RequireAuthorization("billing.manage");
        group.MapPost("/portal", CreatePortalAsync).RequireAuthorization("billing.manage");
        group.MapPost("/webhook", HandleWebhookAsync).AllowAnonymous();
    }

    private static async Task<Ok<SubscriptionDto>> GetSubscriptionAsync(
        IBillingService svc, CancellationToken ct) =>
        TypedResults.Ok(await svc.GetSubscriptionAsync(ct));

    private static async Task<Ok<CheckoutUrlDto>> CreateCheckoutAsync(
        IBillingService svc, CreateCheckoutRequest request, CancellationToken ct) =>
        TypedResults.Ok(await svc.CreateCheckoutAsync(request, ct));

    private static async Task<Ok<CheckoutUrlDto>> CreatePortalAsync(
        IBillingService svc, CancellationToken ct) =>
        TypedResults.Ok(await svc.CreatePortalAsync(ct));

    // Reading the body is an HTTP concern, so it happens here and the service takes a string.
    private static async Task<Ok<WebhookAckDto>> HandleWebhookAsync(
        HttpContext context, IBillingService svc, CancellationToken ct)
    {
        var rawBody = await new StreamReader(context.Request.Body).ReadToEndAsync(ct);
        return TypedResults.Ok(await svc.HandleWebhookAsync(rawBody, ct));
    }
}
