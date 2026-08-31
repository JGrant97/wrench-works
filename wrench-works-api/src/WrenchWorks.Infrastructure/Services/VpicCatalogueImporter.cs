using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Infrastructure.Services;

/// <summary>
/// Imports the make → model spine of the vehicle catalogue from NHTSA vPIC.
///
/// Measured against the live API on 28 Aug 2026 (see docs/vehicle-catalogue.md):
///   • GetAllMakes returns 12,351 makes — overwhelmingly trailer and equipment
///     manufacturers. We import ONLY the passenger vehicle types below
///     (car 195, truck 207, mpv 111 makes) or the make dropdown is unusable.
///   • Coverage starts at model year 1981; earlier years return nothing at all.
///   • vPIC exposes no browsable trim/engine/fuel/transmission data, so this importer
///     deliberately creates NO variants. Those are hand-curated seed data.
///
/// Idempotent: re-running upserts by (name) for makes and (makeId, name) for models.
/// </summary>
public class VpicCatalogueImporter(
    HttpClient http,
    AppDbContext db,
    ILogger<VpicCatalogueImporter> logger)
{
    private const string BaseUrl = "https://vpic.nhtsa.dot.gov/api/vehicles";

    /// <summary>Passenger vehicle types a workshop actually services. Motorcycles (1,684 makes) are excluded.</summary>
    private static readonly string[] PassengerVehicleTypes = ["car", "truck", "mpv"];

    /// <summary>vPIC has no data before this model year — the 17-character VIN standard.</summary>
    public const int EarliestVpicYear = 1981;

    private sealed record VpicMake(int Make_ID, string Make_Name);
    private sealed record VpicMakeResponse(int Count, List<VpicMake> Results);

    private sealed record VpicModel(int Model_ID, string Model_Name);
    private sealed record VpicModelResponse(int Count, List<VpicModel> Results);

    public record ImportResult(int MakesImported, int ModelsImported, int YearsScanned, List<string> Warnings);

    /// <summary>
    /// Imports passenger makes, then models for each make across the given year range.
    /// Model-year scanning is the expensive part: one request per make per year.
    /// </summary>
    public async Task<ImportResult> ImportAsync(
        int fromYear,
        int toYear,
        CancellationToken ct = default)
    {
        var warnings = new List<string>();

        if (fromYear < EarliestVpicYear)
        {
            warnings.Add($"vPIC has no data before {EarliestVpicYear}; requested fromYear {fromYear} was clamped. " +
                         "Pre-1981 vehicles must be hand-seeded.");
            fromYear = EarliestVpicYear;
        }

        var makes = await ImportMakesAsync(ct);
        logger.LogInformation("Imported {Count} passenger makes from vPIC", makes.Count);

        var modelCount = 0;
        for (var year = fromYear; year <= toYear; year++)
        {
            foreach (var make in makes)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    modelCount += await ImportModelsForMakeYearAsync(make, year, ct);
                }
                catch (Exception ex)
                {
                    // One bad make/year must not abort a multi-thousand-request import.
                    warnings.Add($"{make.Name} {year}: {ex.Message}");
                    logger.LogWarning(ex, "Model import failed for {Make} {Year}", make.Name, year);
                }
            }

            await db.SaveChangesAsync(ct);
            logger.LogInformation("Year {Year} complete — {Models} models so far", year, modelCount);
        }

        return new ImportResult(makes.Count, modelCount, toYear - fromYear + 1, warnings);
    }

    /// <summary>Upserts the union of makes across the passenger vehicle types.</summary>
    private async Task<List<VehicleMake>> ImportMakesAsync(CancellationToken ct)
    {
        var seen = new Dictionary<int, string>();

        foreach (var type in PassengerVehicleTypes)
        {
            var response = await http.GetFromJsonAsync<VpicMakeResponse>(
                $"{BaseUrl}/GetMakesForVehicleType/{type}?format=json", ct);

            foreach (var m in response?.Results ?? [])
            {
                if (!string.IsNullOrWhiteSpace(m.Make_Name))
                    seen[m.Make_ID] = Normalise(m.Make_Name);
            }
        }

        var existing = await db.VehicleMakes.ToDictionaryAsync(m => m.Name, ct);
        var result = new List<VehicleMake>();

        foreach (var (vpicId, name) in seen)
        {
            if (existing.TryGetValue(name, out var make))
            {
                make.VpicMakeId ??= vpicId;
            }
            else
            {
                make = new VehicleMake { Name = name, VpicMakeId = vpicId };
                db.VehicleMakes.Add(make);
                existing[name] = make;
            }
            result.Add(make);
        }

        await db.SaveChangesAsync(ct);
        return result;
    }

    private async Task<int> ImportModelsForMakeYearAsync(VehicleMake make, int year, CancellationToken ct)
    {
        var slug = Uri.EscapeDataString(make.Name);
        var response = await http.GetFromJsonAsync<VpicModelResponse>(
            $"{BaseUrl}/getmodelsformakeyear/make/{slug}/modelyear/{year}?format=json", ct);

        if (response is null || response.Count == 0) return 0;

        var existing = await db.VehicleModels
            .Where(m => m.MakeId == make.Id)
            .ToDictionaryAsync(m => m.Name, ct);

        var added = 0;
        foreach (var m in response.Results)
        {
            var name = Normalise(m.Model_Name);
            if (string.IsNullOrWhiteSpace(name)) continue;

            if (existing.TryGetValue(name, out var model))
            {
                model.VpicModelId ??= m.Model_ID;
                continue;
            }

            var created = new VehicleModel { MakeId = make.Id, Name = name, VpicModelId = m.Model_ID };
            db.VehicleModels.Add(created);
            existing[name] = created;
            added++;
        }

        return added;
    }

    /// <summary>vPIC returns SHOUTING CASE ("MAZDA"); store it presentably.</summary>
    private static string Normalise(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.Length == 0) return trimmed;

        // Leave mixed-case and short designations (BMW, MX-5) alone.
        if (trimmed.Any(char.IsLower)) return trimmed;
        if (trimmed.Length <= 3) return trimmed;

        return string.Join(' ', trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => word.Length <= 3
                ? word
                : char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant()));
    }
}
