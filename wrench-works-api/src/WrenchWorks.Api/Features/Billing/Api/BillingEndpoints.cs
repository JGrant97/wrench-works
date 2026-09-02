namespace WrenchWorks.Api.Features.Billing;

public static class BillingEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/billing").WithTags("Billing").RequireAuthorization();

        group.MapGet("/subscription",
            (IBillingEndpointHandler handler, CancellationToken ct) =>
                handler.GetSubscriptionAsync(ct))
            .RequireAuthorization();

        group.MapPost("/checkout",
            (CreateCheckoutRequest request, IBillingEndpointHandler handler, CancellationToken ct) =>
                handler.CreateCheckoutAsync(request, ct))
            .RequireAuthorization("billing.manage");

        group.MapPost("/portal",
            (IBillingEndpointHandler handler, CancellationToken ct) =>
                handler.CreatePortalAsync(ct))
            .RequireAuthorization("billing.manage");

        // Reading the body is an HTTP concern, so it happens here and the handler takes a
        // string. Stripe signature verification will need these exact bytes.
        group.MapPost("/webhook",
            async (HttpContext context, IBillingEndpointHandler handler, CancellationToken ct) =>
            {
                var rawBody = await new StreamReader(context.Request.Body).ReadToEndAsync(ct);
                return await handler.HandleWebhookAsync(rawBody, ct);
            })
            .AllowAnonymous();
    }
}
