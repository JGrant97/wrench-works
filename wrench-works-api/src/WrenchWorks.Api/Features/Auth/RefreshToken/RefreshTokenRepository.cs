using Microsoft.EntityFrameworkCore;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Auth.RefreshToken;

public class RefreshTokenRepository(AppDbContext db) : IRefreshTokenRepository
{
    public Task<BusinessUser?> FindMembershipWithRolesAsync(Guid userId, Guid businessId, CancellationToken ct) =>
        db.BusinessUsers
          .IgnoreQueryFilters()
          .Include(bu => bu.User)
          .Include(bu => bu.Business)
          .Include(bu => bu.Roles)
              .ThenInclude(r => r.Role)
                  .ThenInclude(r => r.Permissions)
                      .ThenInclude(rp => rp.Permission)
          .FirstOrDefaultAsync(bu => bu.UserId == userId && bu.BusinessId == businessId, ct);

    public Task<BusinessSubscription?> GetSubscriptionAsync(Guid businessId, CancellationToken ct) =>
        db.BusinessSubscriptions
          .IgnoreQueryFilters()
          .FirstOrDefaultAsync(s => s.BusinessId == businessId, ct);
}
