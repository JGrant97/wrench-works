using FluentValidation;
using WrenchWorks.Api.Auth;

namespace WrenchWorks.Api.Features.Auth.Login;

public class LoginService(ILoginRepository repository, IJwtTokenService jwtService) : ILoginService
{
    public async Task<LoginOutcome> HandleAsync(LoginRequest request, CancellationToken ct)
    {
        await new LoginValidator().ValidateAndThrowAsync(request, ct);

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await repository.FindUserWithActiveMembershipsAsync(normalizedEmail, ct);

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return LoginOutcome.Failed(LoginFailure.InvalidCredentials);

        if (!user.EmailVerified)
            return LoginOutcome.Failed(LoginFailure.EmailNotVerified);

        var membership = user.BusinessUsers.FirstOrDefault();
        if (membership == null)
            return LoginOutcome.Failed(LoginFailure.NoMembership);

        var permissions = membership.Roles
            .SelectMany(r => r.Role.Permissions)
            .Select(rp => rp.Permission.Key)
            .Distinct()
            .ToList();

        var subscription = await repository.GetSubscriptionAsync(membership.BusinessId, ct);
        var features = new List<string>();
        if (subscription != null)
        {
            if (subscription.InventoryEnabled) features.Add("inventory");
            if (subscription.MessagingEnabled) features.Add("messaging");
        }

        var token = jwtService.GenerateToken(user.Id, user.Email, membership.BusinessId,
            membership.Id, permissions, features);

        return LoginOutcome.Success(new LoginSession(token, user, membership, permissions, features));
    }
}
