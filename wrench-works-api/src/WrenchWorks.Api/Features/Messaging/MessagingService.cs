using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Features.Common;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;
using WrenchWorks.Infrastructure.Services;

namespace WrenchWorks.Api.Features.Messaging;

public class MessagingService(AppDbContext db, CurrentUserService currentUser, IEmailSender emailSender, ISmsSender smsSender) : IMessagingService
{
    public async Task<MessageDto> SendAsync(SendMessageRequest request, CancellationToken ct)
    {
        await new SendMessageValidator().ValidateAndThrowAsync(request, ct);

        var businessId = currentUser.RequireBusinessId();
        var channel = Enum.Parse<MessageChannel>(request.Channel);

        var message = new OutboundMessage
        {
            BusinessId = businessId,
            Channel = channel,
            To = request.To,
            Subject = request.Subject,
            Body = request.Body,
            CustomerId = request.CustomerId,
            JobId = request.JobId,
            BookingId = request.BookingId,
            CreatedByUserId = currentUser.UserId
        };

        SendResult result;
        if (channel == MessageChannel.Email)
            result = await emailSender.SendAsync(request.To, request.Subject ?? "", request.Body, ct);
        else
            result = await smsSender.SendAsync(request.To, request.Body, ct);

        message.Status = result.Success ? MessageStatus.Sent : MessageStatus.Failed;
        message.ProviderMessageId = result.ProviderMessageId;
        message.ErrorMessage = result.Error;
        message.SentAtUtc = result.Success ? DateTime.UtcNow : null;

        db.OutboundMessages.Add(message);
        await db.SaveChangesAsync(ct);

        return new MessageDto(message.Id, message.Channel.ToString(), message.To, message.Subject, message.Body, message.Status.ToString(), message.CreatedAtUtc, message.CustomerId, message.JobId);
    }

    public async Task<PagedResult<MessageDto>> ListAsync(Guid? customerId = null, Guid? jobId = null, int page = 1, int pageSize = 25, CancellationToken ct = default)
    {
        var query = db.OutboundMessages.AsQueryable();
        if (customerId.HasValue) query = query.Where(m => m.CustomerId == customerId);
        if (jobId.HasValue) query = query.Where(m => m.JobId == jobId);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(m => m.CreatedAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(m => new MessageDto(m.Id, m.Channel.ToString(), m.To, m.Subject, m.Body, m.Status.ToString(), m.CreatedAtUtc, m.CustomerId, m.JobId))
            .ToListAsync(ct);

        return new PagedResult<MessageDto>(items, total, page, pageSize);
    }

    public async Task<MessageStatusDto> RetryAsync(Guid id, CancellationToken ct)
    {
        var message = await db.OutboundMessages.FindAsync([id], ct)
            ?? throw new NotFoundException("Message not found");

        if (message.Status != MessageStatus.Failed)
            throw new ValidationException("Only failed messages can be retried");

        SendResult result;
        if (message.Channel == MessageChannel.Email)
            result = await emailSender.SendAsync(message.To, message.Subject ?? "", message.Body, ct);
        else
            result = await smsSender.SendAsync(message.To, message.Body, ct);

        message.Status = result.Success ? MessageStatus.Sent : MessageStatus.Failed;
        message.ProviderMessageId = result.ProviderMessageId;
        message.ErrorMessage = result.Error;
        message.SentAtUtc = result.Success ? DateTime.UtcNow : null;
        await db.SaveChangesAsync(ct);

        return new MessageStatusDto(message.Id, message.Status.ToString());
    }
}
