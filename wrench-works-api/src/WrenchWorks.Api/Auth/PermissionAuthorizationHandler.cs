using Microsoft.AspNetCore.Authorization;

namespace WrenchWorks.Api.Auth;

public class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

public class PermissionAuthorizationHandler(IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var permissions = context.User.FindAll("permission").Select(c => c.Value).ToHashSet();
        if (permissions.Contains(requirement.Permission))
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}

public static class PermissionPolicies
{
    public static readonly string[] AllPermissions =
    [
        "calendar.view", "calendar.edit",
        "jobs.create", "jobs.edit", "jobs.delete", "jobs.assign", "jobs.view",
        "inventory.view", "inventory.manage",
        "customers.view", "customers.manage",
        "vehicles.view", "vehicles.manage",
        "billing.manage",
        "users.manage", "roles.manage", "settings.manage",
        "messaging.send", "messaging.view"
    ];

    public static void AddPermissionPolicies(this AuthorizationOptions options)
    {
        foreach (var perm in AllPermissions)
        {
            options.AddPolicy(perm, policy => policy.Requirements.Add(new PermissionRequirement(perm)));
        }
    }
}
