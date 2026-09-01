namespace WrenchWorks.Domain.Entities;

public class Vehicle : BusinessScopedEntity, IArchivable
{
    /// <summary>Null while active; set when archived. See IArchivable.</summary>
    public DateTime? ArchivedAtUtc { get; set; }

    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    // ── Catalogue-backed identity (see docs/vehicle-catalogue.md) ──
    // Nullable for now so the existing rows can be backfilled; a follow-up migration
    // makes VariantId, Year and DisplayName required and drops the free-text columns below.

    public Guid? VariantId { get; set; }
    public VehicleVariant? Variant { get; set; }

    /// <summary>Specific model year, validated against the variant's YearFrom..YearTo range.</summary>
    public int? Year { get; set; }

    public Guid? ColourId { get; set; }
    public VehicleColour? Colour { get; set; }

    /// <summary>
    /// Snapshot of how this vehicle read when it was created, e.g.
    /// "2001 Mazda MX-5 1.8 Convertible". Deliberately denormalised: historical jobs and
    /// invoices keep their original wording if a variant is later corrected, and list
    /// views render a vehicle without joining four catalogue tables.
    /// </summary>
    public string? DisplayName { get; set; }

    public string? Vin { get; set; }
    public string? Registration { get; set; }
    public string? Notes { get; set; }

    // ── Legacy free-text columns — DEPRECATED, dropped once the backfill lands ──
    public string? Make { get; set; }
    public string? Model { get; set; }
    public string? EngineType { get; set; }
    public string? FuelType { get; set; }

    public ICollection<Job> Jobs { get; set; } = [];
}
