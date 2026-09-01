using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;
using WrenchWorks.Infrastructure.Services;

namespace WrenchWorks.Api.Features.Users;

public class UserService(AppDbContext db, CurrentUserService currentUser, IEmailSender emailSender) : IUserService
{
    public async Task<List<UserListItemDto>> ListAsync(CancellationToken ct)
    {
        var users = await db.BusinessUsers
            .Include(bu => bu.User)
            .Include(bu => bu.Roles).ThenInclude(r => r.Role)
            .OrderBy(bu => bu.User.Name)
            .Select(bu => new UserListItemDto(
                bu.UserId, bu.Id, bu.User.Name, bu.User.Email, bu.Status.ToString(),
                bu.Roles.Select(r => r.Role.Name), bu.CreatedAtUtc))
            .ToListAsync(ct);

        return users;
    }

    public async Task<InvitedUserDto> InviteAsync(InviteUserRequest request, CancellationToken ct)
    {
        await new InviteUserValidator().ValidateAndThrowAsync(request, ct);

        var businessId = currentUser.RequireBusinessId();

        // Check user limit
        var sub = await db.BusinessSubscriptions.FirstOrDefaultAsync(s => s.BusinessId == businessId, ct);
        if (sub != null)
        {
            var currentCount = await db.BusinessUsers.CountAsync(ct);
            if (currentCount >= sub.UserLimit)
                throw new LimitReachedException($"User limit of {sub.UserLimit} reached for your plan");
        }

        // Check role exists
        var role = await db.Roles.IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.BusinessId == businessId && r.Name == request.RoleName, ct)
            ?? throw new NotFoundException($"Role '{request.RoleName}' not found");

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var existingUser = await db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, ct);

        if (existingUser != null)
        {
            // Check if already a member of this business
            var existingMembership = await db.BusinessUsers
                .IgnoreQueryFilters()
                .AnyAsync(bu => bu.UserId == existingUser.Id && bu.BusinessId == businessId, ct);
            if (existingMembership)
                throw new ConflictException("User is already a member of this business");
        }

        // Create user if doesn't exist
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
        if (existingUser == null) db.Users.Add(user);

        // Create business user membership
        var businessUser = new BusinessUser
        {
            UserId = user.Id,
            BusinessId = businessId,
            Status = BusinessUserStatus.Pending
        };
        db.BusinessUsers.Add(businessUser);
        await db.SaveChangesAsync(ct);

        // Assign role
        db.BusinessUserRoles.Add(new BusinessUserRole { BusinessUserId = businessUser.Id, RoleId = role.Id });
        await db.SaveChangesAsync(ct);

        // Send invite email
        await emailSender.SendAsync(user.Email, "You're invited to Wrench Works",
            $"<p>Hi {user.Name},</p><p>You've been invited to join a workshop on Wrench Works.</p>" +
            (existingUser == null ? $"<p>Your temporary password: <strong>{tempPassword}</strong></p>" : "") +
            $"<p>Verify your email with token: <strong>{user.EmailVerificationToken}</strong></p>",
            ct);

        return new InvitedUserDto(businessUser.Id, user.Name, user.Email, "Pending");
    }

    public async Task<CurrentUserDto> GetMeAsync(CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();
        var businessId = currentUser.RequireBusinessId();

        var bu = await db.BusinessUsers
            .IgnoreQueryFilters()
            .Include(b => b.User)
            .Include(b => b.Business)
            .Include(b => b.Roles).ThenInclude(r => r.Role)
            .FirstOrDefaultAsync(b => b.UserId == userId && b.BusinessId == businessId, ct);

        if (bu == null) throw new NotFoundException("Membership not found");

        return new CurrentUserDto(
            bu.UserId, bu.User.Name, bu.User.Email, bu.BusinessId, bu.Business.Name,
            bu.Roles.Select(r => r.Role.Name), currentUser.Permissions);
    }
}
