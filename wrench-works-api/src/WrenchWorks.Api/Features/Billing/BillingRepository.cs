using Microsoft.EntityFrameworkCore;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Billing;

public class BillingRepository(AppDbContext db) : IBillingRepository
{
    public Task<BusinessSubscription?> GetSubscriptionAsync(Guid businessId, CancellationToken ct) =>
        db.BusinessSubscriptions.FirstOrDefaultAsync(s => s.BusinessId == businessId, ct);
}
