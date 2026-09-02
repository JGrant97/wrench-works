using WrenchWorks.Domain.Entities;
using Entities = WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Auth.Register;

// What registration created. The handler turns this into RegisterResponse.
public record RegistrationResult(User Owner, Entities.Business Business);

public interface IRegisterService
{
    Task<RegistrationResult> HandleAsync(RegisterRequest request, CancellationToken ct);
}
