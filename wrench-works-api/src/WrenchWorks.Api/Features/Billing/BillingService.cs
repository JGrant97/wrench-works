using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Stripe;

namespace WrenchWorks.Api.Features.Billing;

public class BillingService(
    IBillingRepository repository,
    CurrentUserService currentUser,
    IStripeService stripeService,
    IConfiguration config) : IBillingService
{
    public async Task<BusinessSubscription> GetSubscriptionAsync(CancellationToken ct) =>
        await repository.GetSubscriptionAsync(currentUser.RequireBusinessId(), ct)
            ?? throw new NotFoundException("No subscription found for this business");

    public async Task<string> CreateCheckoutUrlAsync(CreateCheckoutRequest request, CancellationToken ct)
    {
        var businessId = currentUser.RequireBusinessId();
        return await stripeService.CreateCheckoutSessionAsync(
            businessId, request.Plan, request.SuccessUrl, request.CancelUrl, ct);
    }

    public async Task<string> CreatePortalUrlAsync(CancellationToken ct)
    {
        var subscription = await repository.GetSubscriptionAsync(currentUser.RequireBusinessId(), ct)
            ?? throw new NotFoundException("No subscription found");

        if (string.IsNullOrEmpty(subscription.StripeCustomerId))
            throw new ConflictException("No Stripe customer linked");

        return await stripeService.CreateCustomerPortalSessionAsync(
            subscription.StripeCustomerId, "http://localhost:3000/settings/billing", ct);
    }

    public Task HandleWebhookAsync(string rawBody, CancellationToken ct)
    {
        // TODO: Verify the Stripe signature using config["Stripe:WebhookSecret"] against
        // rawBody. This endpoint is AllowAnonymous, so until that lands anyone can post to
        // it -- finding 11 in docs/review-findings.md. StripeService is stubbed, so there
        // is nothing to forge into yet; that stops being true the moment it is implemented.
        _ = config;
        _ = rawBody;

        // Parse event type and handle:
        // - checkout.session.completed -> create/update subscription
        // - customer.subscription.updated -> update plan/status/limits
        // - customer.subscription.deleted -> mark cancelled
        // - invoice.payment_succeeded -> update status to Active
        // - invoice.payment_failed -> update status to PastDue
        // Idempotency: check the event ID has not been processed before.

        return Task.CompletedTask;
    }
}
