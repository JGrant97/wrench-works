namespace WrenchWorks.Domain.Entities;

public class Zone : BusinessScopedEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
    public int Capacity { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public string? WorkingHoursOverrideJson { get; set; }

    public ICollection<Booking> Bookings { get; set; } = [];
}
