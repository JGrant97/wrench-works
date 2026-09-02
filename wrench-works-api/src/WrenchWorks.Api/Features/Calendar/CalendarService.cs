using FluentValidation;
using FluentValidation.Results;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Calendar;

public class CalendarService(ICalendarRepository repository, CurrentUserService currentUser) : ICalendarService
{
    public Task<List<Booking>> GetBookingsAsync(
        DateTime fromUtc, DateTime toUtc, Guid? zoneId, CancellationToken ct) =>
        repository.GetBookingsInRangeAsync(fromUtc, toUtc, zoneId, ct);

    public async Task<Booking> CreateBookingAsync(CreateBookingRequest request, CancellationToken ct)
    {
        await new CreateBookingValidator().ValidateAndThrowAsync(request, ct);

        var businessId = currentUser.RequireBusinessId();

        var (zone, customer, vehicle) = await ResolveBookingTargetsAsync(
            request.ZoneId, request.CustomerId, request.VehicleId, ct);

        await EnsureSlotIsFreeAsync(request.ZoneId, request.StartUtc, request.EndUtc, zone.Capacity, null, ct);

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
            CreatedByUserId = currentUser.UserId,
            // Set so the handler can name the zone, customer and vehicle without re-reading.
            Zone = zone,
            Customer = customer,
            Vehicle = vehicle
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
            repository.AddJob(linkedJob);
            booking.Job = linkedJob; // Sets booking.JobId
        }

        repository.AddBooking(booking);
        await repository.SaveChangesAsync(ct);

        // Two saves, deliberately: booking.JobId and job.BookingId point at each other, so
        // the reverse FK can only be set once the first insert has produced a row. Not
        // atomic -- a failure between them leaves a job with no back-reference.
        if (linkedJob != null)
        {
            linkedJob.BookingId = booking.Id;
            await repository.SaveChangesAsync(ct);
        }

        return booking;
    }

    /// <summary>
    /// Full update of a booking -- zone, customer, vehicle, title, times and notes.
    ///
    /// Until this existed a booking was immutable once created: the only way to change a
    /// time was cancel-and-recreate, and cancelling CLOSES the linked job. So the most
    /// routine event in a workshop (a job slipping a day) destroyed work.
    ///
    /// Shares conflict checking and the job cascade with MoveBookingAsync so the two
    /// cannot drift apart.
    /// </summary>
    public async Task<Booking> UpdateBookingAsync(Guid id, UpdateBookingRequest request, CancellationToken ct)
    {
        var booking = await repository.FindAsync(id, ct)
            ?? throw new NotFoundException("Booking not found");

        if (booking.Status == BookingStatus.Cancelled)
            throw new ConflictException("This booking was cancelled and can no longer be edited");

        // UpdateBookingRequest has no FluentValidation validator, so these are checked by
        // hand -- finding 13 in docs/review-findings.md.
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ValidationException([new ValidationFailure(nameof(request.Title), "Title is required")]);

        if (request.StartUtc >= request.EndUtc)
            throw new ValidationException([new ValidationFailure(nameof(request.StartUtc), "Start must be before end")]);

        var (zone, _, _) = await ResolveBookingTargetsAsync(
            request.ZoneId, request.CustomerId, request.VehicleId, ct);

        await EnsureSlotIsFreeAsync(request.ZoneId, request.StartUtc, request.EndUtc, zone.Capacity, id, ct);

        booking.ZoneId = request.ZoneId;
        booking.CustomerId = request.CustomerId;
        booking.VehicleId = request.VehicleId;
        booking.Title = request.Title.Trim();
        booking.StartUtc = request.StartUtc;
        booking.EndUtc = request.EndUtc;
        booking.Notes = request.Notes;

        await CascadeToJobAsync(booking, request.ZoneId, request.StartUtc, request.EndUtc, ct);
        await repository.SaveChangesAsync(ct);

        return booking;
    }

    /// <summary>
    /// Moves a booking to Completed or NoShow.
    ///
    /// BookingStatus has four values and the UI styles all four, but only Confirmed (on
    /// create) and Cancelled (on delete) were ever reachable -- the other two were
    /// decorative. Cancelling still goes through DELETE, which also closes the job.
    /// </summary>
    public async Task<Booking> UpdateBookingStatusAsync(Guid id, UpdateBookingStatusRequest request, CancellationToken ct)
    {
        var booking = await repository.FindAsync(id, ct)
            ?? throw new NotFoundException("Booking not found");

        if (!Enum.TryParse<BookingStatus>(request.Status, true, out var status))
            throw new ValidationException([new ValidationFailure(
                nameof(request.Status), $"'{request.Status}' is not a valid booking status")]);

        // Cancelling has side effects on the linked job, so it stays on DELETE.
        if (status == BookingStatus.Cancelled)
            throw new ConflictException("Use DELETE to cancel a booking so the linked job is handled");

        booking.Status = status;
        await repository.SaveChangesAsync(ct);

        return booking;
    }

    public async Task<Booking> MoveBookingAsync(Guid id, MoveBookingRequest request, CancellationToken ct)
    {
        var booking = await repository.FindAsync(id, ct)
            ?? throw new NotFoundException("Booking not found");

        var zone = await repository.FindZoneAsync(request.ZoneId, ct)
            ?? throw new NotFoundException("Zone not found");

        if (request.StartUtc >= request.EndUtc)
            throw new ValidationException([new ValidationFailure(
                nameof(request.StartUtc), "Start must be before end")]);

        await EnsureSlotIsFreeAsync(request.ZoneId, request.StartUtc, request.EndUtc, zone.Capacity, id, ct);

        booking.ZoneId = request.ZoneId;
        booking.StartUtc = request.StartUtc;
        booking.EndUtc = request.EndUtc;

        await CascadeToJobAsync(booking, request.ZoneId, request.StartUtc, request.EndUtc, ct);
        await repository.SaveChangesAsync(ct);

        return booking;
    }

    public async Task DeleteBookingAsync(Guid id, CancellationToken ct)
    {
        var booking = await repository.FindAsync(id, ct)
            ?? throw new NotFoundException("Booking not found");

        booking.Status = BookingStatus.Cancelled;

        // Cancelling closes the linked job unless it has already reached a finished state.
        if (booking.JobId.HasValue)
        {
            var job = await repository.FindJobAsync(booking.JobId.Value, ct);
            if (job != null && job.Status != JobStatus.Closed
                            && job.Status != JobStatus.Invoiced
                            && job.Status != JobStatus.Completed)
            {
                job.Status = JobStatus.Closed;
            }
        }

        await repository.SaveChangesAsync(ct);
    }

    // Keeps a linked job schedule in step with its booking.
    //
    // Plain comment, not XML doc: the .NET 10 preview OpenAPI XML-comment source generator
    // emits System.Void for a Task-returning method carrying a summary and fails CS0673.
    private async Task CascadeToJobAsync(Booking booking, Guid zoneId, DateTime startUtc, DateTime endUtc, CancellationToken ct)
    {
        if (!booking.JobId.HasValue) return;

        var job = await repository.FindJobAsync(booking.JobId.Value, ct);
        if (job is null) return;

        job.AssignedZoneId = zoneId;
        job.ScheduledStartUtc = startUtc;
        job.ScheduledEndUtc = endUtc;
    }

    /// <summary>
    /// Loads the zone, customer and vehicle a booking points at, each through its
    /// tenant-filtered repository method so another business's row is simply not found.
    ///
    /// Extracted because create, update and move each repeated it, and one of the three
    /// omitting a check is exactly how the job endpoints ended up accepting a foreign zone
    /// (finding 2 in docs/review-findings.md). One resolver, one place to get it wrong.
    /// </summary>
    private async Task<(Zone Zone, Customer Customer, Vehicle Vehicle)> ResolveBookingTargetsAsync(
        Guid zoneId, Guid customerId, Guid vehicleId, CancellationToken ct)
    {
        var zone = await repository.FindZoneAsync(zoneId, ct)
            ?? throw new NotFoundException("Zone not found");
        var customer = await repository.FindCustomerAsync(customerId, ct)
            ?? throw new NotFoundException("Customer not found");
        var vehicle = await repository.FindVehicleAsync(vehicleId, ct)
            ?? throw new NotFoundException("Vehicle not found");

        return (zone, customer, vehicle);
    }

    // Throws if the slot is already at capacity. Reads as an assertion rather than as a
    // list the caller must remember to inspect -- forgetting to check a returned list
    // would silently permit a double booking.
    private async Task EnsureSlotIsFreeAsync(
        Guid zoneId, DateTime startUtc, DateTime endUtc, int capacity, Guid? excludeBookingId, CancellationToken ct)
    {
        var overlapping = await repository.GetOverlappingBookingIdsAsync(
            zoneId, startUtc, endUtc, excludeBookingId, ct);

        // Capacity-aware: a bay that takes two cars only conflicts once both slots are full.
        if (overlapping.Count < capacity) return;

        throw new ConflictException("Booking conflicts detected", new { conflictingBookingIds = overlapping });
    }
}
