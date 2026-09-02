using Microsoft.AspNetCore.Http.HttpResults;

namespace WrenchWorks.Api.Features.Auth.VerifyEmail;

public class VerifyEmailEndpointHandler(IVerifyEmailService service) : IVerifyEmailEndpointHandler
{
    public async Task<Ok<VerifyEmailResultDto>> HandleAsync(VerifyEmailRequest request, CancellationToken ct)
    {
        var outcome = await service.HandleAsync(request, ct);

        return TypedResults.Ok(new VerifyEmailResultDto(outcome switch
        {
            VerifyEmailOutcome.AlreadyVerified => "Email already verified",
            _ => "Email verified successfully"
        }));
    }
}
