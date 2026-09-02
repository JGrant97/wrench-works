using WrenchWorks.Api.Features.Common;
using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Messaging;

public interface IMessagingService
{
    Task<OutboundMessage> SendAsync(SendMessageRequest request, CancellationToken ct);
    Task<PagedResult<OutboundMessage>> ListAsync(Guid? customerId, Guid? jobId, int page, int pageSize, CancellationToken ct);
    Task<OutboundMessage> RetryAsync(Guid id, CancellationToken ct);
}
