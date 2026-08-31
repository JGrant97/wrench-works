using FluentValidation;
using Microsoft.EntityFrameworkCore;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Features.Common;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;
using WrenchWorks.Infrastructure.Services;

namespace WrenchWorks.Api.Features.Messaging;

public record SendMessageRequest(string Channel, string To, string? Subject, string Body, Guid? CustomerId, Guid? JobId, Guid? BookingId);
public record MessageDto(Guid Id, string Channel, string To, string? Subject, string Body, string Status, DateTime CreatedAtUtc, Guid? CustomerId, Guid? JobId);

public class SendMessageValidator : AbstractValidator<SendMessageRequest>
{
    public SendMessageValidator()
    {
        RuleFor(x => x.Channel).Must(c => c is "Email" or "Sms").WithMessage("Channel must be Email or Sms");
        RuleFor(x => x.To).NotEmpty().MaximumLength(320);
        RuleFor(x => x.Body).NotEmpty();
        RuleFor(x => x.Subject).NotEmpty().When(x => x.Channel == "Email");
    }
}

public static class MessagingEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/messaging").WithTags("Messaging").RequireAuthorization()
            .AddEndpointFilter(async (ctx, next) =>
            {
                var currentUser = ctx.HttpContext.RequestServices.GetRequiredService<CurrentUserService>();
                if (!currentUser.HasFeature("messaging"))
                    return Results.Json(new { code = "feature_disabled", message = "Messaging is not enabled on your plan" }, statusCode: 403);
                return await next(ctx);
            });

        group.MapPost("/send", SendAsync).RequireAuthorization("messaging.send");
        group.MapGet("/", ListAsync).RequireAuthorization("messaging.view");
        group.MapPost("/{id:guid}/retry", RetryAsync).RequireAuthorization("messaging.send");
    }

    private static async Task<IResult> SendAsync(
        SendMessageRequest request,
        AppDbContext db,
        CurrentUserService currentUser,
        IEmailSender emailSender,
        ISmsSender smsSender,
        CancellationToken ct)
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

        return Results.Created($"/api/messaging/{message.Id}",
            new MessageDto(message.Id, message.Channel.ToString(), message.To, message.Subject, message.Body, message.Status.ToString(), message.CreatedAtUtc, message.CustomerId, message.JobId));
    }

    private static async Task<IResult> ListAsync(
        AppDbContext db,
        Guid? customerId = null, Guid? jobId = null,
        int page = 1, int pageSize = 25,
        CancellationToken ct = default)
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

        return Results.Ok(new PagedResult<MessageDto>(items, total, page, pageSize));
    }

    private static async Task<IResult> RetryAsync(
        Guid id,
        AppDbContext db,
        IEmailSender emailSender,
        ISmsSender smsSender,
        CancellationToken ct)
    {
        var message = await db.OutboundMessages.FindAsync([id], ct)
            ?? throw new NotFoundException("Message not found");

        if (message.Status != MessageStatus.Failed)
            return Results.BadRequest(new { code = "validation_error", message = "Only failed messages can be retried" });

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

        return Results.Ok(new { message.Id, Status = message.Status.ToString() });
    }
}
