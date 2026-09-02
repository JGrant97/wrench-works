using FluentValidation;
using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Auth.VerifyEmail;

public class VerifyEmailService(IVerifyEmailRepository repository) : IVerifyEmailService
{
    public async Task<VerifyEmailOutcome> HandleAsync(VerifyEmailRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Token))
            throw new ValidationException("Email and token are required");

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        // Look up by token -- there should only ever be one user with this token
        var user = await repository.FindUserByVerificationTokenAsync(request.Token.Trim(), ct)
            ?? throw new ValidationException("Invalid or expired verification code");

        // Validate the email matches the user who owns this token
        if (user.NormalizedEmail != normalizedEmail)
            throw new ValidationException("Email does not match the verification code");

        if (user.EmailVerified)
            return VerifyEmailOutcome.AlreadyVerified;

        if (user.EmailVerificationTokenExpiresUtc < DateTime.UtcNow)
            throw new ValidationException("Verification code has expired");

        user.EmailVerified = true;
        user.EmailVerificationToken = null;
        user.EmailVerificationTokenExpiresUtc = null;

        // Activating pending memberships here is what makes the invite flow work: an
        // invited user is created Pending and login requires Active. Removing this would
        // strand every invitee. Guarded by UserAccessTests.
        foreach (var membership in await repository.GetPendingMembershipsAsync(user.Id, ct))
            membership.Status = BusinessUserStatus.Active;

        await repository.SaveChangesAsync(ct);

        return VerifyEmailOutcome.Verified;
    }
}
