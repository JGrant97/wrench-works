using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Billing;

public interface IBillingService
{
    Task<BusinessSubscription> GetSubscriptionAsync(CancellationToken ct);
    Task<string> CreateCheckoutUrlAsync(CreateCheckoutRequest request, CancellationToken ct);
    Task<string> CreatePortalUrlAsync(CancellationToken ct);

    // Takes the already-read request body rather than HttpContext: reading the stream is
    // the endpoint layer's job, and signature verification will need the raw string.
    Task HandleWebhookAsync(string rawBody, CancellationToken ct);
}
