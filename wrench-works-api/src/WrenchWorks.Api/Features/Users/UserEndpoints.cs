using FluentValidation;
using Microsoft.EntityFrameworkCore;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;
using WrenchWorks.Infrastructure.Services;

namespace WrenchWorks.Api.Features.Users;

public record InviteUserRequest(string Name, string Email, string RoleName);
public record UserListItemDto(Guid Id, Guid BusinessUserId, string Name, string Email, string Status, IEnumerable<string> Roles, DateTime CreatedAtUtc);

public class InviteUserValidator : AbstractValidator<InviteUserRequest>
{
    public InviteUserValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.RoleName).NotEmpty();
    }
}

public static class UserEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users").WithTags("Users").RequireAuthorization("users.manage");

        group.MapGet("/", ListAsync);
        group.MapPost("/invite", InviteAsync);
        group.MapGet("/me", GetMeAsync).RequireAuthorization();
    }

    private static async Task<IResult> ListAsync(AppDbContext db, CancellationToken ct)
    {
        var users = await db.BusinessUsers
            .Include(bu => bu.User)
            .Include(bu => bu.Roles).ThenInclude(r => r.Role)
            .OrderBy(bu => bu.User.Name)
            .Select(bu => new UserListItemDto(
                bu.UserId, bu.Id, bu.User.Name, bu.User.Email, bu.Status.ToString(),
                bu.Roles.Select(r => r.Role.Name), bu.CreatedAtUtc))
            .ToListAsync(ct);

        return Results.Ok(users);
    }

    private static async Task<IResult> InviteAsync(
        InviteUserRequest request,
        AppDbContext db,
        CurrentUserService currentUser,
        IEmailSender emailSender,
        CancellationToken ct)
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

        return Results.Created($"/api/users/{businessUser.Id}", new { businessUser.Id, user.Name, user.Email, Status = "Pending" });
    }

    private static async Task<IResult> GetMeAsync(
        AppDbContext db,
        CurrentUserService currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();
        var businessId = currentUser.RequireBusinessId();

        var bu = await db.BusinessUsers
            .IgnoreQueryFilters()
            .Include(b => b.User)
            .Include(b => b.Business)
            .Include(b => b.Roles).ThenInclude(r => r.Role)
            .FirstOrDefaultAsync(b => b.UserId == userId && b.BusinessId == businessId, ct);

        if (bu == null) return Results.NotFound();

        return Results.Ok(new
        {
            bu.UserId,
            bu.User.Name,
            bu.User.Email,
            bu.BusinessId,
            BusinessName = bu.Business.Name,
            Roles = bu.Roles.Select(r => r.Role.Name),
            Permissions = currentUser.Permissions
        });
    }
}
