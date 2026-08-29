using Microsoft.Extensions.Logging;

namespace WrenchWorks.Infrastructure.Services;

public class ConsoleEmailSender(ILogger<ConsoleEmailSender> logger) : IEmailSender
{
    public Task<SendResult> SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        logger.LogInformation("EMAIL -> To: {To}, Subject: {Subject}\n{Body}", to, subject, htmlBody);
        return Task.FromResult(new SendResult(true, $"dev-{Guid.NewGuid():N}"));
    }
}

public class ConsoleSmsSender(ILogger<ConsoleSmsSender> logger) : ISmsSender
{
    public Task<SendResult> SendAsync(string to, string body, CancellationToken ct = default)
    {
        logger.LogInformation("SMS -> To: {To}\n{Body}", to, body);
        return Task.FromResult(new SendResult(true, $"dev-{Guid.NewGuid():N}"));
    }
}
