using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Auth.Login;

// Anonymous endpoint: it runs before a tenant context exists, so these reads ignore the
// global query filters by necessity. See the multi-tenancy note in CLAUDE.md.
public interface ILoginRepository
{
    Task<User?> FindUserWithActiveMembershipsAsync(string normalizedEmail, CancellationToken ct);
    Task<BusinessSubscription?> GetSubscriptionAsync(Guid businessId, CancellationToken ct);
}
