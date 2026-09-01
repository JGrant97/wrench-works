using FluentValidation;
using Microsoft.EntityFrameworkCore;
using WrenchWorks.Api.Auth;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Auth.Login;

public class LoginService(AppDbContext db, IJwtTokenService jwtService) : ILoginService
{
    public async Task<LoginOutcome> HandleAsync(LoginRequest request, CancellationToken ct)
    {
        var validator = new LoginValidator();
        await validator.ValidateAndThrowAsync(request, ct);

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await db.Users
            .Include(u => u.BusinessUsers.Where(bu => bu.Status == Domain.Entities.BusinessUserStatus.Active))
                .ThenInclude(bu => bu.Business)
            .Include(u => u.BusinessUsers.Where(bu => bu.Status == Domain.Entities.BusinessUserStatus.Active))
                .ThenInclude(bu => bu.Roles)
                    .ThenInclude(r => r.Role)
                        .ThenInclude(r => r.Permissions)
                            .ThenInclude(rp => rp.Permission)
            .AsSplitQuery()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, ct);

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return LoginOutcome.Failed(LoginFailure.InvalidCredentials);

        if (!user.EmailVerified)
            return LoginOutcome.Failed(LoginFailure.EmailNotVerified);

        var businessUser = user.BusinessUsers.FirstOrDefault();
        if (businessUser == null)
            return LoginOutcome.Failed(LoginFailure.NoMembership);

        var permissions = businessUser.Roles
            .SelectMany(r => r.Role.Permissions)
            .Select(rp => rp.Permission.Key)
            .Distinct()
            .ToList();

        // Load subscription feature flags
        var subscription = await db.BusinessSubscriptions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.BusinessId == businessUser.BusinessId, ct);

        var features = new List<string>();
        if (subscription != null)
        {
            if (subscription.InventoryEnabled) features.Add("inventory");
            if (subscription.MessagingEnabled) features.Add("messaging");
        }

        var token = jwtService.GenerateToken(user.Id, user.Email, businessUser.BusinessId,
            businessUser.Id, permissions, features);

        return LoginOutcome.Success(new LoginResponse(token, new UserDto(
            user.Id,
            user.Name,
            user.Email,
            businessUser.BusinessId,
            businessUser.Business.Name,
            businessUser.Business.Currency,
            permissions,
            features)));
    }
}
