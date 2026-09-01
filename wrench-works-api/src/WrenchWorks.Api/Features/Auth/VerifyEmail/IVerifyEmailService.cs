namespace WrenchWorks.Api.Features.Auth.VerifyEmail;

// The VerifyEmail slice behind an interface: the endpoints become a thin HTTP shell.
// Methods return DTOs, not IResult -- failures are thrown and mapped by
// ErrorHandlingMiddleware, so nothing here needs to know about status codes.
public interface IVerifyEmailService
{
    Task<VerifyEmailResultDto> HandleAsync(VerifyEmailRequest request, CancellationToken ct);
}
