namespace WrenchWorks.Domain.Entities;

public class Customer : BusinessScopedEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? PreferredContactMethod { get; set; }
    public string? Notes { get; set; }
    public string? Tags { get; set; }

    public ICollection<Vehicle> Vehicles { get; set; } = [];
    public ICollection<Job> Jobs { get; set; } = [];
    public ICollection<Booking> Bookings { get; set; } = [];
}
