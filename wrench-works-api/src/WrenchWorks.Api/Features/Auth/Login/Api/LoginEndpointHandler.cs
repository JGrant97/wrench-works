using Microsoft.AspNetCore.Http.HttpResults;

namespace WrenchWorks.Api.Features.Auth.Login;

public class LoginEndpointHandler(ILoginService service) : ILoginEndpointHandler
{
    /// <summary>
    /// The session payload for both login and refresh. Public and static because
    /// RefreshTokenEndpointHandler returns the same shape, and the two drifting apart
    /// would silently change what the ww_user cookie holds after a currency change.
    /// </summary>
    public static LoginResponse ToResponse(LoginSession session) =>
        new(session.Token, new UserDto(
            session.User.Id,
            session.User.Name,
            session.User.Email,
            session.Membership.BusinessId,
            session.Membership.Business.Name,
            session.Membership.Business.Currency,
            session.Permissions,
            session.Features));

    // The status codes are the contract here, so the mapping is deliberate and explicit:
    // 401 means the credentials are wrong, 403 means the account is real but may not sign
    // in. The web login page distinguishes them.
    public async Task<Results<Ok<LoginResponse>, UnauthorizedHttpResult, ProblemHttpResult>> HandleAsync(
        LoginRequest request, CancellationToken ct)
    {
        var outcome = await service.HandleAsync(request, ct);

        return outcome.Failure switch
        {
            LoginFailure.InvalidCredentials => TypedResults.Unauthorized(),
            LoginFailure.EmailNotVerified => TypedResults.Problem("Email not verified", statusCode: 403),
            LoginFailure.NoMembership => TypedResults.Problem("No active business membership", statusCode: 403),
            _ => TypedResults.Ok(ToResponse(outcome.Session!))
        };
    }
}
