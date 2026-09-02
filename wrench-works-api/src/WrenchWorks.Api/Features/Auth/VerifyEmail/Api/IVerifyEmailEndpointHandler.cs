using Microsoft.AspNetCore.Http.HttpResults;

namespace WrenchWorks.Api.Features.Auth.VerifyEmail;

public interface IVerifyEmailEndpointHandler
{
    Task<Ok<VerifyEmailResultDto>> HandleAsync(VerifyEmailRequest request, CancellationToken ct);
}
