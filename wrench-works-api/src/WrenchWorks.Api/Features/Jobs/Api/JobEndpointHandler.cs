using Microsoft.AspNetCore.Http.HttpResults;
using WrenchWorks.Api.Features.Common;
using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Jobs;

public class JobEndpointHandler(IJobService service) : IJobEndpointHandler
{
    private static string VehicleDisplay(Vehicle v) =>
        (v.DisplayName ?? "") + (v.Registration != null ? " " + v.Registration : "");

    private static LaborLineDto ToDto(JobLaborLine l) =>
        new(l.Id, l.Description, l.Hours, l.Rate, l.Hours * l.Rate, l.TaxRatePercent, l.TaxAmount);

    private static PartLineDto ToDto(JobPartLine p) =>
        new(p.Id, p.InventoryItemId, p.InventoryItem.Name, p.InventoryItem.Sku,
            p.Quantity, p.UnitPrice, p.Quantity * p.UnitPrice, p.TaxRatePercent, p.TaxAmount);

    public async Task<Ok<PagedResult<JobListItemDto>>> ListAsync(int page, int pageSize,
        string? status, string? search, bool includeArchived, CancellationToken ct)
    {
        var result = await service.ListAsync(page, pageSize, status, search, includeArchived, ct);

        // laborTotal and partsTotal are on the list row because the page renders them; the
        // job list used to omit them and rendered NaN for every job. See docs/app-flow.md.
        return TypedResults.Ok(new PagedResult<JobListItemDto>(
            result.Items.Select(j => new JobListItemDto(
                j.Id, j.Title, j.Status.ToString(), j.Priority.ToString(),
                j.Customer.Name,
                VehicleDisplay(j.Vehicle),
                j.AssignedZone?.Name,
                j.ScheduledStartUtc,
                j.LaborLines.Sum(l => l.Hours * l.Rate),
                j.PartLines.Sum(p => p.Quantity * p.UnitPrice),
                j.CreatedAtUtc)).ToList(),
            result.Total, result.Page, result.PageSize));
    }

    public async Task<Ok<JobDetailDto>> GetAsync(Guid id, CancellationToken ct)
    {
        var d = await service.GetAsync(id, ct);
        var job = d.Job;

        return TypedResults.Ok(new JobDetailDto(
            job.Id, job.Title, job.Status.ToString(), job.Priority.ToString(),
            job.CustomerId, job.Customer.Name,
            job.VehicleId, VehicleDisplay(job.Vehicle).Trim(),
            job.AssignedZoneId, job.AssignedZone?.Name,
            job.InternalNotes, job.CustomerNotes,
            job.ScheduledStartUtc, job.ScheduledEndUtc,
            job.LaborLines.Select(ToDto),
            job.PartLines.Select(ToDto),
            d.Totals.LaborTotal, d.Totals.PartsTotal, d.Totals.GrandTotal,
            d.Totals.SubTotal, d.Totals.TaxTotal,
            d.TaxLabel,
            d.PricesIncludeTax,
            job.Customer.IsTaxExempt,
            d.TaxBreakdown.Select(g => new TaxLineDto(
                g.RateName, g.Percent, g.Amount,
                g.Components.Select(c => new TaxComponentLineDto(c.Name, c.Rate)))).ToList(),
            job.CreatedAtUtc));
    }

    public async Task<Created<JobCreatedDto>> CreateAsync(CreateJobRequest request, CancellationToken ct)
    {
        var job = await service.CreateAsync(request, ct);
        return TypedResults.Created($"/api/jobs/{job.Id}", new JobCreatedDto(job.Id, job.Status));
    }

    public async Task<Ok<JobSummaryDto>> UpdateJobAsync(Guid id, UpdateJobRequest request, CancellationToken ct)
    {
        var job = await service.UpdateJobAsync(id, request, ct);
        return TypedResults.Ok(new JobSummaryDto(
            job.Id, job.Title, job.Status.ToString(), job.Priority.ToString()));
    }

    public async Task<Ok<JobStatusDto>> UpdateStatusAsync(Guid id, UpdateJobStatusRequest request, CancellationToken ct)
    {
        var job = await service.UpdateStatusAsync(id, request, ct);
        return TypedResults.Ok(new JobStatusDto(job.Id, job.Status.ToString()));
    }

    public async Task<Created<PartLineDto>> AddPartAsync(Guid id, AddPartToJobRequest request, CancellationToken ct)
    {
        var line = await service.AddPartAsync(id, request, ct);
        return TypedResults.Created($"/api/jobs/{id}/parts/{line.Id}", ToDto(line));
    }

    public async Task<Created<LaborLineDto>> AddLaborAsync(Guid id, AddLaborLineRequest request, CancellationToken ct)
    {
        var line = await service.AddLaborAsync(id, request, ct);
        return TypedResults.Created($"/api/jobs/{id}/labor/{line.Id}", ToDto(line));
    }

    public async Task<NoContent> RemovePartAsync(Guid id, Guid lineId, CancellationToken ct)
    {
        await service.RemovePartAsync(id, lineId, ct);
        return TypedResults.NoContent();
    }

    public async Task<NoContent> RemoveLaborAsync(Guid id, Guid lineId, CancellationToken ct)
    {
        await service.RemoveLaborAsync(id, lineId, ct);
        return TypedResults.NoContent();
    }

    public async Task<NoContent> DeleteAsync(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return TypedResults.NoContent();
    }

    public async Task<Ok<ArchiveResultDto>> ArchiveAsync(Guid id, CancellationToken ct) =>
        TypedResults.Ok(await service.ArchiveAsync(id, ct));

    public async Task<Ok<ArchiveResultDto>> UnarchiveAsync(Guid id, CancellationToken ct) =>
        TypedResults.Ok(await service.UnarchiveAsync(id, ct));
}
