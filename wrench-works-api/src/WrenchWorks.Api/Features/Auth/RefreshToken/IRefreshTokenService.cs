using WrenchWorks.Api.Features.Auth.Login;

namespace WrenchWorks.Api.Features.Auth.RefreshToken;

// The RefreshToken slice behind an interface. Returns null when the caller's membership
// is gone or no longer active, which the endpoint turns into a 401 -- the one failure
// mode this endpoint has, so a full outcome type would be more machinery than it earns.
public interface IRefreshTokenService
{
    Task<LoginResponse?> HandleAsync(CancellationToken ct);
}
