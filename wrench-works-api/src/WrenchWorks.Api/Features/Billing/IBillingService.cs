namespace WrenchWorks.Api.Features.Billing;

// The Billing slice behind an interface: the endpoints become a thin HTTP shell.
// Methods return DTOs, not IResult -- failures are thrown and mapped by
// ErrorHandlingMiddleware, so nothing here needs to know about status codes.
public interface IBillingService
{
    Task<SubscriptionDto> GetSubscriptionAsync(CancellationToken ct);
    Task<CheckoutUrlDto> CreateCheckoutAsync(CreateCheckoutRequest request, CancellationToken ct);
    Task<CheckoutUrlDto> CreatePortalAsync(CancellationToken ct);

    // Takes the already-read request body rather than HttpContext: reading the stream is
    // the endpoint's job, and signature verification will need the raw string anyway.
    Task<WebhookAckDto> HandleWebhookAsync(string rawBody, CancellationToken ct);
}
