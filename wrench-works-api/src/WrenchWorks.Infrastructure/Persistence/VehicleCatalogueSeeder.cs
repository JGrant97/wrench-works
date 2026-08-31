using Microsoft.EntityFrameworkCore;
using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Infrastructure.Persistence;

/// <summary>
/// Hand-curated catalogue seed.
///
/// vPIC supplies the make → model spine but no trim, engine, transmission or fuel data
/// (verified — see docs/vehicle-catalogue.md), and nothing at all before 1981. So every
/// variant is curated here, reviewed like code. This is the layer that makes
/// "an MX-5 cannot be diesel" true: there is simply no such row.
///
/// Idempotent — safe to run on every startup.
/// </summary>
public static class VehicleCatalogueSeeder
{
    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        await SeedColoursAsync(db, ct);
        await ApplyTrimCorrectionsAsync(db, ct);
        await SeedVariantsAsync(db, ct);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Repairs variants seeded before trim and engine size were separated.
    ///
    /// The first cut of this seed put the displacement in Trim ("1.6", "1.8", "2.0"),
    /// which duplicated EngineDisplacementL and left no room for the real spec levels.
    /// Renaming in place — rather than deleting and re-inserting — keeps the foreign keys
    /// of any vehicle already pointing at those rows intact.
    ///
    /// Idempotent: once renamed, nothing matches the pattern again.
    /// </summary>
    private static async Task ApplyTrimCorrectionsAsync(AppDbContext db, CancellationToken ct)
    {
        var suspect = await db.VehicleVariants
            .Include(v => v.Model)
            .Where(v => v.Trim != null && v.EngineDisplacementL != null)
            .ToListAsync(ct);

        foreach (var variant in suspect)
        {
            // A trim that is just the engine size, e.g. Trim "1.8" with 1.8L.
            var displacement = variant.EngineDisplacementL!.Value.ToString("0.0");
            if (variant.Trim == displacement)
                variant.Trim = "Base";
        }

        await db.SaveChangesAsync(ct);
    }

    private static readonly (string Name, string Hex)[] Colours =
    [
        ("Black", "#000000"), ("White", "#FFFFFF"), ("Silver", "#C0C0C0"),
        ("Grey", "#808080"), ("Blue", "#1E40AF"), ("Red", "#B91C1C"),
        ("Green", "#15803D"), ("Yellow", "#EAB308"), ("Orange", "#EA580C"),
        ("Brown", "#78350F"), ("Beige", "#D6C7A1"), ("Gold", "#B8860B"),
        ("Purple", "#6B21A8"), ("Maroon", "#7F1D1D"), ("Other", null!)
    ];

    private static async Task SeedColoursAsync(AppDbContext db, CancellationToken ct)
    {
        var existing = await db.VehicleColours.Select(c => c.Name).ToListAsync(ct);

        foreach (var (name, hex) in Colours)
        {
            if (existing.Contains(name)) continue;
            db.VehicleColours.Add(new VehicleColour { Name = name, HexCode = hex });
        }
    }

    /// <summary>
    /// Curated variants. Each entry is a real configuration; the year range is the
    /// production run of that configuration, not of the model overall.
    ///
    /// To extend: add rows here. Pre-1981 entries are fine — this seed is the only
    /// route to classics, since vPIC has no data before then.
    /// </summary>
    // CONFIDENCE KEY, so a reviewer knows where to spend their attention:
    //   [solid]  structural, or a fact I'd stake the build on
    //   [review] my best understanding — plausible, unverified, has been wrong before
    //
    // vPIC carries no trim data at all (measured), so every row below is hand-written.
    // Two errors have already been caught by the project owner reading this list:
    // a missing MX-5 1.6 automatic, and displacement stored in Trim. Assume more remain.
    private static readonly VariantSeed[] Variants =
    [
        // ─────────────── Mazda MX-5 ───────────────
        // NB (1998–2005). US got the 1.8 only; the 1.6 is a UK/Europe engine. [solid]
        new("Mazda", "MX-5", 1998, 2005, "Base", "Convertible", 1.6m, 4, FuelType.Petrol, TransmissionType.Manual, VehicleDriveType.RWD, VehicleMarket.GB),
        new("Mazda", "MX-5", 1998, 2005, "Base", "Convertible", 1.6m, 4, FuelType.Petrol, TransmissionType.Automatic, VehicleDriveType.RWD, VehicleMarket.GB),
        new("Mazda", "MX-5", 1998, 2005, "Base", "Convertible", 1.8m, 4, FuelType.Petrol, TransmissionType.Manual, VehicleDriveType.RWD, VehicleMarket.Both),
        new("Mazda", "MX-5", 1998, 2005, "Base", "Convertible", 1.8m, 4, FuelType.Petrol, TransmissionType.Automatic, VehicleDriveType.RWD, VehicleMarket.Both),
        // NB editions [review]
        new("Mazda", "MX-5", 1999, 1999, "10th Anniversary", "Convertible", 1.8m, 4, FuelType.Petrol, TransmissionType.Manual, VehicleDriveType.RWD, VehicleMarket.Both),
        new("Mazda", "MX-5", 2004, 2005, "Mazdaspeed", "Convertible", 1.8m, 4, FuelType.Petrol, TransmissionType.Manual, VehicleDriveType.RWD, VehicleMarket.US),

        // NC (2006–2015). US trims Sport / Touring / Grand Touring. [review]
        new("Mazda", "MX-5", 2006, 2015, "Sport", "Convertible", 2.0m, 4, FuelType.Petrol, TransmissionType.Manual, VehicleDriveType.RWD, VehicleMarket.US),
        new("Mazda", "MX-5", 2006, 2015, "Touring", "Convertible", 2.0m, 4, FuelType.Petrol, TransmissionType.Manual, VehicleDriveType.RWD, VehicleMarket.US),
        new("Mazda", "MX-5", 2006, 2015, "Touring", "Convertible", 2.0m, 4, FuelType.Petrol, TransmissionType.Automatic, VehicleDriveType.RWD, VehicleMarket.US),
        new("Mazda", "MX-5", 2006, 2015, "Grand Touring", "Convertible", 2.0m, 4, FuelType.Petrol, TransmissionType.Manual, VehicleDriveType.RWD, VehicleMarket.US),
        new("Mazda", "MX-5", 2006, 2015, "Grand Touring", "Convertible", 2.0m, 4, FuelType.Petrol, TransmissionType.Automatic, VehicleDriveType.RWD, VehicleMarket.US),

        // ND (2016–). Was missing entirely — a 2016+ MX-5 could not be entered. [review]
        new("Mazda", "MX-5", 2016, 2024, "Sport", "Convertible", 2.0m, 4, FuelType.Petrol, TransmissionType.Manual, VehicleDriveType.RWD, VehicleMarket.US),
        new("Mazda", "MX-5", 2016, 2024, "Club", "Convertible", 2.0m, 4, FuelType.Petrol, TransmissionType.Manual, VehicleDriveType.RWD, VehicleMarket.US),
        new("Mazda", "MX-5", 2016, 2024, "Grand Touring", "Convertible", 2.0m, 4, FuelType.Petrol, TransmissionType.Manual, VehicleDriveType.RWD, VehicleMarket.US),
        new("Mazda", "MX-5", 2016, 2024, "Grand Touring", "Convertible", 2.0m, 4, FuelType.Petrol, TransmissionType.Automatic, VehicleDriveType.RWD, VehicleMarket.US),

        // ─────────────── Ford Focus ───────────────
        // UK. Trim is the spec level; TDCi is an ENGINE and belongs in fuel/displacement,
        // not in Trim — that was the same bug as the MX-5 "1.8" trim. [review]
        new("Ford", "Focus", 2011, 2018, "Zetec", "Hatchback", 1.6m, 4, FuelType.Petrol, TransmissionType.Manual, VehicleDriveType.FWD, VehicleMarket.GB),
        new("Ford", "Focus", 2011, 2018, "Zetec", "Hatchback", 1.6m, 4, FuelType.Diesel, TransmissionType.Manual, VehicleDriveType.FWD, VehicleMarket.GB),
        new("Ford", "Focus", 2011, 2018, "Titanium", "Estate", 2.0m, 4, FuelType.Diesel, TransmissionType.Automatic, VehicleDriveType.FWD, VehicleMarket.GB),

        // ─────────────── Toyota Prius ───────────────
        // T3 / Excel are UK trims. US used Two / Three / Four / Five. [review]
        new("Toyota", "Prius", 2009, 2015, "T3", "Hatchback", 1.8m, 4, FuelType.Hybrid, TransmissionType.CVT, VehicleDriveType.FWD, VehicleMarket.GB),
        new("Toyota", "Prius", 2015, 2022, "Excel", "Hatchback", 1.8m, 4, FuelType.Hybrid, TransmissionType.CVT, VehicleDriveType.FWD, VehicleMarket.GB),

        // ─────────────── Tesla Model 3 ───────────────
        // Same configurations both sides of the Atlantic. [solid]
        new("Tesla", "Model 3", 2017, 2024, "Standard Range", "Sedan", null, null, FuelType.Electric, TransmissionType.Automatic, VehicleDriveType.RWD, VehicleMarket.Both),
        new("Tesla", "Model 3", 2017, 2024, "Long Range", "Sedan", null, null, FuelType.Electric, TransmissionType.Automatic, VehicleDriveType.AWD, VehicleMarket.Both),
        new("Tesla", "Model 3", 2017, 2024, "Performance", "Sedan", null, null, FuelType.Electric, TransmissionType.Automatic, VehicleDriveType.AWD, VehicleMarket.Both),

        // ─────────────── Ford Mustang (classic) ───────────────
        // Pre-1981, so unreachable via vPIC. "289 V8" was an engine in the Trim field —
        // the 289 also ran roughly 1965–68, not the 1964–73 previously recorded. [review]
        new("Ford", "Mustang", 1965, 1968, "Base", "Coupe", 4.7m, 8, FuelType.Petrol, TransmissionType.Manual, VehicleDriveType.RWD, VehicleMarket.US),
        new("Ford", "Mustang", 1965, 1968, "Base", "Convertible", 4.7m, 8, FuelType.Petrol, TransmissionType.Automatic, VehicleDriveType.RWD, VehicleMarket.US),
        new("Ford", "Mustang", 1965, 1968, "GT", "Coupe", 4.7m, 8, FuelType.Petrol, TransmissionType.Manual, VehicleDriveType.RWD, VehicleMarket.US),
    ];

    private sealed record VariantSeed(
        string Make, string Model, int YearFrom, int YearTo,
        string? Trim, string? BodyStyle,
        decimal? Displacement, int? Cylinders,
        FuelType Fuel, TransmissionType Transmission, VehicleDriveType Drive,
        VehicleMarket Market = VehicleMarket.Unknown);

    private static async Task SeedVariantsAsync(AppDbContext db, CancellationToken ct)
    {
        foreach (var seed in Variants)
        {
            var make = await db.VehicleMakes.FirstOrDefaultAsync(m => m.Name == seed.Make, ct);
            if (make is null)
            {
                make = new VehicleMake { Name = seed.Make };
                db.VehicleMakes.Add(make);
                await db.SaveChangesAsync(ct);
            }

            var model = await db.VehicleModels
                .FirstOrDefaultAsync(m => m.MakeId == make.Id && m.Name == seed.Model, ct);
            if (model is null)
            {
                model = new VehicleModel { MakeId = make.Id, Name = seed.Model };
                db.VehicleModels.Add(model);
                await db.SaveChangesAsync(ct);
            }

            // Identity is model + years + trim + body + displacement + transmission + fuel.
            // Displacement matters: "Base 1.6 manual" and "Base 1.8 manual" are different
            // vehicles, and omitting it would silently drop the second.
            var existing = await db.VehicleVariants.FirstOrDefaultAsync(v =>
                v.ModelId == model.Id &&
                v.YearFrom == seed.YearFrom &&
                v.YearTo == seed.YearTo &&
                v.Trim == seed.Trim &&
                v.BodyStyle == seed.BodyStyle &&
                v.EngineDisplacementL == seed.Displacement &&
                v.Transmission == seed.Transmission &&
                v.FuelType == seed.Fuel, ct);

            if (existing is not null)
            {
                // Upsert, not skip. A row that already existed still needs its non-key
                // fields refreshed — otherwise adding Market to the seed would leave every
                // pre-existing row blank forever.
                existing.EngineCylinders = seed.Cylinders;
                existing.DriveType = seed.Drive;
                existing.Market = seed.Market;
                existing.IsActive = true;
                continue;
            }

            db.VehicleVariants.Add(new VehicleVariant
            {
                ModelId = model.Id,
                YearFrom = seed.YearFrom,
                YearTo = seed.YearTo,
                Trim = seed.Trim,
                BodyStyle = seed.BodyStyle,
                EngineDisplacementL = seed.Displacement,
                EngineCylinders = seed.Cylinders,
                FuelType = seed.Fuel,
                Transmission = seed.Transmission,
                DriveType = seed.Drive,
                Market = seed.Market
            });
        }

        await db.SaveChangesAsync(ct);
        await RetireSupersededVariantsAsync(db, ct);
    }

    /// <summary>
    /// Deactivates variants of seeded models that no longer appear in the seed.
    ///
    /// Without this the seeder can only ever grow: correcting a row's year range or trim
    /// creates a new row and leaves the wrong one in place, so the picker shows both.
    /// That is exactly what happened when the MX-5 NC range was corrected from 2005–2015
    /// to 2006–2015.
    ///
    /// Deactivated rather than deleted, because a vehicle may hold a foreign key to the
    /// row. Inactive variants are excluded from the cascade but keep existing vehicles
    /// intact — and those carry a DisplayName snapshot anyway.
    ///
    /// Scoped to models the seed actually manages, so anything imported from vPIC or
    /// added by other means is left alone.
    /// </summary>
    private static async Task RetireSupersededVariantsAsync(AppDbContext db, CancellationToken ct)
    {
        var seededModels = Variants.Select(v => (v.Make, v.Model)).Distinct().ToList();

        foreach (var (makeName, modelName) in seededModels)
        {
            var model = await db.VehicleModels
                .Include(m => m.Make)
                .FirstOrDefaultAsync(m => m.Name == modelName && m.Make.Name == makeName, ct);
            if (model is null) continue;

            var wanted = Variants
                .Where(s => s.Make == makeName && s.Model == modelName)
                .ToList();

            var actual = await db.VehicleVariants.Where(v => v.ModelId == model.Id).ToListAsync(ct);

            foreach (var variant in actual)
            {
                var stillWanted = wanted.Any(s =>
                    s.YearFrom == variant.YearFrom &&
                    s.YearTo == variant.YearTo &&
                    s.Trim == variant.Trim &&
                    s.BodyStyle == variant.BodyStyle &&
                    s.Displacement == variant.EngineDisplacementL &&
                    s.Transmission == variant.Transmission &&
                    s.Fuel == variant.FuelType);

                if (!stillWanted && variant.IsActive)
                    variant.IsActive = false;
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
