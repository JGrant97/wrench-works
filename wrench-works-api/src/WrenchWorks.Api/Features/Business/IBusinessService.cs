using Entities = WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Business;

public interface IBusinessService
{
    Task<Entities.Business> GetAsync(CancellationToken ct);
    Task<Entities.Business> UpdateAsync(UpdateBusinessRequest request, CancellationToken ct);
}
