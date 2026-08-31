namespace WrenchWorks.Domain.Entities;

/// <summary>
/// Vehicle catalogue — GLOBAL reference data, shared by every business.
///
/// These deliberately extend BaseEntity, not BusinessScopedEntity: one workshop must
/// not be able to change what another workshop sees. There are no tenant-facing write
/// endpoints; the catalogue is populated by the vPIC importer and by seed data.
///
/// Correctness is structural rather than rule-based. A VehicleVariant is one real,
/// buildable configuration, so "an MX-5 cannot be diesel" needs no validation rule —
/// no MX-5 variant row carries FuelType.Diesel, so the option is never offered and
/// cannot be submitted.
/// </summary>
public class VehicleMake : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>NHTSA vPIC Make_ID, when this make came from the importer. Null for hand-seeded makes.</summary>
    public int? VpicMakeId { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<VehicleModel> Models { get; set; } = [];
}

public class VehicleModel : BaseEntity
{
    public Guid MakeId { get; set; }
    public VehicleMake Make { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    /// <summary>NHTSA vPIC Model_ID, when this model came from the importer.</summary>
    public int? VpicModelId { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<VehicleVariant> Variants { get; set; } = [];
}

/// <summary>
/// The leaf of the cascade: one concrete configuration of a model over a year range.
/// A variant that ran 1998–2005 is a single row, not eight.
/// </summary>
public class VehicleVariant : BaseEntity
{
    public Guid ModelId { get; set; }
    public VehicleModel Model { get; set; } = null!;

    public int YearFrom { get; set; }
    public int YearTo { get; set; }

    public string? Trim { get; set; }
    public string? BodyStyle { get; set; }

    public decimal? EngineDisplacementL { get; set; }
    public int? EngineCylinders { get; set; }

    public FuelType FuelType { get; set; }
    public TransmissionType Transmission { get; set; }
    public VehicleDriveType? DriveType { get; set; }

    /// <summary>
    /// Which market this configuration was sold in. The same model differs by market —
    /// the MX-5 1.6 is UK/Europe, the Focus TDCi is European, and trim names diverge
    /// entirely (UK "Zetec" vs US "SE"). Nothing is filtered by market today; it is a
    /// facet the user can narrow on, so a US garage can tell a UK spec apart at a glance.
    /// </summary>
    public VehicleMarket Market { get; set; } = VehicleMarket.Unknown;

    public bool IsActive { get; set; } = true;

    /// <summary>Human label for the picker, e.g. "1.8 · Convertible · Manual · Petrol".</summary>
    public string Describe()
    {
        var parts = new List<string>();

        var trim = Trim?.Trim();
        var displacement = EngineDisplacementL?.ToString("0.0");

        // Some trims are named after the engine ("1.6 TDCi"), so printing the displacement
        // as well would read "1.6 · 1.6 TDCi".
        if (displacement is not null &&
            (string.IsNullOrEmpty(trim) || !trim.StartsWith(displacement, StringComparison.Ordinal)))
        {
            parts.Add(displacement);
        }

        // "Base" is the absence of a named edition — showing it adds nothing.
        if (!string.IsNullOrWhiteSpace(trim) && !trim.Equals("Base", StringComparison.OrdinalIgnoreCase))
            parts.Add(trim);
        if (!string.IsNullOrWhiteSpace(BodyStyle)) parts.Add(BodyStyle!);
        parts.Add(Transmission.ToString());
        parts.Add(FuelType.ToString());

        return string.Join(" · ", parts);
    }
}

/// <summary>
/// Colour is a property of an individual car, not of a model — vPIC has no colour data
/// at all — so it is its own flat reference list rather than part of the cascade.
/// </summary>
public class VehicleColour : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? HexCode { get; set; }
    public bool IsActive { get; set; } = true;
}

public enum FuelType
{
    Petrol,
    Diesel,
    Hybrid,
    PlugInHybrid,
    Electric,
    LPG,
    Other
}

public enum TransmissionType
{
    Manual,
    Automatic,
    CVT,
    DualClutch
}

public enum VehicleDriveType
{
    FWD,
    RWD,
    AWD,
    FourWD
}

/// <summary>
/// Market a variant was sold in. Deliberately small — extend when a third market is
/// actually needed rather than modelling markets that don't exist yet.
/// </summary>
public enum VehicleMarket
{
    /// <summary>
    /// Not yet classified. Exists so that no real market sits at zero.
    ///
    /// When the Market column was added, existing rows got an empty string, which loads
    /// back as the enum's zero value. If a real market were zero, assigning it during an
    /// upsert would look like "no change" to EF and never be written — those rows would
    /// stay blank forever. Keeping zero meaningless makes every assignment a real change.
    /// </summary>
    Unknown = 0,

    /// <summary>United States.</summary>
    US = 1,

    /// <summary>United Kingdom.</summary>
    GB = 2,

    /// <summary>Sold in both, in the same configuration (e.g. Tesla Model 3).</summary>
    Both = 3
}
