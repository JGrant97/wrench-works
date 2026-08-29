using Microsoft.EntityFrameworkCore;
using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Infrastructure.Persistence;

public static class PermissionSeeder
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

    public static readonly Dictionary<string, string[]> DefaultRoles = new()
    {
        ["Admin"] = AllPermissions,
        ["Advisor"] = [
            "calendar.view", "calendar.edit",
            "jobs.create", "jobs.edit", "jobs.view", "jobs.assign",
            "customers.view", "customers.manage",
            "vehicles.view", "vehicles.manage",
            "messaging.send", "messaging.view"
        ],
        ["Technician"] = [
            "calendar.view",
            "jobs.view", "jobs.edit",
            "inventory.view",
            "vehicles.view",
            "customers.view"
        ],
        ["Inventory"] = [
            "inventory.view", "inventory.manage",
            "jobs.view"
        ],
        ["ReadOnly"] = [
            "calendar.view", "jobs.view", "customers.view", "vehicles.view", "inventory.view"
        ]
    };

    public static async Task SeedPermissionsAsync(AppDbContext db, CancellationToken ct = default)
    {
        var existing = await db.Permissions.Select(p => p.Key).ToListAsync(ct);
        var toAdd = AllPermissions.Where(k => !existing.Contains(k)).ToList();
        if (toAdd.Count > 0)
        {
            db.Permissions.AddRange(toAdd.Select(k => new Permission { Key = k, Description = k }));
            await db.SaveChangesAsync(ct);
        }
    }

    public static async Task SeedDefaultRolesForBusinessAsync(AppDbContext db, Guid businessId, CancellationToken ct = default)
    {
        var permissions = await db.Permissions.ToListAsync(ct);
        var permLookup = permissions.ToDictionary(p => p.Key, p => p.Id);

        foreach (var (roleName, permKeys) in DefaultRoles)
        {
            var role = new Role
            {
                BusinessId = businessId,
                Name = roleName,
                IsSystem = true
            };
            db.Roles.Add(role);
            await db.SaveChangesAsync(ct);

            foreach (var key in permKeys)
            {
                if (permLookup.TryGetValue(key, out var permId))
                {
                    db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permId });
                }
            }
            await db.SaveChangesAsync(ct);
        }
    }
}
