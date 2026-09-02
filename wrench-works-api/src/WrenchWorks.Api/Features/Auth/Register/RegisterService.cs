using FluentValidation;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Domain.Entities;
using Entities = WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Services;

namespace WrenchWorks.Api.Features.Auth.Register;

public class RegisterService(IRegisterRepository repository, IEmailSender emailSender) : IRegisterService
{
    public async Task<RegistrationResult> HandleAsync(RegisterRequest request, CancellationToken ct)
    {
        await new RegisterValidator().ValidateAndThrowAsync(request, ct);

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        if (await repository.EmailExistsAsync(normalizedEmail, ct))
            throw new ConflictException("Email already registered");

        var business = new Entities.Business { Name = request.BusinessName.Trim() };
        repository.AddBusiness(business);

        // Trial: all features enabled for 14 days.
        repository.AddSubscription(new BusinessSubscription
        {
            BusinessId = business.Id,
            Plan = "Trial",
            Status = SubscriptionStatus.Trialing,
            UserLimit = 10,
            ZoneLimit = 10,
            InventoryEnabled = true,
            MessagingEnabled = true,
            CurrentPeriodEndUtc = DateTime.UtcNow.AddDays(14)
        });

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
        repository.AddUser(user);

        var membership = new BusinessUser
        {
            UserId = user.Id,
            BusinessId = business.Id,
            Status = BusinessUserStatus.Active
        };
        repository.AddMembership(membership);

        await repository.SaveChangesAsync(ct);

        await repository.SeedPermissionsAndRolesAsync(business.Id, ct);

        var adminRole = await repository.GetAdminRoleAsync(business.Id, ct);
        repository.AddRoleAssignment(new BusinessUserRole
        {
            BusinessUserId = membership.Id,
            RoleId = adminRole.Id
        });
        await repository.SaveChangesAsync(ct);

        await emailSender.SendAsync(
            user.Email,
            "Verify your Wrench Works account",
            $"<p>Hi {user.Name},</p><p>Your verification code: <strong>{verificationToken}</strong></p>",
            ct);

        repository.AddAuditLog(new AuditLog
        {
            BusinessId = business.Id,
            UserId = user.Id,
            Action = "business.created",
            EntityType = "Business",
            EntityId = business.Id
        });
        await repository.SaveChangesAsync(ct);

        return new RegistrationResult(user, business);
    }
}
