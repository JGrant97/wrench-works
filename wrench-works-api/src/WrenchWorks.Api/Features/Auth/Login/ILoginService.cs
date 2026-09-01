namespace WrenchWorks.Api.Features.Auth.Login;

// The Login slice behind an interface. Unlike the other slices this one cannot simply
// return a DTO: a failed login is not an exception, it is one of three valid answers.
// LoginOutcome carries which, and LoginEndpoint turns it into a status code.
public interface ILoginService
{
    Task<LoginOutcome> HandleAsync(LoginRequest request, CancellationToken ct);
}
