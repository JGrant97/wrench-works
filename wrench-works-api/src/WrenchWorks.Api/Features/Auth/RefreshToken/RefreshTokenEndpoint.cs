using Microsoft.EntityFrameworkCore;
using WrenchWorks.Api.Auth;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Auth.RefreshToken;

public static class RefreshTokenEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/auth/refresh", HandleAsync)
           .WithTags("Auth")
           .WithOpenApi()
           .RequireAuthorization();

    private static async Task<IResult> HandleAsync(
        AppDbContext db,
        CurrentUserService currentUser,
        IJwtTokenService jwtService,
        CancellationToken ct)
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
            return Results.Unauthorized();

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

        return Results.Ok(new
        {
            token,
            user = new
            {
                id = businessUser.UserId,
                name = businessUser.User.Name,
                email = businessUser.User.Email,
                businessId = businessUser.BusinessId,
                businessName = businessUser.Business.Name,
                // Refresh is how a currency change reaches the session without a re-login:
                // the settings page calls it after saving.
                currency = businessUser.Business.Currency,
                permissions,
                features
            }
        });
    }
}
