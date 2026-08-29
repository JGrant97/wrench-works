namespace WrenchWorks.Domain.Entities;

public class Business : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string Timezone { get; set; } = "UTC";
    public string Currency { get; set; } = "GBP";
    public string? LogoUrl { get; set; }
    public string? WorkingHoursJson { get; set; }

    public ICollection<BusinessUser> BusinessUsers { get; set; } = [];
    public ICollection<Zone> Zones { get; set; } = [];
    public ICollection<Customer> Customers { get; set; } = [];
    public ICollection<InventoryItem> InventoryItems { get; set; } = [];
    public ICollection<Role> Roles { get; set; } = [];
    public ICollection<MessageTemplate> MessageTemplates { get; set; } = [];
    public BusinessSubscription? Subscription { get; set; }
}
