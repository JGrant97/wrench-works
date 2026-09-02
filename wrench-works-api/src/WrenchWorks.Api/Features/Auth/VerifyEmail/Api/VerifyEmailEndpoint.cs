namespace WrenchWorks.Api.Features.Auth.VerifyEmail;

public static class VerifyEmailEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/auth/verify-email",
               (VerifyEmailRequest request, IVerifyEmailEndpointHandler handler, CancellationToken ct) =>
                   handler.HandleAsync(request, ct))
           .WithTags("Auth")
           .WithOpenApi()
           .AllowAnonymous();
}
