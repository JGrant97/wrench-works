using FluentValidation;
using Microsoft.EntityFrameworkCore;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Features.Common;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Jobs;

// DTOs
public record CreateJobRequest(Guid CustomerId, Guid VehicleId, string Title, string? InternalNotes, string? CustomerNotes, string Priority, Guid? ZoneId, DateTime? ScheduledStartUtc, DateTime? ScheduledEndUtc);
public record UpdateJobRequest(string Title, string? InternalNotes, string? CustomerNotes, string Priority, Guid? ZoneId, DateTime? ScheduledStartUtc, DateTime? ScheduledEndUtc);
public record UpdateJobStatusRequest(string Status);
public record AddPartToJobRequest(Guid InventoryItemId, decimal Quantity, decimal? UnitPriceOverride);
public record AddLaborLineRequest(string Description, decimal Hours, decimal Rate);

public record JobListItemDto(Guid Id, string Title, string Status, string Priority, string CustomerName, string? VehicleDisplay, string? ZoneName, DateTime? ScheduledStartUtc, decimal LaborTotal, decimal PartsTotal, DateTime CreatedAtUtc);
public record JobDetailDto(
    Guid Id, string Title, string Status, string Priority,
    Guid CustomerId, string CustomerName,
    Guid VehicleId, string? VehicleDisplay,
    Guid? ZoneId, string? ZoneName,
    string? InternalNotes, string? CustomerNotes,
    DateTime? ScheduledStartUtc, DateTime? ScheduledEndUtc,
    IEnumerable<LaborLineDto> LaborLines,
    IEnumerable<PartLineDto> PartLines,
    decimal LaborTotal, decimal PartsTotal, decimal GrandTotal,
    DateTime CreatedAtUtc);
public record LaborLineDto(Guid Id, string Description, decimal Hours, decimal Rate, decimal Total);
public record PartLineDto(Guid Id, Guid InventoryItemId, string ItemName, string? Sku, decimal Quantity, decimal UnitPrice, decimal Total);

// Validators
public class CreateJobValidator : AbstractValidator<CreateJobRequest>
{
    public CreateJobValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.VehicleId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Priority).Must(p => Enum.TryParse<JobPriority>(p, true, out _)).WithMessage("Invalid priority");
    }
}

public static class JobEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/jobs").WithTags("Jobs").RequireAuthorization();

        group.MapGet("/", ListAsync).RequireAuthorization("jobs.view").Produces<PagedResult<JobListItemDto>>();
        group.MapGet("/{id:guid}", GetAsync).RequireAuthorization("jobs.view").Produces<JobDetailDto>();
        group.MapPost("/", CreateAsync).RequireAuthorization("jobs.create");
        group.MapPut("/{id:guid}", UpdateJobAsync).RequireAuthorization("jobs.edit");
        group.MapPatch("/{id:guid}/status", UpdateStatusAsync).RequireAuthorization("jobs.edit");
        group.MapPost("/{id:guid}/parts", AddPartAsync).RequireAuthorization("jobs.edit");
        group.MapPost("/{id:guid}/labor", AddLaborAsync).RequireAuthorization("jobs.edit");
        group.MapDelete("/{id:guid}/parts/{lineId:guid}", RemovePartAsync).RequireAuthorization("jobs.edit");
        group.MapDelete("/{id:guid}/labor/{lineId:guid}", RemoveLaborAsync).RequireAuthorization("jobs.edit");
    }

    private static async Task<IResult> ListAsync(
        AppDbContext db,
        int page = 1, int pageSize = 25,
        string? status = null, string? search = null,
        CancellationToken ct = default)
    {
        var query = db.Jobs
            .Include(j => j.Customer)
            .Include(j => j.Vehicle)
            .Include(j => j.AssignedZone)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<JobStatus>(status, true, out var st))
            query = query.Where(j => j.Status == st);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(j => j.Title.ToLower().Contains(s) || j.Customer.Name.ToLower().Contains(s));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(j => j.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(j => new JobListItemDto(
                j.Id, j.Title, j.Status.ToString(), j.Priority.ToString(),
                j.Customer.Name,
                (j.Vehicle.DisplayName ?? "") + (j.Vehicle.Registration != null ? " " + j.Vehicle.Registration : ""),
                j.AssignedZone != null ? j.AssignedZone.Name : null,
                j.ScheduledStartUtc,
                j.LaborLines.Sum(l => l.Hours * l.Rate),
                j.PartLines.Sum(p => p.Quantity * p.UnitPrice),
                j.CreatedAtUtc))
            .ToListAsync(ct);

        return Results.Ok(new PagedResult<JobListItemDto>(items, total, page, pageSize));
    }

    private static async Task<IResult> GetAsync(Guid id, AppDbContext db, CancellationToken ct)
    {
        var job = await db.Jobs
            .Include(j => j.Customer)
            .Include(j => j.Vehicle)
            .Include(j => j.AssignedZone)
            .Include(j => j.LaborLines)
            .Include(j => j.PartLines).ThenInclude(pl => pl.InventoryItem)
            .FirstOrDefaultAsync(j => j.Id == id, ct)
            ?? throw new NotFoundException("Job not found");

        var laborLines = job.LaborLines.Select(l => new LaborLineDto(l.Id, l.Description, l.Hours, l.Rate, l.Hours * l.Rate));
        var partLines = job.PartLines.Select(p => new PartLineDto(p.Id, p.InventoryItemId, p.InventoryItem.Name, p.InventoryItem.Sku, p.Quantity, p.UnitPrice, p.Quantity * p.UnitPrice));
        var laborTotal = job.LaborLines.Sum(l => l.Hours * l.Rate);
        var partsTotal = job.PartLines.Sum(p => p.Quantity * p.UnitPrice);

        return Results.Ok(new JobDetailDto(
            job.Id, job.Title, job.Status.ToString(), job.Priority.ToString(),
            job.CustomerId, job.Customer.Name,
            job.VehicleId,
            $"{job.Vehicle.DisplayName} {job.Vehicle.Registration}".Trim(),
            job.AssignedZoneId, job.AssignedZone?.Name,
            job.InternalNotes, job.CustomerNotes,
            job.ScheduledStartUtc, job.ScheduledEndUtc,
            laborLines, partLines,
            laborTotal, partsTotal, laborTotal + partsTotal,
            job.CreatedAtUtc));
    }

    private static async Task<IResult> CreateAsync(
        CreateJobRequest request,
        AppDbContext db,
        CurrentUserService currentUser,
        CancellationToken ct)
    {
        await new CreateJobValidator().ValidateAndThrowAsync(request, ct);

        var businessId = currentUser.RequireBusinessId();
        _ = await db.Customers.FindAsync([request.CustomerId], ct) ?? throw new NotFoundException("Customer not found");
        _ = await db.Vehicles.FindAsync([request.VehicleId], ct) ?? throw new NotFoundException("Vehicle not found");
        await EnsureZoneIsOursAsync(db, request.ZoneId, ct);

        var job = new Job
        {
            BusinessId = businessId,
            CustomerId = request.CustomerId,
            VehicleId = request.VehicleId,
            Title = request.Title.Trim(),
            InternalNotes = request.InternalNotes,
            CustomerNotes = request.CustomerNotes,
            Priority = Enum.Parse<JobPriority>(request.Priority, true),
            AssignedZoneId = request.ZoneId,
            ScheduledStartUtc = request.ScheduledStartUtc,
            ScheduledEndUtc = request.ScheduledEndUtc,
            Status = request.ScheduledStartUtc.HasValue ? JobStatus.Scheduled : JobStatus.Draft,
            CreatedByUserId = currentUser.UserId
        };
        db.Jobs.Add(job);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/jobs/{job.Id}", new { job.Id, job.Status });
    }

    // Not a formatting nicety — an omitted zone check crossed tenants and then broke the
    // calendar. CreateAsync and UpdateJobAsync validated CustomerId and VehicleId through
    // the tenant-filtered DbSet but assigned AssignedZoneId with no lookup at all, so
    // another business's zone GUID was accepted (the FK is satisfied at the database, and
    // tenancy is never checked there). UpdateStatusAsync then auto-created a Booking on
    // that foreign zone, and GetBookingsAsync projects b.Zone.Name unconditionally — the
    // zone is filtered out for this tenant, so the projection dereferenced null and the
    // whole calendar list 500'd. Reading through db.Zones applies the global query filter,
    // so a foreign zone simply is not found. See docs/review-findings.md finding 2.
    private static async Task EnsureZoneIsOursAsync(AppDbContext db, Guid? zoneId, CancellationToken ct)
    {
        if (!zoneId.HasValue) return;

        var exists = await db.Zones.AnyAsync(z => z.Id == zoneId.Value, ct);
        if (!exists) throw new NotFoundException("Zone not found");
    }

    private static async Task<IResult> UpdateJobAsync(
        Guid id,
        UpdateJobRequest request,
        AppDbContext db,
        CancellationToken ct)
    {
        var job = await db.Jobs.FindAsync([id], ct)
            ?? throw new NotFoundException("Job not found");

        // Prevent editing closed/completed/invoiced jobs
        if (job.Status is JobStatus.Closed or JobStatus.Completed or JobStatus.Invoiced)
            return Results.BadRequest(new { code = "validation_error", message = $"Cannot edit a {job.Status} job" });

        if (!Enum.TryParse<JobPriority>(request.Priority, true, out var priority))
            return Results.BadRequest(new { code = "validation_error", message = "Invalid priority" });

        job.Title = request.Title.Trim();
        job.InternalNotes = request.InternalNotes;
        job.CustomerNotes = request.CustomerNotes;
        job.Priority = priority;
        await EnsureZoneIsOursAsync(db, request.ZoneId, ct);
        job.AssignedZoneId = request.ZoneId;
        job.ScheduledStartUtc = request.ScheduledStartUtc;
        job.ScheduledEndUtc = request.ScheduledEndUtc;

        // Sync linked booking if the schedule changed
        if (request.ScheduledStartUtc.HasValue && request.ScheduledEndUtc.HasValue)
        {
            // Find the linked booking — check both FK directions for robustness
            var booking = job.BookingId.HasValue
                ? await db.Bookings.FindAsync([job.BookingId.Value], ct)
                : await db.Bookings.FirstOrDefaultAsync(b => b.JobId == id, ct);

            if (booking != null)
            {
                booking.StartUtc = request.ScheduledStartUtc.Value;
                booking.EndUtc = request.ScheduledEndUtc.Value;
                booking.Title = request.Title.Trim();

                if (request.ZoneId.HasValue)
                    booking.ZoneId = request.ZoneId.Value;
            }
        }

        await db.SaveChangesAsync(ct);
        return Results.Ok(new { job.Id, job.Title, Status = job.Status.ToString(), Priority = job.Priority.ToString() });
    }

    private static async Task<IResult> UpdateStatusAsync(
        Guid id,
        UpdateJobStatusRequest request,
        AppDbContext db,
        CurrentUserService currentUser,
        CancellationToken ct)
    {
        if (!Enum.TryParse<JobStatus>(request.Status, true, out var newStatus))
            return Results.BadRequest(new { code = "validation_error", message = "Invalid status" });

        var job = await db.Jobs.FindAsync([id], ct)
            ?? throw new NotFoundException("Job not found");

        // Basic status transition validation
        var validTransitions = new Dictionary<JobStatus, JobStatus[]>
        {
            [JobStatus.Draft] = [JobStatus.Scheduled, JobStatus.Closed],
            [JobStatus.Scheduled] = [JobStatus.InProgress, JobStatus.Closed],
            [JobStatus.InProgress] = [JobStatus.WaitingParts, JobStatus.Completed, JobStatus.Closed],
            [JobStatus.WaitingParts] = [JobStatus.InProgress, JobStatus.Closed],
            [JobStatus.Completed] = [JobStatus.Invoiced, JobStatus.Closed],
            [JobStatus.Invoiced] = [JobStatus.Closed],
            [JobStatus.Closed] = []
        };

        if (!validTransitions.TryGetValue(job.Status, out var allowed) || !allowed.Contains(newStatus))
            return Results.BadRequest(new { code = "validation_error", message = $"Cannot transition from {job.Status} to {newStatus}" });

        job.Status = newStatus;

        // Find linked booking (either FK direction)
        var booking = job.BookingId.HasValue
            ? await db.Bookings.FindAsync([job.BookingId.Value], ct)
            : await db.Bookings.FirstOrDefaultAsync(b => b.JobId == id, ct);

        // Sync booking based on new job status
        if (newStatus == JobStatus.Closed)
        {
            // Closing a job → cancel the linked booking
            if (booking != null && booking.Status != BookingStatus.Cancelled)
            {
                booking.Status = BookingStatus.Cancelled;
            }
        }
        else if (newStatus == JobStatus.Completed || newStatus == JobStatus.Invoiced)
        {
            // Completing a job → mark booking completed
            if (booking != null && booking.Status == BookingStatus.Confirmed)
            {
                booking.Status = BookingStatus.Completed;
            }
        }
        else if (newStatus == JobStatus.Scheduled || newStatus == JobStatus.InProgress)
        {
            // Moving to an active status → restore cancelled booking or create one
            if (booking != null && booking.Status == BookingStatus.Cancelled)
            {
                booking.Status = BookingStatus.Confirmed;
                // Sync schedule if the job has dates
                if (job.ScheduledStartUtc.HasValue && job.ScheduledEndUtc.HasValue)
                {
                    booking.StartUtc = job.ScheduledStartUtc.Value;
                    booking.EndUtc = job.ScheduledEndUtc.Value;
                }
            }
            else if (booking == null && job.ScheduledStartUtc.HasValue && job.ScheduledEndUtc.HasValue && job.AssignedZoneId.HasValue)
            {
                // No booking exists but job has schedule + zone — create one
                var newBooking = new Booking
                {
                    BusinessId = job.BusinessId,
                    ZoneId = job.AssignedZoneId.Value,
                    CustomerId = job.CustomerId,
                    VehicleId = job.VehicleId,
                    Title = job.Title,
                    StartUtc = job.ScheduledStartUtc.Value,
                    EndUtc = job.ScheduledEndUtc.Value,
                    Status = BookingStatus.Confirmed,
                    CreatedByUserId = currentUser.UserId
                };
                db.Bookings.Add(newBooking);
                newBooking.JobId = job.Id;
                job.BookingId = newBooking.Id;
            }
        }

        await db.SaveChangesAsync(ct);

        db.AuditLogs.Add(new AuditLog
        {
            BusinessId = job.BusinessId,
            UserId = currentUser.UserId,
            Action = "job.status_changed",
            EntityType = "Job",
            EntityId = job.Id,
            NewValues = $"{{\"status\":\"{newStatus}\"}}"
        });
        await db.SaveChangesAsync(ct);

        return Results.Ok(new { job.Id, Status = job.Status.ToString() });
    }

    private static async Task<IResult> AddPartAsync(
        Guid id,
        AddPartToJobRequest request,
        AppDbContext db,
        CurrentUserService currentUser,
        CancellationToken ct)
    {
        var job = await db.Jobs.FindAsync([id], ct)
            ?? throw new NotFoundException("Job not found");

        if (job.Status is JobStatus.Closed or JobStatus.Completed or JobStatus.Invoiced)
            return Results.BadRequest(new { code = "validation_error", message = $"Cannot modify a {job.Status} job" });

        var item = await db.InventoryItems.FindAsync([request.InventoryItemId], ct)
            ?? throw new NotFoundException("Inventory item not found");

        if (item.StockOnHand < (int)request.Quantity)
            throw new ConflictException($"Insufficient stock. Available: {item.StockOnHand}");

        var unitPrice = request.UnitPriceOverride ?? item.RetailPrice ?? item.UnitCost;

        var partLine = new JobPartLine
        {
            JobId = id,
            InventoryItemId = request.InventoryItemId,
            Quantity = request.Quantity,
            UnitPrice = unitPrice
        };
        db.JobPartLines.Add(partLine);

        // Create stock movement
        var movement = new StockMovement
        {
            BusinessId = job.BusinessId,
            InventoryItemId = request.InventoryItemId,
            QuantityDelta = -(int)request.Quantity,
            Reason = StockMovementReason.JobConsumption,
            JobId = id,
            CreatedByUserId = currentUser.UserId
        };
        db.StockMovements.Add(movement);
        item.StockOnHand -= (int)request.Quantity;

        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/jobs/{id}/parts/{partLine.Id}",
            new PartLineDto(partLine.Id, partLine.InventoryItemId, item.Name, item.Sku, partLine.Quantity, partLine.UnitPrice, partLine.Quantity * partLine.UnitPrice));
    }

    private static async Task<IResult> AddLaborAsync(
        Guid id,
        AddLaborLineRequest request,
        AppDbContext db,
        CancellationToken ct)
    {
        var job = await db.Jobs.FindAsync([id], ct) ?? throw new NotFoundException("Job not found");

        if (job.Status is JobStatus.Closed or JobStatus.Completed or JobStatus.Invoiced)
            return Results.BadRequest(new { code = "validation_error", message = $"Cannot modify a {job.Status} job" });

        var line = new JobLaborLine
        {
            JobId = id,
            Description = request.Description.Trim(),
            Hours = request.Hours,
            Rate = request.Rate
        };
        db.JobLaborLines.Add(line);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/jobs/{id}/labor/{line.Id}",
            new LaborLineDto(line.Id, line.Description, line.Hours, line.Rate, line.Hours * line.Rate));
    }

    private static async Task<IResult> RemovePartAsync(Guid id, Guid lineId, AppDbContext db, CurrentUserService currentUser, CancellationToken ct)
    {
        var line = await db.JobPartLines
            .Include(l => l.InventoryItem)
            .FirstOrDefaultAsync(l => l.Id == lineId && l.JobId == id, ct)
            ?? throw new NotFoundException("Part line not found");

        var job = await db.Jobs.FindAsync([id], ct)!;

        if (job!.Status is JobStatus.Closed or JobStatus.Completed or JobStatus.Invoiced)
            return Results.BadRequest(new { code = "validation_error", message = $"Cannot modify a {job.Status} job" });

        // Return stock
        line.InventoryItem.StockOnHand += (int)line.Quantity;
        db.StockMovements.Add(new StockMovement
        {
            BusinessId = job!.BusinessId,
            InventoryItemId = line.InventoryItemId,
            QuantityDelta = (int)line.Quantity,
            Reason = StockMovementReason.JobReturn,
            JobId = id,
            CreatedByUserId = currentUser.UserId
        });

        db.JobPartLines.Remove(line);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> RemoveLaborAsync(Guid id, Guid lineId, AppDbContext db, CancellationToken ct)
    {
        var job = await db.Jobs.FindAsync([id], ct) ?? throw new NotFoundException("Job not found");

        if (job.Status is JobStatus.Closed or JobStatus.Completed or JobStatus.Invoiced)
            return Results.BadRequest(new { code = "validation_error", message = $"Cannot modify a {job.Status} job" });

        var line = await db.JobLaborLines.FirstOrDefaultAsync(l => l.Id == lineId && l.JobId == id, ct)
            ?? throw new NotFoundException("Labor line not found");
        db.JobLaborLines.Remove(line);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }
}
