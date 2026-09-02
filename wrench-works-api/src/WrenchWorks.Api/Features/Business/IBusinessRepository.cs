using Entities = WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Business;

// Aliased: this namespace is Features.Business and the entity is also called Business,
// so the unqualified name is ambiguous here.
public interface IBusinessRepository
{
    Task<Entities.Business?> FindAsync(Guid businessId, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
