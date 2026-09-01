namespace WrenchWorks.Domain.Entities;

public class Customer : BusinessScopedEntity, IArchivable
{
    /// <summary>Null while active; set when archived. See IArchivable.</summary>
    public DateTime? ArchivedAtUtc { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? PreferredContactMethod { get; set; }
    public string? Notes { get; set; }
    public string? Tags { get; set; }

    /// <summary>
    /// Covers US resale/government/non-profit certificates and EU B2B reverse charge.
    /// When set, every line on this customer's jobs is raised at a zero rate.
    /// </summary>
    public bool IsTaxExempt { get; set; }

    /// <summary>The certificate or VAT number justifying the exemption. Shown on the invoice.</summary>
    public string? TaxExemptionReference { get; set; }

    public ICollection<Vehicle> Vehicles { get; set; } = [];
    public ICollection<Job> Jobs { get; set; } = [];
    public ICollection<Booking> Bookings { get; set; } = [];
}
