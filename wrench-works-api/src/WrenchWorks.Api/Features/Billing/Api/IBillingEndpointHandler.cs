using Microsoft.AspNetCore.Http.HttpResults;

namespace WrenchWorks.Api.Features.Billing;

public interface IBillingEndpointHandler
{
    Task<Ok<SubscriptionDto>> GetSubscriptionAsync(CancellationToken ct);
    Task<Ok<CheckoutUrlDto>> CreateCheckoutAsync(CreateCheckoutRequest request, CancellationToken ct);
    Task<Ok<CheckoutUrlDto>> CreatePortalAsync(CancellationToken ct);
    Task<Ok<WebhookAckDto>> HandleWebhookAsync(string rawBody, CancellationToken ct);
}
