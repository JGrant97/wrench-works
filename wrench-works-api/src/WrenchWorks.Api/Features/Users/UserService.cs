using FluentValidation;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Services;

namespace WrenchWorks.Api.Features.Users;

public class UserService(
    IUserRepository repository,
    CurrentUserService currentUser,
    IEmailSender emailSender) : IUserService
{
    public Task<List<BusinessUser>> ListAsync(CancellationToken ct) => repository.ListMembersAsync(ct);

    public async Task<BusinessUser> InviteAsync(InviteUserRequest request, CancellationToken ct)
    {
        await new InviteUserValidator().ValidateAndThrowAsync(request, ct);

        var businessId = currentUser.RequireBusinessId();

        var subscription = await repository.GetSubscriptionAsync(businessId, ct);
        if (subscription != null)
        {
            var memberCount = await repository.CountMembersAsync(ct);
            if (memberCount >= subscription.UserLimit)
                throw new LimitReachedException($"User limit of {subscription.UserLimit} reached for your plan");
        }

        var role = await repository.FindRoleAsync(businessId, request.RoleName, ct)
            ?? throw new NotFoundException($"Role '{request.RoleName}' not found");

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var existingUser = await repository.FindUserByNormalizedEmailAsync(normalizedEmail, ct);

        if (existingUser != null && await repository.MembershipExistsAsync(existingUser.Id, businessId, ct))
            throw new ConflictException("User is already a member of this business");

        var tempPassword = Guid.NewGuid().ToString("N")[..12];
        var user = existingUser ?? new User
        {
            Email = request.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            Name = request.Name.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(tempPassword),
            EmailVerified = false,
            EmailVerificationToken = Guid.NewGuid().ToString("N"),
            EmailVerificationTokenExpiresUtc = DateTime.UtcNow.AddDays(7)
        };
        if (existingUser == null) repository.AddUser(user);

        var membership = new BusinessUser
        {
            UserId = user.Id,
            BusinessId = businessId,
            Status = BusinessUserStatus.Pending,
            // Set so the handler can read the invitee's name and email without re-querying.
            User = user
        };
        repository.AddMembership(membership);
        await repository.SaveChangesAsync(ct);

        repository.AddRoleAssignment(new BusinessUserRole { BusinessUserId = membership.Id, RoleId = role.Id });
        await repository.SaveChangesAsync(ct);

        // The membership is created Pending; VerifyEmailService activates it, which is why
        // the invite carries both the temporary password and the verification token.
        await emailSender.SendAsync(user.Email, "You're invited to Wrench Works",
            $"<p>Hi {user.Name},</p><p>You've been invited to join a workshop on Wrench Works.</p>" +
            (existingUser == null ? $"<p>Your temporary password: <strong>{tempPassword}</strong></p>" : "") +
            $"<p>Verify your email with token: <strong>{user.EmailVerificationToken}</strong></p>",
            ct);

        return membership;
    }

    public async Task<CurrentUserProfile> GetMeAsync(CancellationToken ct)
    {
        var membership = await repository.FindMembershipAsync(
            currentUser.RequireUserId(), currentUser.RequireBusinessId(), ct)
            ?? throw new NotFoundException("Membership not found");

        return new CurrentUserProfile(membership, currentUser.Permissions);
    }
}
