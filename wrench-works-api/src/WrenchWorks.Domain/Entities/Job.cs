namespace WrenchWorks.Domain.Entities;

public class Job : BusinessScopedEntity, IArchivable
{
    /// <summary>Null while active; set when archived. See IArchivable.</summary>
    public DateTime? ArchivedAtUtc { get; set; }

    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public Guid VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;
    public Guid? BookingId { get; set; }
    public Booking? Booking { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? InternalNotes { get; set; }
    public string? CustomerNotes { get; set; }
    public JobStatus Status { get; set; } = JobStatus.Draft;
    public JobPriority Priority { get; set; } = JobPriority.Normal;
    public Guid? AssignedZoneId { get; set; }
    public Zone? AssignedZone { get; set; }
    public DateTime? ScheduledStartUtc { get; set; }
    public DateTime? ScheduledEndUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }

    public ICollection<JobLaborLine> LaborLines { get; set; } = [];
    public ICollection<JobPartLine> PartLines { get; set; } = [];
    public ICollection<JobAssignment> Assignments { get; set; } = [];
    public ICollection<OutboundMessage> Messages { get; set; } = [];
}

public enum JobStatus
{
    Draft,
    Scheduled,
    InProgress,
    WaitingParts,
    Completed,
    Invoiced,
    Closed
}

public enum JobPriority { Low, Normal, High, Urgent }

public class JobLaborLine : BaseEntity
{
    public Guid JobId { get; set; }
    public Job Job { get; set; } = null!;
    public string Description { get; set; } = string.Empty;
    public decimal Hours { get; set; }
    public decimal Rate { get; set; }

    // Tax as applied, snapshotted. Never recomputed from current settings: when UK VAT
    // moved 17.5% -> 20% -> 15% -> 20%, systems that recomputed silently rewrote their own
    // history. See docs/tax.md.
    public Guid? TaxRateId { get; set; }
    public decimal TaxRatePercent { get; set; }
    public decimal TaxAmount { get; set; }
}

public class JobPartLine : BaseEntity
{
    public Guid JobId { get; set; }
    public Job Job { get; set; } = null!;
    public Guid InventoryItemId { get; set; }
    public InventoryItem InventoryItem { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    // Tax as applied, snapshotted. Never recomputed from current settings: when UK VAT
    // moved 17.5% -> 20% -> 15% -> 20%, systems that recomputed silently rewrote their own
    // history. See docs/tax.md.
    public Guid? TaxRateId { get; set; }
    public decimal TaxRatePercent { get; set; }
    public decimal TaxAmount { get; set; }
}

public class JobAssignment
{
    public Guid JobId { get; set; }
    public Job Job { get; set; } = null!;
    public Guid BusinessUserId { get; set; }
    public BusinessUser BusinessUser { get; set; } = null!;
}
