using FluentValidation;
using Microsoft.EntityFrameworkCore;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;
using WrenchWorks.Infrastructure.Services;

namespace WrenchWorks.Api.Features.Auth.Register;

public record RegisterRequest(string BusinessName, string OwnerName, string Email, string Password);

public record RegisterResponse(Guid UserId, Guid BusinessId, string Message);

public class RegisterValidator : AbstractValidator<RegisterRequest>
{
    public RegisterValidator()
    {
        RuleFor(x => x.BusinessName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.OwnerName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(128);
    }
}

public static class RegisterEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/auth/register", HandleAsync)
           .WithTags("Auth")
           .WithOpenApi()
           .AllowAnonymous();

    private static async Task<IResult> HandleAsync(
        RegisterRequest request,
        AppDbContext db,
        IEmailSender emailSender,
        CancellationToken ct)
    {
        var validator = new RegisterValidator();
        await validator.ValidateAndThrowAsync(request, ct);

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var emailExists = await db.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail, ct);
        if (emailExists)
            return Results.Conflict(new { code = "conflict", message = "Email already registered" });

        // Create business
        var business = new Domain.Entities.Business { Name = request.BusinessName.Trim() };
        db.Businesses.Add(business);

        // Create subscription (trial — all features enabled for 14 days)
        var subscription = new BusinessSubscription
        {
            BusinessId = business.Id,
            Plan = "Trial",
            Status = SubscriptionStatus.Trialing,
            UserLimit = 10,
            ZoneLimit = 10,
            InventoryEnabled = true,
            MessagingEnabled = true,
            CurrentPeriodEndUtc = DateTime.UtcNow.AddDays(14)
        };
        db.BusinessSubscriptions.Add(subscription);

        // Create user
        var verificationToken = Guid.NewGuid().ToString("N");
        var user = new User
        {
            Email = request.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            Name = request.OwnerName.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            EmailVerificationToken = verificationToken,
            EmailVerificationTokenExpiresUtc = DateTime.UtcNow.AddHours(24)
        };
        db.Users.Add(user);

        // Create business user membership
        var businessUser = new BusinessUser
        {
            UserId = user.Id,
            BusinessId = business.Id,
            Status = BusinessUserStatus.Active
        };
        db.BusinessUsers.Add(businessUser);

        await db.SaveChangesAsync(ct);

        // Seed permissions + default roles
        await PermissionSeeder.SeedPermissionsAsync(db, ct);
        await PermissionSeeder.SeedDefaultRolesForBusinessAsync(db, business.Id, ct);

        // Assign Admin role
        var adminRole = await db.Roles.FirstAsync(r => r.BusinessId == business.Id && r.Name == "Admin", ct);
        db.BusinessUserRoles.Add(new BusinessUserRole { BusinessUserId = businessUser.Id, RoleId = adminRole.Id });
        await db.SaveChangesAsync(ct);

        // Send verification email
        await emailSender.SendAsync(
            user.Email,
            "Verify your Wrench Works account",
            $"<p>Hi {user.Name},</p><p>Your verification code: <strong>{verificationToken}</strong></p>",
            ct);

        // Audit log
        db.AuditLogs.Add(new AuditLog
        {
            BusinessId = business.Id,
            UserId = user.Id,
            Action = "business.created",
            EntityType = "Business",
            EntityId = business.Id
        });
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/auth/me", new RegisterResponse(user.Id, business.Id, "Registration successful. Please verify your email."));
    }
}
