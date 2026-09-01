namespace WrenchWorks.Api.Features.Auth.Login;

/// <summary>
/// Why a login did not succeed. Login is one of the few endpoints with a genuine
/// multi-status contract (401 for bad credentials, 403 for a real account that may not
/// sign in), and the web login route branches on the status -- so the statuses are part
/// of the contract, not an implementation detail.
///
/// The service reports the outcome and the endpoint maps it to a status. That keeps HTTP
/// out of the service without flattening three distinct answers into one.
/// </summary>
public enum LoginFailure
{
    None,
    InvalidCredentials,
    EmailNotVerified,
    NoMembership
}

public record LoginOutcome(LoginResponse? Response, LoginFailure Failure)
{
    public static LoginOutcome Success(LoginResponse response) => new(response, LoginFailure.None);
    public static LoginOutcome Failed(LoginFailure failure) => new(null, failure);
}
