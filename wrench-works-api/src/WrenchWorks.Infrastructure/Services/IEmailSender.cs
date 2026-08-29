namespace WrenchWorks.Infrastructure.Services;

public interface IEmailSender
{
    Task<SendResult> SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
}

public interface ISmsSender
{
    Task<SendResult> SendAsync(string to, string body, CancellationToken ct = default);
}

public record SendResult(bool Success, string? ProviderMessageId = null, string? Error = null);
