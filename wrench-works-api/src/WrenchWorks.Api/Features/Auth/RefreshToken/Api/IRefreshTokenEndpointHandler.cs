using Microsoft.AspNetCore.Http.HttpResults;
using WrenchWorks.Api.Features.Auth.Login;

namespace WrenchWorks.Api.Features.Auth.RefreshToken;

public interface IRefreshTokenEndpointHandler
{
    Task<Results<Ok<LoginResponse>, UnauthorizedHttpResult>> HandleAsync(CancellationToken ct);
}
