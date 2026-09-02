using Microsoft.EntityFrameworkCore;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Auth.Login;

public class LoginRepository(AppDbContext db) : ILoginRepository
{
    // AsSplitQuery because the role -> permission chain multiplies rows badly as a single
    // join; the whole graph is needed to build the permission claims in the token.
    public Task<User?> FindUserWithActiveMembershipsAsync(string normalizedEmail, CancellationToken ct) =>
        db.Users
          .Include(u => u.BusinessUsers.Where(bu => bu.Status == BusinessUserStatus.Active))
              .ThenInclude(bu => bu.Business)
          .Include(u => u.BusinessUsers.Where(bu => bu.Status == BusinessUserStatus.Active))
              .ThenInclude(bu => bu.Roles)
                  .ThenInclude(r => r.Role)
                      .ThenInclude(r => r.Permissions)
                          .ThenInclude(rp => rp.Permission)
          .AsSplitQuery()
          .IgnoreQueryFilters()
          .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, ct);

    public Task<BusinessSubscription?> GetSubscriptionAsync(Guid businessId, CancellationToken ct) =>
        db.BusinessSubscriptions
          .IgnoreQueryFilters()
          .FirstOrDefaultAsync(s => s.BusinessId == businessId, ct);
}
