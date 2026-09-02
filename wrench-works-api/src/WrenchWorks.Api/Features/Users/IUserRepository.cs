using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Users;

/// <summary>
/// Data access for Users. Several reads deliberately use IgnoreQueryFilters: User is a
/// global entity and a person may belong to more than one business, so invite and /me
/// both need cross-business lookups. Every such read is paired with an explicit
/// BusinessId predicate, which is the convention described in CLAUDE.md.
/// </summary>
public interface IUserRepository
{
    Task<List<BusinessUser>> ListMembersAsync(CancellationToken ct);
    Task<BusinessUser?> FindMembershipAsync(Guid userId, Guid businessId, CancellationToken ct);
    Task<int> CountMembersAsync(CancellationToken ct);
    Task<BusinessSubscription?> GetSubscriptionAsync(Guid businessId, CancellationToken ct);
    Task<Role?> FindRoleAsync(Guid businessId, string roleName, CancellationToken ct);
    Task<User?> FindUserByNormalizedEmailAsync(string normalizedEmail, CancellationToken ct);
    Task<bool> MembershipExistsAsync(Guid userId, Guid businessId, CancellationToken ct);

    void AddUser(User user);
    void AddMembership(BusinessUser membership);
    void AddRoleAssignment(BusinessUserRole assignment);
    Task SaveChangesAsync(CancellationToken ct);
}
