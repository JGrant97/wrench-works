namespace WrenchWorks.Api.Features.Auth.RefreshToken;

public static class RefreshTokenEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/auth/refresh",
               (IRefreshTokenEndpointHandler handler, CancellationToken ct) =>
                   handler.HandleAsync(ct))
           .WithTags("Auth")
           .WithOpenApi()
           .RequireAuthorization();
}
