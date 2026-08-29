using FluentValidation;
using Microsoft.EntityFrameworkCore;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Vehicles;

public record CreateVehicleRequest(Guid CustomerId, string? Make, string? Model, int? Year, string? Vin, string? Registration, string? EngineType, string? FuelType, string? Notes);
public record UpdateVehicleRequest(string? Make, string? Model, int? Year, string? Vin, string? Registration, string? EngineType, string? FuelType, string? Notes);
public record VehicleDto(Guid Id, Guid CustomerId, string? CustomerName, string? Make, string? Model, int? Year, string? Vin, string? Registration, string? EngineType, string? FuelType, string? Notes, DateTime CreatedAtUtc);
public record VehicleHistoryItemDto(Guid JobId, string Title, string Status, DateTime? ScheduledStartUtc, DateTime CreatedAtUtc, IEnumerable<string> PartsUsed, decimal LaborTotal, decimal PartsTotal);

public class CreateVehicleValidator : AbstractValidator<CreateVehicleRequest>
{
    public CreateVehicleValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Vin).MaximumLength(17);
        RuleFor(x => x.Registration).MaximumLength(20);
    }
}

public static class VehicleEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/vehicles").WithTags("Vehicles").RequireAuthorization();

        group.MapPost("/", CreateAsync).RequireAuthorization("vehicles.manage");
        group.MapPut("/{id:guid}", UpdateAsync).RequireAuthorization("vehicles.manage");
        group.MapGet("/{id:guid}", GetAsync).RequireAuthorization("vehicles.view");
        group.MapGet("/{id:guid}/history", GetHistoryAsync).RequireAuthorization("vehicles.view");
    }

    private static async Task<IResult> CreateAsync(
        CreateVehicleRequest request,
        AppDbContext db,
        CurrentUserService currentUser,
        CancellationToken ct)
    {
        await new CreateVehicleValidator().ValidateAndThrowAsync(request, ct);

        var customer = await db.Customers.FindAsync([request.CustomerId], ct)
            ?? throw new NotFoundException("Customer not found");

        var vehicle = new Vehicle
        {
            BusinessId = currentUser.RequireBusinessId(),
            CustomerId = request.CustomerId,
            Make = request.Make?.Trim(),
            Model = request.Model?.Trim(),
            Year = request.Year,
            Vin = request.Vin?.Trim(),
            Registration = request.Registration?.Trim().ToUpperInvariant(),
            EngineType = request.EngineType,
            FuelType = request.FuelType,
            Notes = request.Notes
        };
        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/vehicles/{vehicle.Id}",
            new VehicleDto(vehicle.Id, vehicle.CustomerId, customer.Name, vehicle.Make, vehicle.Model, vehicle.Year, vehicle.Vin, vehicle.Registration, vehicle.EngineType, vehicle.FuelType, vehicle.Notes, vehicle.CreatedAtUtc));
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateVehicleRequest request,
        AppDbContext db,
        CancellationToken ct)
    {
        var vehicle = await db.Vehicles.Include(v => v.Customer).FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw new NotFoundException("Vehicle not found");

        vehicle.Make = request.Make?.Trim();
        vehicle.Model = request.Model?.Trim();
        vehicle.Year = request.Year;
        vehicle.Vin = request.Vin?.Trim();
        vehicle.Registration = request.Registration?.Trim().ToUpperInvariant();
        vehicle.EngineType = request.EngineType;
        vehicle.FuelType = request.FuelType;
        vehicle.Notes = request.Notes;

        await db.SaveChangesAsync(ct);
        return Results.Ok(new VehicleDto(vehicle.Id, vehicle.CustomerId, vehicle.Customer.Name, vehicle.Make, vehicle.Model, vehicle.Year, vehicle.Vin, vehicle.Registration, vehicle.EngineType, vehicle.FuelType, vehicle.Notes, vehicle.CreatedAtUtc));
    }

    private static async Task<IResult> GetAsync(Guid id, AppDbContext db, CancellationToken ct)
    {
        var vehicle = await db.Vehicles
            .Include(v => v.Customer)
            .FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw new NotFoundException("Vehicle not found");

        return Results.Ok(new VehicleDto(vehicle.Id, vehicle.CustomerId, vehicle.Customer.Name, vehicle.Make, vehicle.Model, vehicle.Year, vehicle.Vin, vehicle.Registration, vehicle.EngineType, vehicle.FuelType, vehicle.Notes, vehicle.CreatedAtUtc));
    }

    private static async Task<IResult> GetHistoryAsync(Guid id, AppDbContext db, CancellationToken ct)
    {
        var vehicle = await db.Vehicles.AnyAsync(v => v.Id == id, ct);
        if (!vehicle) throw new NotFoundException("Vehicle not found");

        var jobs = await db.Jobs
            .Where(j => j.VehicleId == id)
            .OrderByDescending(j => j.CreatedAtUtc)
            .Include(j => j.PartLines).ThenInclude(pl => pl.InventoryItem)
            .Include(j => j.LaborLines)
            .Select(j => new VehicleHistoryItemDto(
                j.Id,
                j.Title,
                j.Status.ToString(),
                j.ScheduledStartUtc,
                j.CreatedAtUtc,
                j.PartLines.Select(pl => pl.InventoryItem.Name),
                j.LaborLines.Sum(l => l.Hours * l.Rate),
                j.PartLines.Sum(p => p.Quantity * p.UnitPrice)))
            .ToListAsync(ct);

        return Results.Ok(jobs);
    }
}
