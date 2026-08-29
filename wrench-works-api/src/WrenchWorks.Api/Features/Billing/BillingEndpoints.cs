using Microsoft.EntityFrameworkCore;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;
using WrenchWorks.Infrastructure.Stripe;

namespace WrenchWorks.Api.Features.Billing;

public record CreateCheckoutRequest(string Plan, string SuccessUrl, string CancelUrl);
public record SubscriptionDto(string Plan, string Status, DateTime? CurrentPeriodEnd, int UserLimit, int ZoneLimit, bool InventoryEnabled, bool MessagingEnabled);

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

    private static async Task<IResult> GetSubscriptionAsync(AppDbContext db, CurrentUserService currentUser, CancellationToken ct)
    {
        var businessId = currentUser.RequireBusinessId();
        var sub = await db.BusinessSubscriptions.FirstOrDefaultAsync(s => s.BusinessId == businessId, ct);
        if (sub == null) return Results.NotFound();

        return Results.Ok(new SubscriptionDto(sub.Plan, sub.Status.ToString(), sub.CurrentPeriodEndUtc, sub.UserLimit, sub.ZoneLimit, sub.InventoryEnabled, sub.MessagingEnabled));
    }

    private static async Task<IResult> CreateCheckoutAsync(
        CreateCheckoutRequest request,
        AppDbContext db,
        CurrentUserService currentUser,
        IStripeService stripeService,
        CancellationToken ct)
    {
        var businessId = currentUser.RequireBusinessId();
        var url = await stripeService.CreateCheckoutSessionAsync(businessId, request.Plan, request.SuccessUrl, request.CancelUrl, ct);
        return Results.Ok(new { url });
    }

    private static async Task<IResult> CreatePortalAsync(
        AppDbContext db,
        CurrentUserService currentUser,
        IStripeService stripeService,
        CancellationToken ct)
    {
        var businessId = currentUser.RequireBusinessId();
        var sub = await db.BusinessSubscriptions.FirstOrDefaultAsync(s => s.BusinessId == businessId, ct)
            ?? throw new NotFoundException("No subscription found");

        if (string.IsNullOrEmpty(sub.StripeCustomerId))
            throw new ConflictException("No Stripe customer linked");

        var url = await stripeService.CreateCustomerPortalSessionAsync(sub.StripeCustomerId, "http://localhost:3000/settings/billing", ct);
        return Results.Ok(new { url });
    }

    private static async Task<IResult> HandleWebhookAsync(
        HttpContext context,
        AppDbContext db,
        IConfiguration config,
        CancellationToken ct)
    {
        // TODO: Verify Stripe signature using config["Stripe:WebhookSecret"]
        // For now, stub implementation
        var body = await new StreamReader(context.Request.Body).ReadToEndAsync(ct);

        // Parse event type and handle:
        // - checkout.session.completed -> create/update subscription
        // - customer.subscription.updated -> update plan/status/limits
        // - customer.subscription.deleted -> mark cancelled
        // - invoice.payment_succeeded -> update status to Active
        // - invoice.payment_failed -> update status to PastDue

        // Idempotency: check event ID hasn't been processed before

        return Results.Ok(new { received = true });
    }
}
