using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Auth.RefreshToken;

public interface IRefreshTokenRepository
{
    Task<BusinessUser?> FindMembershipWithRolesAsync(Guid userId, Guid businessId, CancellationToken ct);
    Task<BusinessSubscription?> GetSubscriptionAsync(Guid businessId, CancellationToken ct);
}
