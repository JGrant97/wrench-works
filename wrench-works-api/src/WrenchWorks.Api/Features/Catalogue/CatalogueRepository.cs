using Microsoft.EntityFrameworkCore;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Catalogue;

public class CatalogueRepository(AppDbContext db) : ICatalogueRepository
{
    // Only makes that actually have models -- an empty make is a dead end in the cascade.
    public Task<List<VehicleMake>> GetActiveMakesWithModelsAsync(CancellationToken ct) =>
        db.VehicleMakes
          .Where(m => m.IsActive && m.Models.Any(mo => mo.IsActive))
          .OrderBy(m => m.Name)
          .ToListAsync(ct);

    public Task<bool> MakeExistsAsync(Guid makeId, CancellationToken ct) =>
        db.VehicleMakes.AnyAsync(m => m.Id == makeId, ct);

    public Task<List<VehicleModel>> GetActiveModelsAsync(Guid makeId, CancellationToken ct) =>
        db.VehicleModels
          .Where(m => m.MakeId == makeId && m.IsActive)
          .OrderBy(m => m.Name)
          .ToListAsync(ct);

    public Task<bool> ModelExistsAsync(Guid modelId, CancellationToken ct) =>
        db.VehicleModels.AnyAsync(m => m.Id == modelId, ct);

    // Projected in SQL: flattening the ranges needs two ints per variant, nothing else.
    public Task<List<VariantYearRange>> GetVariantYearRangesAsync(Guid modelId, CancellationToken ct) =>
        db.VehicleVariants
          .Where(v => v.ModelId == modelId && v.IsActive)
          .Select(v => new VariantYearRange(v.YearFrom, v.YearTo))
          .ToListAsync(ct);

    public Task<List<VehicleVariant>> GetActiveVariantsAsync(Guid modelId, int? year, CancellationToken ct)
    {
        var query = db.VehicleVariants.Where(v => v.ModelId == modelId && v.IsActive);

        if (year.HasValue)
            query = query.Where(v => v.YearFrom <= year.Value && v.YearTo >= year.Value);

        return query.OrderBy(v => v.Trim).ThenBy(v => v.BodyStyle).ToListAsync(ct);
    }

    // Include the model and make: the detail response names both, and this is what the
    // vehicle edit form hydrates its picker from.
    public Task<VehicleVariant?> FindVariantWithModelAndMakeAsync(Guid variantId, CancellationToken ct) =>
        db.VehicleVariants
          .Include(v => v.Model).ThenInclude(m => m.Make)
          .FirstOrDefaultAsync(v => v.Id == variantId, ct);

    public Task<List<VehicleColour>> GetActiveColoursAsync(CancellationToken ct) =>
        db.VehicleColours.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync(ct);
}
