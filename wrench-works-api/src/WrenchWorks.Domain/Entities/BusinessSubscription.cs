namespace WrenchWorks.Domain.Entities;

public class BusinessSubscription : BaseEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = null!;
    public string Plan { get; set; } = "starter";
    public string? StripeCustomerId { get; set; }
    public string? StripeSubscriptionId { get; set; }
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Trialing;
    public DateTime? CurrentPeriodEndUtc { get; set; }
    public int UserLimit { get; set; } = 3;
    public int ZoneLimit { get; set; } = 2;
    public bool InventoryEnabled { get; set; }
    public bool MessagingEnabled { get; set; }
}

public enum SubscriptionStatus
{
    Trialing,
    Active,
    PastDue,
    Canceled,
    Unpaid
}
