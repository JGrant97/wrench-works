using Microsoft.AspNetCore.Http.HttpResults;
using WrenchWorks.Api.Features.Common;
using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Messaging;

public class MessagingEndpointHandler(IMessagingService service) : IMessagingEndpointHandler
{
    private static MessageDto ToDto(OutboundMessage m) =>
        new(m.Id, m.Channel.ToString(), m.To, m.Subject, m.Body,
            m.Status.ToString(), m.CreatedAtUtc, m.CustomerId, m.JobId);

    public async Task<Created<MessageDto>> SendAsync(SendMessageRequest request, CancellationToken ct)
    {
        var message = await service.SendAsync(request, ct);
        return TypedResults.Created($"/api/messaging/{message.Id}", ToDto(message));
    }

    public async Task<Ok<PagedResult<MessageDto>>> ListAsync(
        Guid? customerId, Guid? jobId, int page, int pageSize, CancellationToken ct)
    {
        var result = await service.ListAsync(customerId, jobId, page, pageSize, ct);
        return TypedResults.Ok(new PagedResult<MessageDto>(
            result.Items.Select(ToDto).ToList(), result.Total, result.Page, result.PageSize));
    }

    public async Task<Ok<MessageStatusDto>> RetryAsync(Guid id, CancellationToken ct)
    {
        var message = await service.RetryAsync(id, ct);
        return TypedResults.Ok(new MessageStatusDto(message.Id, message.Status.ToString()));
    }
}
