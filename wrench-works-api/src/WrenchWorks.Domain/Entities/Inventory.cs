namespace WrenchWorks.Domain.Entities;

public class InventoryCategory : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public ICollection<InventoryItem> Items { get; set; } = [];
}

public class InventoryItem : BusinessScopedEntity
{
    public string? Sku { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? CategoryId { get; set; }
    public InventoryCategory? Category { get; set; }
    public decimal UnitCost { get; set; }
    public decimal? RetailPrice { get; set; }
    public int StockOnHand { get; set; }
    public int ReorderThreshold { get; set; }
    public string? CompatibilityTagsJson { get; set; }

    public ICollection<StockMovement> StockMovements { get; set; } = [];
    public ICollection<JobPartLine> JobPartLines { get; set; } = [];
}

public class StockMovement : BusinessScopedEntity
{
    public Guid InventoryItemId { get; set; }
    public InventoryItem InventoryItem { get; set; } = null!;
    public int QuantityDelta { get; set; }
    public StockMovementReason Reason { get; set; }
    public string? Notes { get; set; }
    public Guid? JobId { get; set; }
    public Job? Job { get; set; }
    public Guid? CreatedByUserId { get; set; }
}

public enum StockMovementReason
{
    ManualAdjustment,
    JobConsumption,
    JobReturn,
    PurchaseReceived,
    Correction,
    Damaged,
    Other
}
