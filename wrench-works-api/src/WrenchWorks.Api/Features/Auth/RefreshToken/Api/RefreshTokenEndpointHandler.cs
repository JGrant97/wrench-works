using Microsoft.AspNetCore.Http.HttpResults;
using WrenchWorks.Api.Features.Auth.Login;

namespace WrenchWorks.Api.Features.Auth.RefreshToken;

public class RefreshTokenEndpointHandler(IRefreshTokenService service) : IRefreshTokenEndpointHandler
{
    public async Task<Results<Ok<LoginResponse>, UnauthorizedHttpResult>> HandleAsync(CancellationToken ct)
    {
        var session = await service.HandleAsync(ct);

        // Same mapper as login, so the two payloads cannot drift.
        return session is null
            ? TypedResults.Unauthorized()
            : TypedResults.Ok(LoginEndpointHandler.ToResponse(session));
    }
}
