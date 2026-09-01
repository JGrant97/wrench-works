using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Features.Common;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Jobs;

public class JobService(AppDbContext db, CurrentUserService currentUser) : IJobService
{
    // Which statuses a job may move to from each status. Static because it is a fixed
    // property of the domain, not per-request state -- it was previously rebuilt as a new
    // Dictionary on every single status change.
    private static readonly Dictionary<JobStatus, JobStatus[]> ValidTransitions = new()
    {
        [JobStatus.Draft] = [JobStatus.Scheduled, JobStatus.Closed],
        [JobStatus.Scheduled] = [JobStatus.InProgress, JobStatus.Closed],
        [JobStatus.InProgress] = [JobStatus.WaitingParts, JobStatus.Completed, JobStatus.Closed],
        [JobStatus.WaitingParts] = [JobStatus.InProgress, JobStatus.Closed],
        [JobStatus.Completed] = [JobStatus.Invoiced, JobStatus.Closed],
        [JobStatus.Invoiced] = [JobStatus.Closed],
        [JobStatus.Closed] = []
    };

    public async Task<PagedResult<JobListItemDto>> ListAsync(int page = 1, int pageSize = 25, string? status = null, string? search = null, bool includeArchived = false, CancellationToken ct = default)
    {
        var query = db.Jobs
            .Include(j => j.Customer)
            .Include(j => j.Vehicle)
            .Include(j => j.AssignedZone)
            .AsQueryable();

        if (!includeArchived) query = query.Where(j => j.ArchivedAtUtc == null);

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

        return new PagedResult<JobListItemDto>(items, total, page, pageSize);
    }

    // <summary>
    // A job may be deleted outright only while it is still a Draft — nothing has been
    // worked, billed or booked against it, so there is no history to lose. Once it has
    // been scheduled or beyond it is archived instead: labor and part lines are its own
    // children and would cascade away with it, taking the record of what the customer
    // was charged and why stock left the shelf.
    // </summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var job = await db.Jobs.FindAsync([id], ct)
            ?? throw new NotFoundException("Job not found");

        if (job.Status != JobStatus.Draft)
            throw new ConflictException(
                $"A {job.Status} job cannot be deleted because it carries billing and stock history. " +
                "Archive it instead — it will be hidden from lists while its history stays intact.");

        Archiving.EnsureDeletable("job",
            new Dependent("labour lines", await db.JobLaborLines.CountAsync(l => l.JobId == id, ct)),
            new Dependent("part lines", await db.JobPartLines.CountAsync(p => p.JobId == id, ct)),
            new Dependent("bookings", await db.Bookings.CountAsync(b => b.JobId == id, ct)));

        db.Jobs.Remove(job);
        await db.SaveChangesAsync(ct);
        return;
    }

    public async Task<ArchiveResultDto> ArchiveAsync(Guid id, CancellationToken ct)
    {
        var job = await db.Jobs.FindAsync([id], ct) ?? throw new NotFoundException("Job not found");
        var result = Archiving.Archive(job, id);
        await db.SaveChangesAsync(ct);
        return result;
    }

    public async Task<ArchiveResultDto> UnarchiveAsync(Guid id, CancellationToken ct)
    {
        var job = await db.Jobs.FindAsync([id], ct) ?? throw new NotFoundException("Job not found");
        var result = Archiving.Unarchive(job, id);
        await db.SaveChangesAsync(ct);
        return result;
    }

    public async Task<JobDetailDto> GetAsync(Guid id, CancellationToken ct)
    {
        var job = await db.Jobs
            .Include(j => j.Customer)
            .Include(j => j.Vehicle)
            .Include(j => j.AssignedZone)
            .Include(j => j.LaborLines)
            .Include(j => j.PartLines).ThenInclude(pl => pl.InventoryItem)
            .FirstOrDefaultAsync(j => j.Id == id, ct)
            ?? throw new NotFoundException("Job not found");

        var laborLines = job.LaborLines.Select(l => new LaborLineDto(l.Id, l.Description, l.Hours, l.Rate, l.Hours * l.Rate, l.TaxRatePercent, l.TaxAmount));
        var partLines = job.PartLines.Select(p => new PartLineDto(p.Id, p.InventoryItemId, p.InventoryItem.Name, p.InventoryItem.Sku, p.Quantity, p.UnitPrice, p.Quantity * p.UnitPrice, p.TaxRatePercent, p.TaxAmount));
        var laborTotal = job.LaborLines.Sum(l => l.Hours * l.Rate);
        var partsTotal = job.PartLines.Sum(p => p.Quantity * p.UnitPrice);

        var business = await db.Businesses.FindAsync([job.BusinessId], ct);
        var pricesIncludeTax = business?.PricesIncludeTax ?? false;

        // Totals come from the SNAPSHOTTED amounts, never recomputed from current rates —
        // a rate change must not silently rewrite what a past job was charged.
        var taxTotal = job.LaborLines.Sum(l => l.TaxAmount) + job.PartLines.Sum(p => p.TaxAmount);
        var lineTotal = laborTotal + partsTotal;

        // With inclusive pricing the line amounts already contain the tax, so the net is
        // what is left after removing it. With exclusive pricing they are the net already.
        var subTotal = pricesIncludeTax ? lineTotal - taxTotal : lineTotal;
        var grandTotal = pricesIncludeTax ? lineTotal : lineTotal + taxTotal;

        var breakdown = await BuildTaxBreakdownAsync(db, job, ct);

        return new JobDetailDto(
            job.Id, job.Title, job.Status.ToString(), job.Priority.ToString(),
            job.CustomerId, job.Customer.Name,
            job.VehicleId,
            $"{job.Vehicle.DisplayName} {job.Vehicle.Registration}".Trim(),
            job.AssignedZoneId, job.AssignedZone?.Name,
            job.InternalNotes, job.CustomerNotes,
            job.ScheduledStartUtc, job.ScheduledEndUtc,
            laborLines, partLines,
            laborTotal, partsTotal, grandTotal,
            subTotal, taxTotal,
            business?.TaxLabel ?? "Tax",
            pricesIncludeTax,
            job.Customer.IsTaxExempt,
            breakdown,
            job.CreatedAtUtc);
    }

    public async Task<JobCreatedDto> CreateAsync(CreateJobRequest request, CancellationToken ct)
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

        return new JobCreatedDto(job.Id, job.Status);
    }

    public async Task<JobSummaryDto> UpdateJobAsync(Guid id, UpdateJobRequest request, CancellationToken ct)
    {
        var job = await db.Jobs.FindAsync([id], ct)
            ?? throw new NotFoundException("Job not found");

        // Prevent editing closed/completed/invoiced jobs
        if (job.Status is JobStatus.Closed or JobStatus.Completed or JobStatus.Invoiced)
            throw new ValidationException($"Cannot edit a {job.Status} job");

        if (!Enum.TryParse<JobPriority>(request.Priority, true, out var priority))
            throw new ValidationException("Invalid priority");

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
            var booking = await FindLinkedBookingAsync(db, job, ct);

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
        return new JobSummaryDto(job.Id, job.Title, job.Status.ToString(), job.Priority.ToString());
    }

    public async Task<JobStatusDto> UpdateStatusAsync(Guid id, UpdateJobStatusRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<JobStatus>(request.Status, true, out var newStatus))
            throw new ValidationException("Invalid status");

        var job = await db.Jobs.FindAsync([id], ct)
            ?? throw new NotFoundException("Job not found");

        if (!ValidTransitions.TryGetValue(job.Status, out var allowed) || !allowed.Contains(newStatus))
            throw new ValidationException($"Cannot transition from {job.Status} to {newStatus}");

        var booking = await FindLinkedBookingAsync(db, job, ct);

        job.Status = newStatus;
        SyncBookingToJobStatus(db, job, booking, newStatus, currentUser.UserId);
        await db.SaveChangesAsync(ct);

        // Second save: the audit row is written only once the change it records has
        // actually committed, so a failed status change cannot leave a log saying it
        // succeeded. (Not atomic — see finding 8 in docs/review-findings.md.)
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

        return new JobStatusDto(job.Id, job.Status.ToString());
    }

    public async Task<PartLineDto> AddPartAsync(Guid id, AddPartToJobRequest request, CancellationToken ct)
    {
        var job = await db.Jobs.FindAsync([id], ct)
            ?? throw new NotFoundException("Job not found");

        if (job.Status is JobStatus.Closed or JobStatus.Completed or JobStatus.Invoiced)
            throw new ValidationException($"Cannot modify a {job.Status} job");

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

        var business = await db.Businesses.FindAsync([job.BusinessId], ct);
        // A consumable is taxed as a consumable, not as a part — the whole reason the flag
        // exists. See docs/tax.md.
        var category = item.IsConsumable ? TaxCategory.Consumables : TaxCategory.Parts;
        var (rateId, percent) = await ResolveTaxRateAsync(db, job.CustomerId, category, ct);
        var taxed = TaxCalculator.CalculateLine(
            new TaxableLine(partLine.Quantity * partLine.UnitPrice, percent),
            business?.PricesIncludeTax ?? false);

        partLine.TaxRateId = rateId;
        partLine.TaxRatePercent = percent;
        partLine.TaxAmount = taxed.Tax;

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

        return new PartLineDto(partLine.Id, partLine.InventoryItemId, item.Name, item.Sku, partLine.Quantity, partLine.UnitPrice, partLine.Quantity * partLine.UnitPrice, partLine.TaxRatePercent, partLine.TaxAmount);
    }

    public async Task<LaborLineDto> AddLaborAsync(Guid id, AddLaborLineRequest request, CancellationToken ct)
    {
        var job = await db.Jobs.FindAsync([id], ct) ?? throw new NotFoundException("Job not found");

        if (job.Status is JobStatus.Closed or JobStatus.Completed or JobStatus.Invoiced)
            throw new ValidationException($"Cannot modify a {job.Status} job");

        var business = await db.Businesses.FindAsync([job.BusinessId], ct);
        var (rateId, percent) = await ResolveTaxRateAsync(db, job.CustomerId, TaxCategory.Labour, ct);
        var lineTotal = request.Hours * request.Rate;
        var taxed = TaxCalculator.CalculateLine(
            new TaxableLine(lineTotal, percent), business?.PricesIncludeTax ?? false);

        var line = new JobLaborLine
        {
            JobId = id,
            Description = request.Description.Trim(),
            Hours = request.Hours,
            Rate = request.Rate,
            TaxRateId = rateId,
            TaxRatePercent = percent,
            TaxAmount = taxed.Tax
        };
        db.JobLaborLines.Add(line);
        await db.SaveChangesAsync(ct);

        return new LaborLineDto(line.Id, line.Description, line.Hours, line.Rate, lineTotal,
                line.TaxRatePercent, line.TaxAmount);
    }

    public async Task RemovePartAsync(Guid id, Guid lineId, CancellationToken ct)
    {
        var line = await db.JobPartLines
            .Include(l => l.InventoryItem)
            .FirstOrDefaultAsync(l => l.Id == lineId && l.JobId == id, ct)
            ?? throw new NotFoundException("Part line not found");

        var job = await db.Jobs.FindAsync([id], ct)!;

        if (job!.Status is JobStatus.Closed or JobStatus.Completed or JobStatus.Invoiced)
            throw new ValidationException($"Cannot modify a {job.Status} job");

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
        return;
    }

    public async Task RemoveLaborAsync(Guid id, Guid lineId, CancellationToken ct)
    {
        var job = await db.Jobs.FindAsync([id], ct) ?? throw new NotFoundException("Job not found");

        if (job.Status is JobStatus.Closed or JobStatus.Completed or JobStatus.Invoiced)
            throw new ValidationException($"Cannot modify a {job.Status} job");

        var line = await db.JobLaborLines.FirstOrDefaultAsync(l => l.Id == lineId && l.JobId == id, ct)
            ?? throw new NotFoundException("Labor line not found");
        db.JobLaborLines.Remove(line);
        await db.SaveChangesAsync(ct);
        return;
    }

    // Groups the job's tax by the rate each line was charged at, so an invoice can show
    // "VAT 20% — £42.00" rather than one undifferentiated number. Where a rate carries
    // jurisdiction components they ride along for display; the AMOUNT always comes from the
    // line snapshots, never from re-summing component percentages, which would drift from
    // what the customer was actually charged.
    //
    // Plain // rather than ///: Task-returning method with a generic return, which the
    // .NET 10 preview OpenAPI comment generator mishandles. See CLAUDE.md.
    private static async Task<List<TaxLineDto>> BuildTaxBreakdownAsync(AppDbContext db, Job job, CancellationToken ct)
    {
        var byRate = job.LaborLines
            .Select(l => new { l.TaxRateId, l.TaxRatePercent, l.TaxAmount })
            .Concat(job.PartLines.Select(p => new { p.TaxRateId, p.TaxRatePercent, p.TaxAmount }))
            .Where(x => x.TaxAmount != 0m)
            .GroupBy(x => new { x.TaxRateId, x.TaxRatePercent })
            .ToList();

        if (byRate.Count == 0) return [];

        var rateIds = byRate.Select(g => g.Key.TaxRateId).OfType<Guid>().ToList();

        // Deliberately does not filter on ArchivedAtUtc: a rate that has since been retired
        // must still resolve, or a historical job loses the name of the tax it was charged.
        var rates = await db.TaxRates
            .Include(r => r.Components)
            .Where(r => rateIds.Contains(r.Id))
            .ToListAsync(ct);

        return byRate.Select(g =>
        {
            var rate = rates.FirstOrDefault(r => r.Id == g.Key.TaxRateId);
            var components = rate?.Components
                .OrderBy(c => c.SortOrder)
                .Select(c => new TaxComponentLineDto(c.Name, c.Rate))
                ?? [];

            return new TaxLineDto(
                rate?.Name ?? "Tax",
                g.Key.TaxRatePercent,
                g.Sum(x => x.TaxAmount),
                components);
        }).ToList();
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

    /// <summary>
    /// Jobs and bookings cross-link with two independent nullable FKs and nothing keeps
    /// them consistent, so both directions have to be checked. Extracted because the same
    /// lookup appears in UpdateJobAsync.
    /// </summary>
    private static async Task<Booking?> FindLinkedBookingAsync(AppDbContext db, Job job, CancellationToken ct)
=> job.BookingId.HasValue
            ? await db.Bookings.FindAsync([job.BookingId.Value], ct)
            : await db.Bookings.FirstOrDefaultAsync(b => b.JobId == job.Id, ct);

    /// <summary>
    /// Mirrors a job's new status onto its calendar booking. Three cases, and the third is
    /// the one worth reading twice: moving back to an active status either revives a
    /// cancelled booking or creates the booking that never existed, which is the only path
    /// in the codebase that writes a Booking outside the Calendar slice.
    ///
    /// Synchronous, and takes the already-loaded booking, so the decision table stays
    /// readable next to the I/O rather than interleaved with it.
    /// </summary>
    private static void SyncBookingToJobStatus(AppDbContext db, Job job, Booking? booking, JobStatus newStatus, Guid? userId)
    {
        switch (newStatus)
        {
            case JobStatus.Closed:
                if (booking is { Status: not BookingStatus.Cancelled })
                    booking.Status = BookingStatus.Cancelled;
                break;

            case JobStatus.Completed:
            case JobStatus.Invoiced:
                if (booking is { Status: BookingStatus.Confirmed })
                    booking.Status = BookingStatus.Completed;
                break;

            case JobStatus.Scheduled:
            case JobStatus.InProgress:
                ReviveOrCreateBooking(db, job, booking, userId);
                break;
        }
    }

    private static void ReviveOrCreateBooking(AppDbContext db, Job job, Booking? booking, Guid? userId)
    {
        var isScheduled = job.ScheduledStartUtc.HasValue && job.ScheduledEndUtc.HasValue;

        if (booking is { Status: BookingStatus.Cancelled })
        {
            booking.Status = BookingStatus.Confirmed;
            if (isScheduled)
            {
                booking.StartUtc = job.ScheduledStartUtc!.Value;
                booking.EndUtc = job.ScheduledEndUtc!.Value;
            }
            return;
        }

        // A job can only acquire a booking if it has somewhere and sometime to be.
        if (booking != null || !isScheduled || !job.AssignedZoneId.HasValue) return;

        var created = new Booking
        {
            BusinessId = job.BusinessId,
            ZoneId = job.AssignedZoneId.Value,
            CustomerId = job.CustomerId,
            VehicleId = job.VehicleId,
            Title = job.Title,
            StartUtc = job.ScheduledStartUtc!.Value,
            EndUtc = job.ScheduledEndUtc!.Value,
            Status = BookingStatus.Confirmed,
            CreatedByUserId = userId
        };

        db.Bookings.Add(created);
        created.JobId = job.Id;
        job.BookingId = created.Id;
    }

    /// <summary>
    /// Picks the rate a new line is raised at, and snapshots it.
    ///
    /// Returns nothing when the customer is exempt or no rate is mapped to the category —
    /// a US shop with no labour mapping is stating that labour is not taxable there, which
    /// is a real answer rather than a missing setting.
    /// </summary>
    private static async Task<(Guid? RateId, decimal Percent)> ResolveTaxRateAsync(AppDbContext db, Guid customerId, TaxCategory category, CancellationToken ct)
    {
        var customer = await db.Customers.FindAsync([customerId], ct);
        if (customer is { IsTaxExempt: true }) return (null, 0m);

        var mapping = await db.TaxRateCategories
            .Include(m => m.TaxRate)
            .Where(m => m.Category == category && m.TaxRate.ArchivedAtUtc == null)
            .FirstOrDefaultAsync(ct);

        return mapping is null ? (null, 0m) : (mapping.TaxRateId, mapping.TaxRate.Rate);
    }
}
