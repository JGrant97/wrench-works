using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Features.Auth.Login;
using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Auth.RefreshToken;

public class RefreshTokenService(
    IRefreshTokenRepository repository,
    CurrentUserService currentUser,
    IJwtTokenService jwtService) : IRefreshTokenService
{
    public async Task<LoginSession?> HandleAsync(CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();
        var businessId = currentUser.RequireBusinessId();

        var membership = await repository.FindMembershipWithRolesAsync(userId, businessId, ct);
        if (membership == null || membership.Status != BusinessUserStatus.Active)
            return null;

        var permissions = membership.Roles
            .SelectMany(r => r.Role.Permissions)
            .Select(rp => rp.Permission.Key)
            .Distinct()
            .ToList();

        var subscription = await repository.GetSubscriptionAsync(businessId, ct);
        var features = new List<string>();
        if (subscription != null)
        {
            if (subscription.InventoryEnabled) features.Add("inventory");
            if (subscription.MessagingEnabled) features.Add("messaging");
        }

        var token = jwtService.GenerateToken(
            membership.UserId, membership.User.Email, membership.BusinessId,
            membership.Id, permissions, features);

        // Refresh is how a currency change reaches the session without a re-login: the
        // settings page calls it after saving, and Business.Currency rides in the response.
        return new LoginSession(token, membership.User, membership, permissions, features);
    }
}
