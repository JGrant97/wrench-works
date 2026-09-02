using Microsoft.EntityFrameworkCore;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Users;

public class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<List<BusinessUser>> ListMembersAsync(CancellationToken ct) =>
        db.BusinessUsers
          .Include(bu => bu.User)
          .Include(bu => bu.Roles).ThenInclude(r => r.Role)
          .OrderBy(bu => bu.User.Name)
          .ToListAsync(ct);

    public Task<BusinessUser?> FindMembershipAsync(Guid userId, Guid businessId, CancellationToken ct) =>
        db.BusinessUsers
          .IgnoreQueryFilters()
          .Include(b => b.User)
          .Include(b => b.Business)
          .Include(b => b.Roles).ThenInclude(r => r.Role)
          .FirstOrDefaultAsync(b => b.UserId == userId && b.BusinessId == businessId, ct);

    public Task<int> CountMembersAsync(CancellationToken ct) => db.BusinessUsers.CountAsync(ct);

    public Task<BusinessSubscription?> GetSubscriptionAsync(Guid businessId, CancellationToken ct) =>
        db.BusinessSubscriptions.FirstOrDefaultAsync(s => s.BusinessId == businessId, ct);

    public Task<Role?> FindRoleAsync(Guid businessId, string roleName, CancellationToken ct) =>
        db.Roles.IgnoreQueryFilters()
          .FirstOrDefaultAsync(r => r.BusinessId == businessId && r.Name == roleName, ct);

    public Task<User?> FindUserByNormalizedEmailAsync(string normalizedEmail, CancellationToken ct) =>
        db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, ct);

    public Task<bool> MembershipExistsAsync(Guid userId, Guid businessId, CancellationToken ct) =>
        db.BusinessUsers.IgnoreQueryFilters()
          .AnyAsync(bu => bu.UserId == userId && bu.BusinessId == businessId, ct);

    public void AddUser(User user) => db.Users.Add(user);
    public void AddMembership(BusinessUser membership) => db.BusinessUsers.Add(membership);
    public void AddRoleAssignment(BusinessUserRole assignment) => db.BusinessUserRoles.Add(assignment);
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
