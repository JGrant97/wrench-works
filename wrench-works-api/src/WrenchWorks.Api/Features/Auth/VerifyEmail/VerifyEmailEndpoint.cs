using Microsoft.EntityFrameworkCore;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Auth.VerifyEmail;

public record VerifyEmailRequest(string Email, string Token);

public static class VerifyEmailEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/auth/verify-email", HandleAsync)
           .WithTags("Auth")
           .WithOpenApi()
           .AllowAnonymous();

    private static async Task<IResult> HandleAsync(VerifyEmailRequest request, AppDbContext db, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Token))
            return Results.BadRequest(new { code = "validation_error", message = "Email and token are required" });

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        // Look up by token — there should only ever be one user with this token
        var user = await db.Users
            .FirstOrDefaultAsync(u => u.EmailVerificationToken == request.Token.Trim(), ct);

        if (user == null)
            return Results.BadRequest(new { code = "validation_error", message = "Invalid or expired verification code" });

        // Validate the email matches the user who owns this token
        if (user.NormalizedEmail != normalizedEmail)
            return Results.BadRequest(new { code = "validation_error", message = "Email does not match the verification code" });

        if (user.EmailVerified)
            return Results.Ok(new { message = "Email already verified" });

        if (user.EmailVerificationTokenExpiresUtc < DateTime.UtcNow)
            return Results.BadRequest(new { code = "validation_error", message = "Verification code has expired" });

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

        return Results.Ok(new { message = "Email verified successfully" });
    }
}
