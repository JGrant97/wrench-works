namespace WrenchWorks.Infrastructure.Stripe;

public interface IStripeService
{
    Task<string> CreateCheckoutSessionAsync(Guid businessId, string plan, string successUrl, string cancelUrl, CancellationToken ct = default);
    Task<string> CreateCustomerPortalSessionAsync(string stripeCustomerId, string returnUrl, CancellationToken ct = default);
}

public class StripeService : IStripeService
{
    public Task<string> CreateCheckoutSessionAsync(Guid businessId, string plan, string successUrl, string cancelUrl, CancellationToken ct = default)
    {
        // TODO: Implement with Stripe.net SDK
        return Task.FromResult($"https://checkout.stripe.com/placeholder?business={businessId}&plan={plan}");
    }

    public Task<string> CreateCustomerPortalSessionAsync(string stripeCustomerId, string returnUrl, CancellationToken ct = default)
    {
        return Task.FromResult($"https://billing.stripe.com/placeholder?customer={stripeCustomerId}");
    }
}
