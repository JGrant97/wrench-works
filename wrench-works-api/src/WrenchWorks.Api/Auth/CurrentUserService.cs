using System.Security.Claims;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Auth;

public class CurrentUserService : ITenantProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public Guid? UserId => Guid.TryParse(User?.FindFirstValue("sub"), out var id) ? id : null;
    public Guid? BusinessId => Guid.TryParse(User?.FindFirstValue("business_id"), out var id) ? id : null;
    public Guid? BusinessUserId => Guid.TryParse(User?.FindFirstValue("business_user_id"), out var id) ? id : null;
    public string? Email => User?.FindFirstValue("email");

    public IReadOnlySet<string> Permissions =>
        User?.FindAll("permission").Select(c => c.Value).ToHashSet()
        ?? new HashSet<string>();

    public IReadOnlySet<string> Features =>
        User?.FindAll("feature").Select(c => c.Value).ToHashSet()
        ?? new HashSet<string>();

    public bool HasPermission(string permission) => Permissions.Contains(permission);
    public bool HasFeature(string feature) => Features.Contains(feature);

    public Guid RequireBusinessId() =>
        BusinessId ?? throw new UnauthorizedAccessException("No business context");

    public Guid RequireUserId() =>
        UserId ?? throw new UnauthorizedAccessException("No user context");
}
