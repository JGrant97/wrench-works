using Microsoft.EntityFrameworkCore;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Auth.VerifyEmail;

public class VerifyEmailService(AppDbContext db) : IVerifyEmailService
{
    public async Task<VerifyEmailResultDto> HandleAsync(VerifyEmailRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Token))
            throw new ValidationException("Email and token are required");

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        // Look up by token — there should only ever be one user with this token
        var user = await db.Users
            .FirstOrDefaultAsync(u => u.EmailVerificationToken == request.Token.Trim(), ct);

        if (user == null)
            throw new ValidationException("Invalid or expired verification code");

        // Validate the email matches the user who owns this token
        if (user.NormalizedEmail != normalizedEmail)
            throw new ValidationException("Email does not match the verification code");

        if (user.EmailVerified)
            return new VerifyEmailResultDto("Email already verified");

        if (user.EmailVerificationTokenExpiresUtc < DateTime.UtcNow)
            throw new ValidationException("Verification code has expired");

        user.EmailVerified = true;
        user.EmailVerificationToken = null;
        user.EmailVerificationTokenExpiresUtc = null;

        // Activate any pending business memberships for this user
        var pendingMemberships = await db.BusinessUsers
            .IgnoreQueryFilters()
            .Where(bu => bu.UserId == user.Id && bu.Status == BusinessUserStatus.Pending)
            .ToListAsync(ct);

        foreach (var membership in pendingMemberships)
        {
            membership.Status = BusinessUserStatus.Active;
        }

        await db.SaveChangesAsync(ct);

        return new VerifyEmailResultDto("Email verified successfully");
    }
}
