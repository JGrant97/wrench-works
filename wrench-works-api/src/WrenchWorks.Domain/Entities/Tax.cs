namespace WrenchWorks.Domain.Entities;

/// <summary>
/// What kind of line is being taxed.
///
/// This exists because in much of the US labour and parts are taxed differently — parts
/// taxable, labour often not. A single rate per job would be wrong there, so the category
/// is what selects the rate. See docs/tax.md.
/// </summary>
public enum TaxCategory
{
    Labour = 0,
    Parts = 1,

    /// <summary>
    /// Shop supplies, cleaner, gloves, disposal levies. Separate from Parts because the US
    /// frequently taxes them differently — often taxable even where labour is not, and in
    /// some states the shop is treated as the end consumer so nothing is charged onward.
    /// </summary>
    Consumables = 2
}

/// <summary>
/// A tax rate the business charges. Configured by them, never shipped by us: a rate table
/// for the US means ~13,000 jurisdictions changing monthly.
///
/// Archivable rather than deletable — a superseded rate is still referenced by every line
/// raised while it applied.
/// </summary>
public class TaxRate : BusinessScopedEntity, IArchivable
{
    /// <summary>Printed on the invoice: "VAT Standard", "NY State + NYC".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Stored as a FRACTION — 0.2 is 20%. Six decimal places, because as a fraction a rate
    /// like 8.875% is 0.08875 — five places. Four was not enough and silently rounded it to
    /// 0.0888, overcharging by 5p per £1000. Caught by TaxTests.
    /// </summary>
    public decimal Rate { get; set; }

    public DateTime? ArchivedAtUtc { get; set; }

    public ICollection<TaxRateComponent> Components { get; set; } = [];

    /// <summary>Which line categories this rate is applied to. See TaxRateCategory.</summary>
    public ICollection<TaxRateCategory> Categories { get; set; } = [];
}

/// <summary>
/// Which rate applies to which category of line.
///
/// This replaced a pair of booleans on TaxRate (IsDefaultForLabour / IsDefaultForParts).
/// That shape cost a schema migration and a code change for every new category, and
/// consumables was the case that proved it — the next ones are tyre levies and hazmat.
///
/// Category is the natural key alongside BusinessId, and the unique index enforces it:
/// a category maps to exactly ONE rate, structurally, rather than by a validation rule
/// that could be forgotten. A category with no row is untaxed, which is precisely how a US
/// shop says "labour is not taxable here".
/// </summary>
public class TaxRateCategory : BusinessScopedEntity
{
    public TaxCategory Category { get; set; }

    public Guid TaxRateId { get; set; }
    public TaxRate TaxRate { get; set; } = null!;
}

/// <summary>
/// An optional jurisdiction breakdown, so a US invoice can show
/// "NY State 4% · NYC 4.5% · MCTD 0.375%" instead of a bare 8.875%.
///
/// Display and reporting only. A line's tax is computed from the parent rate and NEVER by
/// summing components: rounding each component separately drifts from the total the
/// customer was actually charged.
/// </summary>
public class TaxRateComponent : BaseEntity
{
    public Guid TaxRateId { get; set; }
    public TaxRate TaxRate { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public decimal Rate { get; set; }

    /// <summary>Invoices list jurisdictions in a fixed order, widest first.</summary>
    public int SortOrder { get; set; }
}
