using Microsoft.EntityFrameworkCore;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Features.Auth.Login;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Auth.RefreshToken;

public class RefreshTokenService(
    AppDbContext db,
    CurrentUserService currentUser,
    IJwtTokenService jwtService) : IRefreshTokenService
{
    public async Task<LoginResponse?> HandleAsync(CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();
        var businessId = currentUser.RequireBusinessId();

        var businessUser = await db.BusinessUsers
            .IgnoreQueryFilters()
            .Include(bu => bu.User)
            .Include(bu => bu.Business)
            .Include(bu => bu.Roles)
                .ThenInclude(r => r.Role)
                    .ThenInclude(r => r.Permissions)
                        .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(bu => bu.UserId == userId && bu.BusinessId == businessId, ct);

        if (businessUser == null || businessUser.Status != BusinessUserStatus.Active)
            return null;

        var permissions = businessUser.Roles
            .SelectMany(r => r.Role.Permissions)
            .Select(rp => rp.Permission.Key)
            .Distinct()
            .ToList();

        var subscription = await db.BusinessSubscriptions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.BusinessId == businessId, ct);

        var features = new List<string>();
        if (subscription != null)
        {
            if (subscription.InventoryEnabled) features.Add("inventory");
            if (subscription.MessagingEnabled) features.Add("messaging");
        }

        var token = jwtService.GenerateToken(
            businessUser.UserId,
            businessUser.User.Email,
            businessUser.BusinessId,
            businessUser.Id,
            permissions,
            features);

        // Same wire shape as login, so the session contract lives in exactly one record.
        return new LoginResponse(token, new UserDto(
            businessUser.UserId,
            businessUser.User.Name,
            businessUser.User.Email,
            businessUser.BusinessId,
            businessUser.Business.Name,
            // Refresh is how a currency change reaches the session without a re-login:
            // the settings page calls it after saving.
            businessUser.Business.Currency,
            permissions,
            features));
    }
}
