namespace WrenchWorks.Domain.Entities;

public class OutboundMessage : BusinessScopedEntity
{
    public MessageChannel Channel { get; set; }
    public string To { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;
    public MessageStatus Status { get; set; } = MessageStatus.Pending;
    public string? ProviderMessageId { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public Guid? JobId { get; set; }
    public Job? Job { get; set; }
    public Guid? BookingId { get; set; }
    public Booking? Booking { get; set; }
    public Guid? CreatedByUserId { get; set; }
}

public enum MessageChannel { Email, Sms }
public enum MessageStatus { Pending, Sent, Failed, Delivered }

public class MessageTemplate : BusinessScopedEntity
{
    public string Name { get; set; } = string.Empty;
    public MessageChannel Channel { get; set; }
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;
}
