namespace WrenchWorks.Domain.Entities;

public class Vehicle : BusinessScopedEntity
{
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public string? Make { get; set; }
    public string? Model { get; set; }
    public int? Year { get; set; }
    public string? Vin { get; set; }
    public string? Registration { get; set; }
    public string? EngineType { get; set; }
    public string? FuelType { get; set; }
    public string? Notes { get; set; }

    public ICollection<Job> Jobs { get; set; } = [];
}
