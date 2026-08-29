namespace WrenchWorks.Domain.Entities;

public class Booking : BusinessScopedEntity
{
    public Guid ZoneId { get; set; }
    public Zone Zone { get; set; } = null!;
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public Guid VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public string? Notes { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Confirmed;
    public Guid? JobId { get; set; }
    public Job? Job { get; set; }
    public Guid? CreatedByUserId { get; set; }
}

public enum BookingStatus
{
    Confirmed,
    Cancelled,
    Completed,
    NoShow
}
