using Microsoft.EntityFrameworkCore;
using WrenchWorks.Domain.Entities;
using Entities = WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Auth.Register;

public class RegisterRepository(AppDbContext db) : IRegisterRepository
{
    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken ct) =>
        db.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail, ct);

    public Task<Role> GetAdminRoleAsync(Guid businessId, CancellationToken ct) =>
        db.Roles.FirstAsync(r => r.BusinessId == businessId && r.Name == "Admin", ct);

    public void AddBusiness(Entities.Business business) => db.Businesses.Add(business);
    public void AddSubscription(BusinessSubscription subscription) => db.BusinessSubscriptions.Add(subscription);
    public void AddUser(User user) => db.Users.Add(user);
    public void AddMembership(BusinessUser membership) => db.BusinessUsers.Add(membership);
    public void AddRoleAssignment(BusinessUserRole assignment) => db.BusinessUserRoles.Add(assignment);
    public void AddAuditLog(AuditLog log) => db.AuditLogs.Add(log);

    // A new business has no roles until this runs, and a missing permission here means no
    // role will ever have it -- see the PermissionSeeder note in CLAUDE.md.
    public async Task SeedPermissionsAndRolesAsync(Guid businessId, CancellationToken ct)
    {
        await PermissionSeeder.SeedPermissionsAsync(db, ct);
        await PermissionSeeder.SeedDefaultRolesForBusinessAsync(db, businessId, ct);
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
