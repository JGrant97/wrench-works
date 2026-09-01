using Microsoft.EntityFrameworkCore;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Infrastructure.Persistence;
using WrenchWorks.Infrastructure.Stripe;

namespace WrenchWorks.Api.Features.Billing;

public class BillingService(
    AppDbContext db,
    CurrentUserService currentUser,
    IStripeService stripeService,
    IConfiguration config) : IBillingService
{
    public async Task<SubscriptionDto> GetSubscriptionAsync(CancellationToken ct)
    {
        var businessId = currentUser.RequireBusinessId();
        var sub = await db.BusinessSubscriptions.FirstOrDefaultAsync(s => s.BusinessId == businessId, ct);
        if (sub == null) throw new NotFoundException("No subscription found for this business");

        return new SubscriptionDto(sub.Plan, sub.Status.ToString(), sub.CurrentPeriodEndUtc,
            sub.UserLimit, sub.ZoneLimit, sub.InventoryEnabled, sub.MessagingEnabled);
    }

    public async Task<CheckoutUrlDto> CreateCheckoutAsync(CreateCheckoutRequest request, CancellationToken ct)
    {
        var businessId = currentUser.RequireBusinessId();
        var url = await stripeService.CreateCheckoutSessionAsync(
            businessId, request.Plan, request.SuccessUrl, request.CancelUrl, ct);
        return new CheckoutUrlDto(url);
    }

    public async Task<CheckoutUrlDto> CreatePortalAsync(CancellationToken ct)
    {
        var businessId = currentUser.RequireBusinessId();
        var sub = await db.BusinessSubscriptions.FirstOrDefaultAsync(s => s.BusinessId == businessId, ct)
            ?? throw new NotFoundException("No subscription found");

        if (string.IsNullOrEmpty(sub.StripeCustomerId))
            throw new ConflictException("No Stripe customer linked");

        var url = await stripeService.CreateCustomerPortalSessionAsync(
            sub.StripeCustomerId, "http://localhost:3000/settings/billing", ct);
        return new CheckoutUrlDto(url);
    }

    public Task<WebhookAckDto> HandleWebhookAsync(string rawBody, CancellationToken ct)
    {
        // TODO: Verify Stripe signature using config["Stripe:WebhookSecret"] against rawBody.
        // This endpoint is AllowAnonymous, so until that lands anyone can post to it --
        // see finding 11 in docs/review-findings.md. StripeService is stubbed, so there is
        // nothing to forge into yet; that stops being true the moment it is implemented.
        _ = config;
        _ = rawBody;

        // Parse event type and handle:
        // - checkout.session.completed -> create/update subscription
        // - customer.subscription.updated -> update plan/status/limits
        // - customer.subscription.deleted -> mark cancelled
        // - invoice.payment_succeeded -> update status to Active
        // - invoice.payment_failed -> update status to PastDue

        // Idempotency: check event ID hasn't been processed before

        return Task.FromResult(new WebhookAckDto(true));
    }
}
