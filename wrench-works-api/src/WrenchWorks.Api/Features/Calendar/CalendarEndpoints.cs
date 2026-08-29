using FluentValidation;
using Microsoft.EntityFrameworkCore;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Calendar;

// DTOs
public record CreateBookingRequest(Guid ZoneId, Guid CustomerId, Guid VehicleId, string Title, DateTime StartUtc, DateTime EndUtc, string? Notes, bool CreateJob);
public record MoveBookingRequest(Guid ZoneId, DateTime StartUtc, DateTime EndUtc);
public record BookingDto(Guid Id, Guid ZoneId, string ZoneName, string? ZoneColor, Guid CustomerId, string CustomerName, Guid VehicleId, string? VehicleDisplay, string Title, DateTime StartUtc, DateTime EndUtc, string? Notes, string Status, Guid? JobId, DateTime CreatedAtUtc);

public record GetBookingsQuery(DateTime From, DateTime To, Guid? ZoneId = null);

// Validators
public class CreateBookingValidator : AbstractValidator<CreateBookingRequest>
{
    public CreateBookingValidator()
    {
        RuleFor(x => x.ZoneId).NotEmpty();
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.VehicleId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.StartUtc).NotEmpty().LessThan(x => x.EndUtc).WithMessage("Start must be before end");
        RuleFor(x => x.EndUtc).NotEmpty();
    }
}

// Endpoints
public static class CalendarEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/calendar").WithTags("Calendar").RequireAuthorization();

        group.MapGet("/bookings", GetBookingsAsync).RequireAuthorization("calendar.view");
        group.MapPost("/bookings", CreateBookingAsync).RequireAuthorization("calendar.edit");
        group.MapPut("/bookings/{id:guid}/move", MoveBookingAsync).RequireAuthorization("calendar.edit");
        group.MapDelete("/bookings/{id:guid}", DeleteBookingAsync).RequireAuthorization("calendar.edit");
    }

    private static async Task<IResult> GetBookingsAsync(
        [AsParameters] GetBookingsQuery query,
        AppDbContext db,
        CancellationToken ct)
    {
        var q = db.Bookings
            .Include(b => b.Zone)
            .Include(b => b.Customer)
            .Include(b => b.Vehicle)
            .Where(b => b.StartUtc < query.To && b.EndUtc > query.From)
            .Where(b => b.Status != BookingStatus.Cancelled);

        if (query.ZoneId.HasValue)
            q = q.Where(b => b.ZoneId == query.ZoneId.Value);

        var bookings = await q
            .OrderBy(b => b.StartUtc)
            .Select(b => new BookingDto(
                b.Id, b.ZoneId, b.Zone.Name, b.Zone.Color,
                b.CustomerId, b.Customer.Name,
                b.VehicleId, (b.Vehicle.Make ?? "") + " " + (b.Vehicle.Model ?? "") + " " + (b.Vehicle.Registration ?? ""),
                b.Title, b.StartUtc, b.EndUtc, b.Notes,
                b.Status.ToString(), b.JobId, b.CreatedAtUtc))
            .ToListAsync(ct);

        return Results.Ok(bookings);
    }

    private static async Task<IResult> CreateBookingAsync(
        CreateBookingRequest request,
        AppDbContext db,
        CurrentUserService currentUser,
        CancellationToken ct)
    {
        await new CreateBookingValidator().ValidateAndThrowAsync(request, ct);

        var businessId = currentUser.RequireBusinessId();

        // Validate zone exists
        var zone = await db.Zones.FindAsync([request.ZoneId], ct)
            ?? throw new NotFoundException("Zone not found");

        // Validate customer and vehicle
        var customer = await db.Customers.FindAsync([request.CustomerId], ct)
            ?? throw new NotFoundException("Customer not found");
        var vehicle = await db.Vehicles.FindAsync([request.VehicleId], ct)
            ?? throw new NotFoundException("Vehicle not found");

        // Check conflicts
        var conflicts = await CheckConflictsAsync(db, request.ZoneId, request.StartUtc, request.EndUtc, zone.Capacity, null, ct);
        if (conflicts.Count > 0)
            throw new ConflictException("Booking conflicts detected", new { conflictingBookingIds = conflicts });

        var booking = new Booking
        {
            BusinessId = businessId,
            ZoneId = request.ZoneId,
            CustomerId = request.CustomerId,
            VehicleId = request.VehicleId,
            Title = request.Title.Trim(),
            StartUtc = request.StartUtc,
            EndUtc = request.EndUtc,
            Notes = request.Notes,
            CreatedByUserId = currentUser.UserId
        };

        // Optionally create a linked job
        Job? linkedJob = null;
        if (request.CreateJob)
        {
            linkedJob = new Job
            {
                BusinessId = businessId,
                CustomerId = request.CustomerId,
                VehicleId = request.VehicleId,
                Title = request.Title.Trim(),
                Status = JobStatus.Scheduled,
                AssignedZoneId = request.ZoneId,
                ScheduledStartUtc = request.StartUtc,
                ScheduledEndUtc = request.EndUtc,
                CreatedByUserId = currentUser.UserId
            };
            db.Jobs.Add(linkedJob);
            booking.Job = linkedJob; // Sets booking.JobId
        }

        db.Bookings.Add(booking);
        await db.SaveChangesAsync(ct);

        // Now set the reverse FK (Job → Booking) without circular insert
        if (linkedJob != null)
        {
            linkedJob.BookingId = booking.Id;
            await db.SaveChangesAsync(ct);
        }

        return Results.Created($"/api/calendar/bookings/{booking.Id}",
            new BookingDto(booking.Id, booking.ZoneId, zone.Name, zone.Color,
                booking.CustomerId, customer.Name, booking.VehicleId,
                $"{vehicle.Make} {vehicle.Model} {vehicle.Registration}".Trim(),
                booking.Title, booking.StartUtc, booking.EndUtc, booking.Notes,
                booking.Status.ToString(), booking.JobId, booking.CreatedAtUtc));
    }

    private static async Task<IResult> MoveBookingAsync(
        Guid id,
        MoveBookingRequest request,
        AppDbContext db,
        CancellationToken ct)
    {
        var booking = await db.Bookings.FindAsync([id], ct)
            ?? throw new NotFoundException("Booking not found");

        var zone = await db.Zones.FindAsync([request.ZoneId], ct)
            ?? throw new NotFoundException("Zone not found");

        if (request.StartUtc >= request.EndUtc)
            return Results.BadRequest(new { code = "validation_error", message = "Start must be before end" });

        var conflicts = await CheckConflictsAsync(db, request.ZoneId, request.StartUtc, request.EndUtc, zone.Capacity, id, ct);
        if (conflicts.Count > 0)
            throw new ConflictException("Booking conflicts detected", new { conflictingBookingIds = conflicts });

        booking.ZoneId = request.ZoneId;
        booking.StartUtc = request.StartUtc;
        booking.EndUtc = request.EndUtc;

        // Update linked job schedule if exists
        if (booking.JobId.HasValue)
        {
            var job = await db.Jobs.FindAsync([booking.JobId.Value], ct);
            if (job != null)
            {
                job.AssignedZoneId = request.ZoneId;
                job.ScheduledStartUtc = request.StartUtc;
                job.ScheduledEndUtc = request.EndUtc;
            }
        }

        await db.SaveChangesAsync(ct);
        return Results.Ok(new { message = "Booking moved successfully" });
    }

    private static async Task<IResult> DeleteBookingAsync(Guid id, AppDbContext db, CancellationToken ct)
    {
        var booking = await db.Bookings.FindAsync([id], ct)
            ?? throw new NotFoundException("Booking not found");

        booking.Status = BookingStatus.Cancelled;

        // Cancel the linked job too
        if (booking.JobId.HasValue)
        {
            var job = await db.Jobs.FindAsync([booking.JobId.Value], ct);
            if (job != null && job.Status != JobStatus.Closed && job.Status != JobStatus.Invoiced && job.Status != JobStatus.Completed)
            {
                job.Status = JobStatus.Closed;
            }
        }

        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<List<Guid>> CheckConflictsAsync(
        AppDbContext db, Guid zoneId, DateTime start, DateTime end, int capacity, Guid? excludeBookingId, CancellationToken ct)
    {
        var query = db.Bookings
            .Where(b => b.ZoneId == zoneId)
            .Where(b => b.Status != BookingStatus.Cancelled)
            .Where(b => b.StartUtc < end && b.EndUtc > start);

        if (excludeBookingId.HasValue)
            query = query.Where(b => b.Id != excludeBookingId.Value);

        var overlapping = await query.Select(b => b.Id).ToListAsync(ct);

        // If capacity allows multiple concurrent bookings
        if (overlapping.Count < capacity)
            return [];

        return overlapping;
    }
}
