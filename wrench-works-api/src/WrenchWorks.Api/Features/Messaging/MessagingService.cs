using FluentValidation;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Features.Common;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Services;

namespace WrenchWorks.Api.Features.Messaging;

public class MessagingService(
    IMessagingRepository repository,
    CurrentUserService currentUser,
    IEmailSender emailSender,
    ISmsSender smsSender) : IMessagingService
{
    public async Task<OutboundMessage> SendAsync(SendMessageRequest request, CancellationToken ct)
    {
        await new SendMessageValidator().ValidateAndThrowAsync(request, ct);

        var channel = Enum.Parse<MessageChannel>(request.Channel);

        var message = new OutboundMessage
        {
            BusinessId = currentUser.RequireBusinessId(),
            Channel = channel,
            To = request.To,
            Subject = request.Subject,
            Body = request.Body,
            CustomerId = request.CustomerId,
            JobId = request.JobId,
            BookingId = request.BookingId,
            CreatedByUserId = currentUser.UserId
        };

        ApplyResult(message, await DispatchAsync(message, ct));

        repository.Add(message);
        await repository.SaveChangesAsync(ct);
        return message;
    }

    public Task<PagedResult<OutboundMessage>> ListAsync(
        Guid? customerId, Guid? jobId, int page, int pageSize, CancellationToken ct) =>
        repository.ListAsync(customerId, jobId, page, pageSize, ct);

    public async Task<OutboundMessage> RetryAsync(Guid id, CancellationToken ct)
    {
        var message = await repository.FindAsync(id, ct)
            ?? throw new NotFoundException("Message not found");

        if (message.Status != MessageStatus.Failed)
            throw new ValidationException("Only failed messages can be retried");

        ApplyResult(message, await DispatchAsync(message, ct));
        await repository.SaveChangesAsync(ct);
        return message;
    }

    private Task<SendResult> DispatchAsync(OutboundMessage message, CancellationToken ct) =>
        message.Channel == MessageChannel.Email
            ? emailSender.SendAsync(message.To, message.Subject ?? "", message.Body, ct)
            : smsSender.SendAsync(message.To, message.Body, ct);

    // The provider result is recorded on the message rather than discarded. Register and
    // invite still throw theirs away -- finding 12 in docs/review-findings.md -- and this
    // slice is the one that already did it correctly.
    private static void ApplyResult(OutboundMessage message, SendResult result)
    {
        message.Status = result.Success ? MessageStatus.Sent : MessageStatus.Failed;
        message.ProviderMessageId = result.ProviderMessageId;
        message.ErrorMessage = result.Error;
        message.SentAtUtc = result.Success ? DateTime.UtcNow : null;
    }
}
