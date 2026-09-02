using Microsoft.EntityFrameworkCore;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Vehicles;

public class VehicleRepository(AppDbContext db) : IVehicleRepository
{
    public Task<List<Vehicle>> SearchAsync(string compact, string term, int take, CancellationToken ct) =>
        db.Vehicles
          .Include(v => v.Customer)
          .Where(v =>
              (v.Registration != null && v.Registration.Contains(compact)) ||
              (v.Vin != null && v.Vin.ToUpper().Contains(compact)) ||
              (v.DisplayName != null && v.DisplayName.ToUpper().Contains(term)))
          .OrderBy(v => v.DisplayName)
          .Take(take)
          .ToListAsync(ct);

    public async Task<Vehicle?> FindAsync(Guid id, CancellationToken ct) =>
        await db.Vehicles.FindAsync([id], ct);

    // Variant is left-joined: a vehicle created before the catalogue has none, and the
    // handler falls back to the deprecated free-text columns for those rows.
    public Task<Vehicle?> FindWithDetailsAsync(Guid id, CancellationToken ct) =>
        db.Vehicles
          .Include(x => x.Customer)
          .Include(x => x.Colour)
          .Include(x => x.Variant!).ThenInclude(va => va.Model).ThenInclude(m => m.Make)
          .FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct) =>
        db.Vehicles.AnyAsync(v => v.Id == id, ct);

    // No Include here on purpose: a full Select projection makes EF ignore Include
    // entirely, so the ones this query used to carry were dead configuration that read as
    // load-bearing. Noted in the performance section of docs/review-findings.md.
    public Task<List<VehicleHistoryRow>> GetHistoryAsync(Guid vehicleId, CancellationToken ct) =>
        db.Jobs
          .Where(j => j.VehicleId == vehicleId)
          .OrderByDescending(j => j.CreatedAtUtc)
          .Select(j => new VehicleHistoryRow(
              j.Id, j.Title, j.Status, j.ScheduledStartUtc, j.CreatedAtUtc,
              j.PartLines.Select(pl => pl.InventoryItem.Name).ToList(),
              j.LaborLines.Sum(l => l.Hours * l.Rate),
              j.PartLines.Sum(p => p.Quantity * p.UnitPrice)))
          .ToListAsync(ct);

    public async Task<Customer?> FindCustomerAsync(Guid customerId, CancellationToken ct) =>
        await db.Customers.FindAsync([customerId], ct);

    // The catalogue is global, so this is not tenant-filtered -- but the UI can only ever
    // offer variants it was served, and the year is re-checked by the service regardless.
    public Task<VehicleVariant?> FindActiveVariantAsync(Guid variantId, CancellationToken ct) =>
        db.VehicleVariants
          .Include(v => v.Model).ThenInclude(m => m.Make)
          .FirstOrDefaultAsync(v => v.Id == variantId && v.IsActive, ct);

    public Task<bool> ActiveColourExistsAsync(Guid colourId, CancellationToken ct) =>
        db.VehicleColours.AnyAsync(c => c.Id == colourId && c.IsActive, ct);

    // Tenant filtered, so this can only ever match a vehicle in the caller's own business.
    public Task<Vehicle?> FindByRegistrationAsync(string registration, Guid? excludeVehicleId, CancellationToken ct) =>
        db.Vehicles
          .Include(v => v.Customer)
          .FirstOrDefaultAsync(v =>
              v.Registration == registration &&
              (excludeVehicleId == null || v.Id != excludeVehicleId), ct);

    public Task<int> CountJobsAsync(Guid vehicleId, CancellationToken ct) =>
        db.Jobs.CountAsync(j => j.VehicleId == vehicleId, ct);

    public Task<int> CountBookingsAsync(Guid vehicleId, CancellationToken ct) =>
        db.Bookings.CountAsync(b => b.VehicleId == vehicleId, ct);

    public void Add(Vehicle vehicle) => db.Vehicles.Add(vehicle);
    public void Remove(Vehicle vehicle) => db.Vehicles.Remove(vehicle);
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
