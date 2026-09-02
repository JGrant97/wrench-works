namespace WrenchWorks.Api.Features.Auth.VerifyEmail;

// Verification succeeds two ways and the difference is worth telling the user about, but
// the wording is presentation -- the handler turns this into the message.
public enum VerifyEmailOutcome
{
    Verified,
    AlreadyVerified
}

public interface IVerifyEmailService
{
    Task<VerifyEmailOutcome> HandleAsync(VerifyEmailRequest request, CancellationToken ct);
}
