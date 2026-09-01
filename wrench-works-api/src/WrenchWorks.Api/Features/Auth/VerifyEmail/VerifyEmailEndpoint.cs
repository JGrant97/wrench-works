using Microsoft.AspNetCore.Http.HttpResults;

namespace WrenchWorks.Api.Features.Auth.VerifyEmail;

public static class VerifyEmailEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/auth/verify-email", HandleAsync)
           .WithTags("Auth")
           .WithOpenApi()
           .AllowAnonymous();

    private static async Task<Ok<VerifyEmailResultDto>> HandleAsync(
        IVerifyEmailService svc, VerifyEmailRequest request, CancellationToken ct) =>
        TypedResults.Ok(await svc.HandleAsync(request, ct));
}
