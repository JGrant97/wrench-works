using FluentValidation;
using Microsoft.EntityFrameworkCore;
using WrenchWorks.Api.Auth;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Auth.Login;

public record LoginRequest(string Email, string Password);
public record LoginResponse(string Token, UserDto User);
// Currency rides along with the session because every screen formats money and the
// alternative is a business lookup on each one. It lands in the readable ww_user cookie,
// which is what lets both client components and server components format consistently.
public record UserDto(Guid Id, string Name, string Email, Guid BusinessId, string BusinessName, string Currency, IEnumerable<string> Permissions, IEnumerable<string> Features);

public class LoginValidator : AbstractValidator<LoginRequest>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public static class LoginEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/auth/login", HandleAsync)
           .WithTags("Auth")
           .WithOpenApi()
           .AllowAnonymous();

    private static async Task<IResult> HandleAsync(
        LoginRequest request,
        AppDbContext db,
        IJwtTokenService jwtService,
        CancellationToken ct)
    {
        var validator = new LoginValidator();
        await validator.ValidateAndThrowAsync(request, ct);

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await db.Users
            .Include(u => u.BusinessUsers.Where(bu => bu.Status == Domain.Entities.BusinessUserStatus.Active))
                .ThenInclude(bu => bu.Business)
            .Include(u => u.BusinessUsers.Where(bu => bu.Status == Domain.Entities.BusinessUserStatus.Active))
                .ThenInclude(bu => bu.Roles)
                    .ThenInclude(r => r.Role)
                        .ThenInclude(r => r.Permissions)
                            .ThenInclude(rp => rp.Permission)
            .AsSplitQuery()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, ct);

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Results.Unauthorized();

        if (!user.EmailVerified)
            return Results.Problem("Email not verified", statusCode: 403);

        var businessUser = user.BusinessUsers.FirstOrDefault();
        if (businessUser == null)
            return Results.Problem("No active business membership", statusCode: 403);

        var permissions = businessUser.Roles
            .SelectMany(r => r.Role.Permissions)
            .Select(rp => rp.Permission.Key)
            .Distinct()
            .ToList();

        // Load subscription feature flags
        var subscription = await db.BusinessSubscriptions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.BusinessId == businessUser.BusinessId, ct);

        var features = new List<string>();
        if (subscription != null)
        {
            if (subscription.InventoryEnabled) features.Add("inventory");
            if (subscription.MessagingEnabled) features.Add("messaging");
        }

        var token = jwtService.GenerateToken(user.Id, user.Email, businessUser.BusinessId, businessUser.Id, permissions, features);

        return Results.Ok(new LoginResponse(token, new UserDto(
            user.Id,
            user.Name,
            user.Email,
            businessUser.BusinessId,
            businessUser.Business.Name,
            businessUser.Business.Currency,
            permissions,
            features)));
    }
}
