using WrenchWorks.Api.Features.Auth.Login;

namespace WrenchWorks.Api.Features.Auth.RefreshToken;

// Returns null when the membership is gone or no longer active, which the handler turns
// into a 401 -- the one failure mode this endpoint has, so a full outcome type would be
// more machinery than it earns. Produces the same LoginSession as login.
public interface IRefreshTokenService
{
    Task<LoginSession?> HandleAsync(CancellationToken ct);
}
