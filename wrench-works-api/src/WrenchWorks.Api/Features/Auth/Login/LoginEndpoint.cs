using Microsoft.AspNetCore.Http.HttpResults;

namespace WrenchWorks.Api.Features.Auth.Login;

public static class LoginEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/auth/login", HandleAsync)
           .WithTags("Auth")
           .WithOpenApi()
           .AllowAnonymous();

    // The status codes are the contract here, so the mapping is deliberate and explicit.
    // 401 for "these credentials are wrong", 403 for "this account exists but may not
    // sign in" -- the web login page distinguishes them.
    private static async Task<Results<Ok<LoginResponse>, UnauthorizedHttpResult, ProblemHttpResult>> HandleAsync(
        ILoginService svc, LoginRequest request, CancellationToken ct)
    {
        var outcome = await svc.HandleAsync(request, ct);

        return outcome.Failure switch
        {
            LoginFailure.InvalidCredentials => TypedResults.Unauthorized(),
            LoginFailure.EmailNotVerified => TypedResults.Problem("Email not verified", statusCode: 403),
            LoginFailure.NoMembership => TypedResults.Problem("No active business membership", statusCode: 403),
            _ => TypedResults.Ok(outcome.Response!)
        };
    }
}
