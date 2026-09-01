using FluentValidation;
using WrenchWorks.Api.Middleware;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;
using WrenchWorks.Infrastructure.Services;

namespace WrenchWorks.Api.Features.Auth.Register;

public class RegisterService(AppDbContext db, IEmailSender emailSender) : IRegisterService
{
    public async Task<RegisterResponse> HandleAsync(RegisterRequest request, CancellationToken ct)
    {
        var validator = new RegisterValidator();
        await validator.ValidateAndThrowAsync(request, ct);

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var emailExists = await db.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail, ct);
        if (emailExists)
            throw new ConflictException("Email already registered");

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

        return new RegisterResponse(user.Id, business.Id, "Registration successful. Please verify your email.");
    }
}
