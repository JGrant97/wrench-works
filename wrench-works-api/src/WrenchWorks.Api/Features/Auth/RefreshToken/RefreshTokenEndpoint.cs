using Microsoft.AspNetCore.Http.HttpResults;
using WrenchWorks.Api.Features.Auth.Login;

namespace WrenchWorks.Api.Features.Auth.RefreshToken;

public static class RefreshTokenEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/auth/refresh", HandleAsync)
           .WithTags("Auth")
           .WithOpenApi()
           .RequireAuthorization();

    private static async Task<Results<Ok<LoginResponse>, UnauthorizedHttpResult>> HandleAsync(
        IRefreshTokenService svc, CancellationToken ct)
    {
        var response = await svc.HandleAsync(ct);
        return response is null ? TypedResults.Unauthorized() : TypedResults.Ok(response);
    }
}
