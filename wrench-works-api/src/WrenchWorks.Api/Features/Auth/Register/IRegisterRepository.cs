using WrenchWorks.Domain.Entities;
using Entities = WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Auth.Register;

// Anonymous endpoint, so no tenant context exists yet and every read here is unfiltered
// by necessity. Registration is ~14 sequential saves with no transaction -- finding 8 in
// docs/review-findings.md -- and concentrating them here is what would make wrapping the
// whole thing in one transaction a single change rather than a scatter.
public interface IRegisterRepository
{
    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken ct);
    Task<Role> GetAdminRoleAsync(Guid businessId, CancellationToken ct);

    void AddBusiness(Entities.Business business);
    void AddSubscription(BusinessSubscription subscription);
    void AddUser(User user);
    void AddMembership(BusinessUser membership);
    void AddRoleAssignment(BusinessUserRole assignment);
    void AddAuditLog(AuditLog log);

    Task SeedPermissionsAndRolesAsync(Guid businessId, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
