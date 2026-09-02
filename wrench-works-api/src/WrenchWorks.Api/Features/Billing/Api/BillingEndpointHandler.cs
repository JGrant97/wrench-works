using Microsoft.AspNetCore.Http.HttpResults;

namespace WrenchWorks.Api.Features.Billing;

public class BillingEndpointHandler(IBillingService service) : IBillingEndpointHandler
{
    public async Task<Ok<SubscriptionDto>> GetSubscriptionAsync(CancellationToken ct)
    {
        var s = await service.GetSubscriptionAsync(ct);
        return TypedResults.Ok(new SubscriptionDto(
            s.Plan, s.Status.ToString(), s.CurrentPeriodEndUtc,
            s.UserLimit, s.ZoneLimit, s.InventoryEnabled, s.MessagingEnabled));
    }

    public async Task<Ok<CheckoutUrlDto>> CreateCheckoutAsync(CreateCheckoutRequest request, CancellationToken ct) =>
        TypedResults.Ok(new CheckoutUrlDto(await service.CreateCheckoutUrlAsync(request, ct)));

    public async Task<Ok<CheckoutUrlDto>> CreatePortalAsync(CancellationToken ct) =>
        TypedResults.Ok(new CheckoutUrlDto(await service.CreatePortalUrlAsync(ct)));

    public async Task<Ok<WebhookAckDto>> HandleWebhookAsync(string rawBody, CancellationToken ct)
    {
        await service.HandleWebhookAsync(rawBody, ct);
        return TypedResults.Ok(new WebhookAckDto(true));
    }
}
