using Microsoft.AspNetCore.Http.HttpResults;

namespace WrenchWorks.Api.Features.Auth.Register;

public class RegisterEndpointHandler(IRegisterService service) : IRegisterEndpointHandler
{
    public async Task<Created<RegisterResponse>> HandleAsync(RegisterRequest request, CancellationToken ct)
    {
        var result = await service.HandleAsync(request, ct);
        return TypedResults.Created("/api/auth/me", new RegisterResponse(
            result.Owner.Id, result.Business.Id,
            "Registration successful. Please verify your email."));
    }
}
