namespace WrenchWorks.Api.Features.Auth.Login;

public static class LoginEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/auth/login",
               (LoginRequest request, ILoginEndpointHandler handler, CancellationToken ct) =>
                   handler.HandleAsync(request, ct))
           .WithTags("Auth")
           .WithOpenApi()
           .AllowAnonymous();
}
