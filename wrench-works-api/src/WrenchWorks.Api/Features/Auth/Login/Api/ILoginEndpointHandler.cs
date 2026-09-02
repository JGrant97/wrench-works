using Microsoft.AspNetCore.Http.HttpResults;

namespace WrenchWorks.Api.Features.Auth.Login;

public interface ILoginEndpointHandler
{
    Task<Results<Ok<LoginResponse>, UnauthorizedHttpResult, ProblemHttpResult>> HandleAsync(
        LoginRequest request, CancellationToken ct);
}
