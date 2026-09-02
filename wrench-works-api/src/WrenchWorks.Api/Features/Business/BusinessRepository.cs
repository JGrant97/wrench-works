using WrenchWorks.Infrastructure.Persistence;
using Entities = WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Business;

public class BusinessRepository(AppDbContext db) : IBusinessRepository
{
    public async Task<Entities.Business?> FindAsync(Guid businessId, CancellationToken ct) =>
        await db.Businesses.FindAsync([businessId], ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
