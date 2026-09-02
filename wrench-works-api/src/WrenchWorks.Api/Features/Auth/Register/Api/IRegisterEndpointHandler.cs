using Microsoft.AspNetCore.Http.HttpResults;

namespace WrenchWorks.Api.Features.Auth.Register;

public interface IRegisterEndpointHandler
{
    Task<Created<RegisterResponse>> HandleAsync(RegisterRequest request, CancellationToken ct);
}
