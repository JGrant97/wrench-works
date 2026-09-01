using WrenchWorks.Api.Features.Common;

namespace WrenchWorks.Api.Features.Messaging;

// The Messaging slice behind an interface: the endpoints become a thin HTTP shell.
// Methods return DTOs, not IResult -- failures are thrown and mapped by
// ErrorHandlingMiddleware, so nothing here needs to know about status codes.
public interface IMessagingService
{
    Task<MessageDto> SendAsync(SendMessageRequest request, CancellationToken ct);
    Task<PagedResult<MessageDto>> ListAsync(Guid? customerId = null, Guid? jobId = null, int page = 1, int pageSize = 25, CancellationToken ct = default);
    Task<MessageStatusDto> RetryAsync(Guid id, CancellationToken ct);
}
