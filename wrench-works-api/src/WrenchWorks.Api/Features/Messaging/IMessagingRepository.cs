using WrenchWorks.Api.Features.Common;
using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Messaging;

public interface IMessagingRepository
{
    Task<PagedResult<OutboundMessage>> ListAsync(Guid? customerId, Guid? jobId, int page, int pageSize, CancellationToken ct);
    Task<OutboundMessage?> FindAsync(Guid id, CancellationToken ct);
    void Add(OutboundMessage message);
    Task SaveChangesAsync(CancellationToken ct);
}
