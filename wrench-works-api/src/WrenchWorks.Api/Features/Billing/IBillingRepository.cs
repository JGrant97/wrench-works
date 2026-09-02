using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Billing;

public interface IBillingRepository
{
    Task<BusinessSubscription?> GetSubscriptionAsync(Guid businessId, CancellationToken ct);
}
