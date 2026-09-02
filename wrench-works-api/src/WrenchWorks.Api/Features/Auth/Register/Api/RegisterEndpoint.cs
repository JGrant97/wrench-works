namespace WrenchWorks.Api.Features.Auth.Register;

public static class RegisterEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/auth/register",
               (RegisterRequest request, IRegisterEndpointHandler handler, CancellationToken ct) =>
                   handler.HandleAsync(request, ct))
           .WithTags("Auth")
           .WithOpenApi()
           .AllowAnonymous();
}
