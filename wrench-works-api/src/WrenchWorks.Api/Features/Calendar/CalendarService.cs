using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Calendar;

public class CalendarService(AppDbContext db, CurrentUserService currentUser) : ICalendarService
{
    public async Task<List<BookingDto>> GetBookingsAsync([AsParameters] GetBookingsQuery query, CancellationToken ct)
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
                b.VehicleId, (b.Vehicle.DisplayName ?? "") + (b.Vehicle.Registration != null ? " " + b.Vehicle.Registration : ""),
                b.Title, b.StartUtc, b.EndUtc, b.Notes,
                b.Status.ToString(), b.JobId, b.CreatedAtUtc))
            .ToListAsync(ct);

        return bookings;
    }

    public async Task<BookingDto> CreateBookingAsync(CreateBookingRequest request, CancellationToken ct)
    {
        await new CreateBookingValidator().ValidateAndThrowAsync(request, ct);

        var businessId = currentUser.RequireBusinessId();

        var (zone, customer, vehicle) = await ResolveBookingTargetsAsync(
            db, request.ZoneId, request.CustomerId, request.VehicleId, ct);

        await EnsureSlotIsFreeAsync(db, request.ZoneId, request.StartUtc, request.EndUtc, zone.Capacity, null, ct);

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

        // Two saves, deliberately: booking.JobId and job.BookingId point at each other, so
        // the reverse FK can only be set once the first insert has produced a row. Not
        // atomic — a failure between them leaves a job with no back-reference.
        if (linkedJob != null)
        {
            linkedJob.BookingId = booking.Id;
            await db.SaveChangesAsync(ct);
        }

        return new BookingDto(booking.Id, booking.ZoneId, zone.Name, zone.Color,
                booking.CustomerId, customer.Name, booking.VehicleId,
                $"{vehicle.DisplayName} {vehicle.Registration}".Trim(),
                booking.Title, booking.StartUtc, booking.EndUtc, booking.Notes,
                booking.Status.ToString(), booking.JobId, booking.CreatedAtUtc);
    }

    /// <summary>
    /// Full update of a booking — zone, customer, vehicle, title, times and notes.
    ///
    /// Until this existed a booking was immutable once created: the only way to change
    /// a time was cancel-and-recreate, and cancelling CLOSES the linked job. So the most
    /// routine event in a workshop (a job slipping a day) destroyed work.
    ///
    /// Shares conflict checking and the job cascade with /move, so the two cannot drift.
    /// </summary>
    public async Task<BookingActionResultDto> UpdateBookingAsync(Guid id, UpdateBookingRequest request, CancellationToken ct)
    {
        var booking = await db.Bookings.FindAsync([id], ct)
            ?? throw new NotFoundException("Booking not found");

        if (booking.Status == BookingStatus.Cancelled)
            throw new ConflictException("This booking was cancelled and can no longer be edited");

        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ValidationException([new FluentValidation.Results.ValidationFailure(
                nameof(request.Title), "Title is required")]);

        if (request.StartUtc >= request.EndUtc)
            throw new ValidationException([new FluentValidation.Results.ValidationFailure(
                nameof(request.StartUtc), "Start must be before end")]);

        var (zone, _, _) = await ResolveBookingTargetsAsync(
            db, request.ZoneId, request.CustomerId, request.VehicleId, ct);

        await EnsureSlotIsFreeAsync(db, request.ZoneId, request.StartUtc, request.EndUtc, zone.Capacity, id, ct);

        booking.ZoneId = request.ZoneId;
        booking.CustomerId = request.CustomerId;
        booking.VehicleId = request.VehicleId;
        booking.Title = request.Title.Trim();
        booking.StartUtc = request.StartUtc;
        booking.EndUtc = request.EndUtc;
        booking.Notes = request.Notes;

        await CascadeToJobAsync(db, booking, request.ZoneId, request.StartUtc, request.EndUtc, ct);

        await db.SaveChangesAsync(ct);
        return new BookingActionResultDto("Booking updated");
    }

    /// <summary>
    /// Moves a booking to Completed or NoShow.
    ///
    /// BookingStatus has four values and the UI styles all four, but only Confirmed
    /// (on create) and Cancelled (on delete) were ever reachable — the other two were
    /// decorative. Cancelling still goes through DELETE, which also closes the job.
    /// </summary>
    public async Task<BookingStatusDto> UpdateBookingStatusAsync(Guid id, UpdateBookingStatusRequest request, CancellationToken ct)
    {
        var booking = await db.Bookings.FindAsync([id], ct)
            ?? throw new NotFoundException("Booking not found");

        if (!Enum.TryParse<BookingStatus>(request.Status, true, out var status))
            throw new ValidationException([new FluentValidation.Results.ValidationFailure(
                nameof(request.Status), $"'{request.Status}' is not a valid booking status")]);

        // Cancelling has side effects on the linked job, so it stays on DELETE.
        if (status == BookingStatus.Cancelled)
            throw new ConflictException("Use DELETE to cancel a booking so the linked job is handled");

        booking.Status = status;
        await db.SaveChangesAsync(ct);

        return new BookingStatusDto(booking.Id, booking.Status.ToString());
    }

    public async Task<BookingActionResultDto> MoveBookingAsync(Guid id, MoveBookingRequest request, CancellationToken ct)
    {
        var booking = await db.Bookings.FindAsync([id], ct)
            ?? throw new NotFoundException("Booking not found");

        var zone = await db.Zones.FindAsync([request.ZoneId], ct)
            ?? throw new NotFoundException("Zone not found");

        if (request.StartUtc >= request.EndUtc)
            throw new ValidationException("Start must be before end");

        var conflicts = await CheckConflictsAsync(db, request.ZoneId, request.StartUtc, request.EndUtc, zone.Capacity, id, ct);
        if (conflicts.Count > 0)
            throw new ConflictException("Booking conflicts detected", new { conflictingBookingIds = conflicts });

        booking.ZoneId = request.ZoneId;
        booking.StartUtc = request.StartUtc;
        booking.EndUtc = request.EndUtc;

        await CascadeToJobAsync(db, booking, request.ZoneId, request.StartUtc, request.EndUtc, ct);

        await db.SaveChangesAsync(ct);
        return new BookingActionResultDto("Booking moved successfully");
    }

    public async Task DeleteBookingAsync(Guid id, CancellationToken ct)
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
        return;
    }

    // Keeps a linked job's schedule in step with its booking.
    //
    // NOTE: deliberately a plain comment, not an XML doc comment. The .NET 10 preview
    // OpenAPI XML-comment source generator emits `System.Void` for a Task-returning
    // (void) method carrying a <summary>, which fails to compile with CS0673.
    private static async Task CascadeToJobAsync(AppDbContext db, Booking booking, Guid zoneId, DateTime startUtc, DateTime endUtc, CancellationToken ct)
    {
        if (!booking.JobId.HasValue) return;

        var job = await db.Jobs.FindAsync([booking.JobId.Value], ct);
        if (job is null) return;

        job.AssignedZoneId = zoneId;
        job.ScheduledStartUtc = startUtc;
        job.ScheduledEndUtc = endUtc;
    }

    /// <summary>
    /// Loads the zone, customer and vehicle a booking points at, each through its
    /// tenant-filtered DbSet so another business's row is simply not found.
    ///
    /// Extracted because create, update and move each repeated it, and one of the three
    /// omitting a check is exactly how the job endpoints ended up accepting a foreign zone
    /// (docs/review-findings.md finding 2). One resolver, one place to get it wrong.
    /// </summary>
    private static async Task<(Zone Zone, Customer Customer, Vehicle Vehicle)> ResolveBookingTargetsAsync(AppDbContext db, Guid zoneId, Guid customerId, Guid vehicleId, CancellationToken ct)
    {
        var zone = await db.Zones.FindAsync([zoneId], ct)
            ?? throw new NotFoundException("Zone not found");
        var customer = await db.Customers.FindAsync([customerId], ct)
            ?? throw new NotFoundException("Customer not found");
        var vehicle = await db.Vehicles.FindAsync([vehicleId], ct)
            ?? throw new NotFoundException("Vehicle not found");

        return (zone, customer, vehicle);
    }

    // Throws if the slot is already at capacity. Wraps CheckConflictsAsync so callers read
    // as an assertion rather than as a list they must remember to inspect — forgetting to
    // check the returned list would silently permit a double booking.
    //
    // Plain // rather than ///: the .NET 10 preview OpenAPI XML-comment source generator
    // emits System.Void for a Task-returning method carrying a <summary> and fails with
    // CS0673. See the note in CLAUDE.md.
    private static async Task EnsureSlotIsFreeAsync(AppDbContext db, Guid zoneId, DateTime startUtc, DateTime endUtc, int capacity, Guid? excludeBookingId, CancellationToken ct)
    {
        var conflicts = await CheckConflictsAsync(db, zoneId, startUtc, endUtc, capacity, excludeBookingId, ct);
        if (conflicts.Count > 0)
            throw new ConflictException("Booking conflicts detected", new { conflictingBookingIds = conflicts });
    }

    private static async Task<List<Guid>> CheckConflictsAsync(AppDbContext db, Guid zoneId, DateTime start, DateTime end, int capacity, Guid? excludeBookingId, CancellationToken ct)
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
