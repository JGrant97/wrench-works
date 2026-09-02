using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Auth.Login;

/// <summary>
/// A signed-in session in domain terms. The handler turns this into LoginResponse, so the
/// wire shape stays out of the service -- and RefreshToken produces the same type, which
/// is what keeps login and refresh returning an identical payload.
/// </summary>
public record LoginSession(
    string Token,
    User User,
    BusinessUser Membership,
    List<string> Permissions,
    List<string> Features);

/// <summary>
/// Why a login did not succeed. Login is one of the few endpoints with a genuine
/// multi-status contract (401 for bad credentials, 403 for a real account that may not
/// sign in), and the web login route branches on the status -- so the statuses are part
/// of the contract, not an implementation detail.
/// </summary>
public enum LoginFailure
{
    None,
    InvalidCredentials,
    EmailNotVerified,
    NoMembership
}

public record LoginOutcome(LoginSession? Session, LoginFailure Failure)
{
    public static LoginOutcome Success(LoginSession session) => new(session, LoginFailure.None);
    public static LoginOutcome Failed(LoginFailure failure) => new(null, failure);
}

public interface ILoginService
{
    Task<LoginOutcome> HandleAsync(LoginRequest request, CancellationToken ct);
}
