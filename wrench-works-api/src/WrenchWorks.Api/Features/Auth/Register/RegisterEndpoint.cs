using Microsoft.AspNetCore.Http.HttpResults;

namespace WrenchWorks.Api.Features.Auth.Register;

public static class RegisterEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/auth/register", HandleAsync)
           .WithTags("Auth")
           .WithOpenApi()
           .AllowAnonymous();

    private static async Task<Created<RegisterResponse>> HandleAsync(
        IRegisterService svc, RegisterRequest request, CancellationToken ct)
    {
        var result = await svc.HandleAsync(request, ct);
        return TypedResults.Created("/api/auth/me", result);
    }
}
