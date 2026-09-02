using Microsoft.AspNetCore.Http.HttpResults;
using WrenchWorks.Api.Features.Common;

namespace WrenchWorks.Api.Features.Messaging;

public interface IMessagingEndpointHandler
{
    Task<Created<MessageDto>> SendAsync(SendMessageRequest request, CancellationToken ct);
    Task<Ok<PagedResult<MessageDto>>> ListAsync(Guid? customerId, Guid? jobId, int page, int pageSize, CancellationToken ct);
    Task<Ok<MessageStatusDto>> RetryAsync(Guid id, CancellationToken ct);
}
