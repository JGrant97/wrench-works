using FluentValidation;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Features.Common;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Jobs;

public class JobService(IJobRepository repository, CurrentUserService currentUser) : IJobService
{
    // Which statuses a job may move to from each status. Static because it is a fixed
    // property of the domain, not per-request state. Mirrored by STATUS_TRANSITIONS in the
    // web app jobs/[id]/_lib/job.ts -- change one and you must change the other.
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

    public Task<PagedResult<Job>> ListAsync(int page, int pageSize, string? status,
        string? search, bool includeArchived, CancellationToken ct) =>
        repository.ListAsync(page, pageSize, status, search, includeArchived, ct);

    public async Task<JobDetail> GetAsync(Guid id, CancellationToken ct)
    {
        var job = await repository.FindWithLinesAsync(id, ct)
            ?? throw new NotFoundException("Job not found");

        var business = await repository.FindBusinessAsync(job.BusinessId, ct);
        var pricesIncludeTax = business?.PricesIncludeTax ?? false;

        var laborTotal = job.LaborLines.Sum(l => l.Hours * l.Rate);
        var partsTotal = job.PartLines.Sum(p => p.Quantity * p.UnitPrice);

        // Totals come from the SNAPSHOTTED amounts, never recomputed from current rates --
        // a rate change must not silently rewrite what a past job was charged.
        var taxTotal = job.LaborLines.Sum(l => l.TaxAmount) + job.PartLines.Sum(p => p.TaxAmount);
        var lineTotal = laborTotal + partsTotal;

        // With inclusive pricing the line amounts already contain the tax, so the net is
        // what is left after removing it. With exclusive pricing they are the net already.
        var subTotal = pricesIncludeTax ? lineTotal - taxTotal : lineTotal;
        var grandTotal = pricesIncludeTax ? lineTotal : lineTotal + taxTotal;

        return new JobDetail(
            job,
            new JobTotals(laborTotal, partsTotal, subTotal, taxTotal, grandTotal),
            business?.TaxLabel ?? "Tax",
            pricesIncludeTax,
            await BuildTaxBreakdownAsync(job, ct));
    }

    public async Task<Job> CreateAsync(CreateJobRequest request, CancellationToken ct)
    {
        await new CreateJobValidator().ValidateAndThrowAsync(request, ct);

        var businessId = currentUser.RequireBusinessId();
        _ = await repository.FindCustomerAsync(request.CustomerId, ct)
            ?? throw new NotFoundException("Customer not found");
        _ = await repository.FindVehicleAsync(request.VehicleId, ct)
            ?? throw new NotFoundException("Vehicle not found");
        await EnsureZoneIsOursAsync(request.ZoneId, ct);

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

        repository.AddJob(job);
        await repository.SaveChangesAsync(ct);
        return job;
    }

    public async Task<Job> UpdateJobAsync(Guid id, UpdateJobRequest request, CancellationToken ct)
    {
        var job = await repository.FindAsync(id, ct)
            ?? throw new NotFoundException("Job not found");

        EnsureModifiable(job, "edit");

        if (!Enum.TryParse<JobPriority>(request.Priority, true, out var priority))
            throw new ValidationException("Invalid priority");

        job.Title = request.Title.Trim();
        job.InternalNotes = request.InternalNotes;
        job.CustomerNotes = request.CustomerNotes;
        job.Priority = priority;
        await EnsureZoneIsOursAsync(request.ZoneId, ct);
        job.AssignedZoneId = request.ZoneId;
        job.ScheduledStartUtc = request.ScheduledStartUtc;
        job.ScheduledEndUtc = request.ScheduledEndUtc;

        // Keep the linked booking in step if the schedule changed.
        if (request.ScheduledStartUtc.HasValue && request.ScheduledEndUtc.HasValue)
        {
            var booking = await repository.FindLinkedBookingAsync(job, ct);
            if (booking != null)
            {
                booking.StartUtc = request.ScheduledStartUtc.Value;
                booking.EndUtc = request.ScheduledEndUtc.Value;
                booking.Title = request.Title.Trim();

                if (request.ZoneId.HasValue)
                    booking.ZoneId = request.ZoneId.Value;
            }
        }

        await repository.SaveChangesAsync(ct);
        return job;
    }

    public async Task<Job> UpdateStatusAsync(Guid id, UpdateJobStatusRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<JobStatus>(request.Status, true, out var newStatus))
            throw new ValidationException("Invalid status");

        var job = await repository.FindAsync(id, ct)
            ?? throw new NotFoundException("Job not found");

        if (!ValidTransitions.TryGetValue(job.Status, out var allowed) || !allowed.Contains(newStatus))
            throw new ValidationException($"Cannot transition from {job.Status} to {newStatus}");

        var booking = await repository.FindLinkedBookingAsync(job, ct);

        job.Status = newStatus;
        SyncBookingToJobStatus(job, booking, newStatus);
        await repository.SaveChangesAsync(ct);

        // Second save: the audit row is written only once the change it records has
        // actually committed, so a failed status change cannot leave a log saying it
        // succeeded. Not atomic -- see finding 8 in docs/review-findings.md.
        repository.AddAuditLog(new AuditLog
        {
            BusinessId = job.BusinessId,
            UserId = currentUser.UserId,
            Action = "job.status_changed",
            EntityType = "Job",
            EntityId = job.Id,
            NewValues = $"{{\"status\":\"{newStatus}\"}}"
        });
        await repository.SaveChangesAsync(ct);

        return job;
    }

    public async Task<JobPartLine> AddPartAsync(Guid id, AddPartToJobRequest request, CancellationToken ct)
    {
        var job = await repository.FindAsync(id, ct)
            ?? throw new NotFoundException("Job not found");

        EnsureModifiable(job, "modify");

        var item = await repository.FindInventoryItemAsync(request.InventoryItemId, ct)
            ?? throw new NotFoundException("Inventory item not found");

        if (item.StockOnHand < (int)request.Quantity)
            throw new ConflictException($"Insufficient stock. Available: {item.StockOnHand}");

        var unitPrice = request.UnitPriceOverride ?? item.RetailPrice ?? item.UnitCost;

        var partLine = new JobPartLine
        {
            JobId = id,
            InventoryItemId = request.InventoryItemId,
            Quantity = request.Quantity,
            UnitPrice = unitPrice,
            // Set so the handler can name the part without re-reading it.
            InventoryItem = item
        };

        var business = await repository.FindBusinessAsync(job.BusinessId, ct);

        // A consumable is taxed as a consumable, not as a part -- the whole reason the flag
        // exists. See docs/tax.md.
        var category = item.IsConsumable ? TaxCategory.Consumables : TaxCategory.Parts;
        var (rateId, percent) = await ResolveTaxRateAsync(job.CustomerId, category, ct);
        var taxed = TaxCalculator.CalculateLine(
            new TaxableLine(partLine.Quantity * partLine.UnitPrice, percent),
            business?.PricesIncludeTax ?? false);

        partLine.TaxRateId = rateId;
        partLine.TaxRatePercent = percent;
        partLine.TaxAmount = taxed.Tax;

        repository.AddPartLine(partLine);

        repository.AddStockMovement(new StockMovement
        {
            BusinessId = job.BusinessId,
            InventoryItemId = request.InventoryItemId,
            QuantityDelta = -(int)request.Quantity,
            Reason = StockMovementReason.JobConsumption,
            JobId = id,
            CreatedByUserId = currentUser.UserId
        });
        item.StockOnHand -= (int)request.Quantity;

        await repository.SaveChangesAsync(ct);
        return partLine;
    }

    public async Task<JobLaborLine> AddLaborAsync(Guid id, AddLaborLineRequest request, CancellationToken ct)
    {
        var job = await repository.FindAsync(id, ct)
            ?? throw new NotFoundException("Job not found");

        EnsureModifiable(job, "modify");

        var business = await repository.FindBusinessAsync(job.BusinessId, ct);
        var (rateId, percent) = await ResolveTaxRateAsync(job.CustomerId, TaxCategory.Labour, ct);
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

        repository.AddLaborLine(line);
        await repository.SaveChangesAsync(ct);
        return line;
    }

    // Resolves the parent job FIRST, then the line. JobPartLines has no global query
    // filter, so the tenant check lives entirely in that parent lookup -- and the old
    // order (line first, then a null-forgiving job lookup) turned a foreign job into a
    // NullReferenceException and a bare 500 instead of a 404. Fixed here; it is the
    // RemovePartAsync bug recorded in CLAUDE.md and finding 19 in review-findings.md.
    public async Task RemovePartAsync(Guid id, Guid lineId, CancellationToken ct)
    {
        var job = await repository.FindAsync(id, ct)
            ?? throw new NotFoundException("Job not found");

        EnsureModifiable(job, "modify");

        var line = await repository.FindPartLineAsync(id, lineId, ct)
            ?? throw new NotFoundException("Part line not found");

        // Returning the stock and recording the reverse movement keeps the trail able to
        // reconstruct the level from zero.
        line.InventoryItem.StockOnHand += (int)line.Quantity;
        repository.AddStockMovement(new StockMovement
        {
            BusinessId = job.BusinessId,
            InventoryItemId = line.InventoryItemId,
            QuantityDelta = (int)line.Quantity,
            Reason = StockMovementReason.JobReturn,
            JobId = id,
            CreatedByUserId = currentUser.UserId
        });

        repository.RemovePartLine(line);
        await repository.SaveChangesAsync(ct);
    }

    public async Task RemoveLaborAsync(Guid id, Guid lineId, CancellationToken ct)
    {
        var job = await repository.FindAsync(id, ct)
            ?? throw new NotFoundException("Job not found");

        EnsureModifiable(job, "modify");

        var line = await repository.FindLaborLineAsync(id, lineId, ct)
            ?? throw new NotFoundException("Labor line not found");

        repository.RemoveLaborLine(line);
        await repository.SaveChangesAsync(ct);
    }

    // <summary>
    // A job may be deleted outright only while it is still a Draft -- nothing has been
    // worked, billed or booked against it, so there is no history to lose. Once it has
    // been scheduled or beyond it is archived instead: labor and part lines are its own
    // children and would cascade away with it, taking the record of what the customer was
    // charged and why stock left the shelf.
    // </summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var job = await repository.FindAsync(id, ct)
            ?? throw new NotFoundException("Job not found");

        if (job.Status != JobStatus.Draft)
            throw new ConflictException(
                $"A {job.Status} job cannot be deleted because it carries billing and stock history. " +
                "Archive it instead - it will be hidden from lists while its history stays intact.");

        Archiving.EnsureDeletable("job",
            new Dependent("labour lines", await repository.CountLabourLinesAsync(id, ct)),
            new Dependent("part lines", await repository.CountPartLinesAsync(id, ct)),
            new Dependent("bookings", await repository.CountBookingsAsync(id, ct)));

        repository.RemoveJob(job);
        await repository.SaveChangesAsync(ct);
    }

    public async Task<ArchiveResultDto> ArchiveAsync(Guid id, CancellationToken ct)
    {
        var job = await repository.FindAsync(id, ct) ?? throw new NotFoundException("Job not found");
        var result = Archiving.Archive(job, id);
        await repository.SaveChangesAsync(ct);
        return result;
    }

    public async Task<ArchiveResultDto> UnarchiveAsync(Guid id, CancellationToken ct)
    {
        var job = await repository.FindAsync(id, ct) ?? throw new NotFoundException("Job not found");
        var result = Archiving.Unarchive(job, id);
        await repository.SaveChangesAsync(ct);
        return result;
    }

    private static void EnsureModifiable(Job job, string verb)
    {
        if (job.Status is JobStatus.Closed or JobStatus.Completed or JobStatus.Invoiced)
            throw new ValidationException($"Cannot {verb} a {job.Status} job");
    }

    // Not a formatting nicety -- an omitted zone check crossed tenants and then broke the
    // calendar. Create and update validated CustomerId and VehicleId through the
    // tenant-filtered DbSet but assigned AssignedZoneId with no lookup at all, so another
    // business's zone GUID was accepted (the FK is satisfied at the database, and tenancy
    // is never checked there). UpdateStatusAsync then auto-created a Booking on that
    // foreign zone, and the calendar list projects b.Zone.Name unconditionally -- the zone
    // is filtered out for this tenant, so the projection dereferenced null and the whole
    // calendar 500'd. See finding 2 in docs/review-findings.md.
    private async Task EnsureZoneIsOursAsync(Guid? zoneId, CancellationToken ct)
    {
        if (!zoneId.HasValue) return;

        if (!await repository.ZoneExistsAsync(zoneId.Value, ct))
            throw new NotFoundException("Zone not found");
    }

    /// <summary>
    /// Mirrors a job's new status onto its calendar booking. Three cases, and the third is
    /// the one worth reading twice: moving back to an active status either revives a
    /// cancelled booking or creates the booking that never existed, which is the only path
    /// in the codebase that writes a Booking outside the Calendar slice.
    ///
    /// Synchronous, and takes the already-loaded booking, so the decision table stays
    /// readable next to the I/O rather than interleaved with it.
    /// </summary>
    private void SyncBookingToJobStatus(Job job, Booking? booking, JobStatus newStatus)
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
                ReviveOrCreateBooking(job, booking);
                break;
        }
    }

    private void ReviveOrCreateBooking(Job job, Booking? booking)
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
            CreatedByUserId = currentUser.UserId
        };

        repository.AddBooking(created);
        created.JobId = job.Id;
        job.BookingId = created.Id;
    }

    // Groups the job's tax by the rate each line was charged at. Where a rate carries
    // jurisdiction components they ride along for display; the AMOUNT always comes from the
    // line snapshots, never from re-summing component percentages, which would drift from
    // what the customer was actually charged.
    //
    // Plain // rather than ///: Task-returning method with a generic return, which the
    // .NET 10 preview OpenAPI comment generator mishandles. See CLAUDE.md.
    private async Task<List<JobTaxGroup>> BuildTaxBreakdownAsync(Job job, CancellationToken ct)
    {
        var byRate = job.LaborLines
            .Select(l => new { l.TaxRateId, l.TaxRatePercent, l.TaxAmount })
            .Concat(job.PartLines.Select(p => new { p.TaxRateId, p.TaxRatePercent, p.TaxAmount }))
            .Where(x => x.TaxAmount != 0m)
            .GroupBy(x => new { x.TaxRateId, x.TaxRatePercent })
            .ToList();

        if (byRate.Count == 0) return [];

        var rateIds = byRate.Select(g => g.Key.TaxRateId).OfType<Guid>().ToList();
        var rates = await repository.GetTaxRatesWithComponentsAsync(rateIds, ct);

        return byRate.Select(g =>
        {
            var rate = rates.FirstOrDefault(r => r.Id == g.Key.TaxRateId);
            var components = rate?.Components.OrderBy(c => c.SortOrder).ToList() ?? [];

            return new JobTaxGroup(
                rate?.Name ?? "Tax",
                g.Key.TaxRatePercent,
                g.Sum(x => x.TaxAmount),
                components);
        }).ToList();
    }

    /// <summary>
    /// Picks the rate a new line is raised at, and snapshots it.
    ///
    /// Returns nothing when the customer is exempt or no rate is mapped to the category --
    /// a US shop with no labour mapping is stating that labour is not taxable there, which
    /// is a real answer rather than a missing setting.
    /// </summary>
    private async Task<(Guid? RateId, decimal Percent)> ResolveTaxRateAsync(
        Guid customerId, TaxCategory category, CancellationToken ct)
    {
        var customer = await repository.FindCustomerAsync(customerId, ct);
        if (customer is { IsTaxExempt: true }) return (null, 0m);

        var mapping = await repository.FindActiveTaxMappingAsync(category, ct);
        return mapping is null ? (null, 0m) : (mapping.TaxRateId, mapping.TaxRate.Rate);
    }
}
