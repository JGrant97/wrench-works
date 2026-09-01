using Microsoft.AspNetCore.Http.HttpResults;
using WrenchWorks.Api.Features.Common;
using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Jobs;

public static class JobEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/jobs").WithTags("Jobs").RequireAuthorization();

        group.MapGet("/", ListAsync).RequireAuthorization("jobs.view");
        group.MapGet("/{id:guid}", GetAsync).RequireAuthorization("jobs.view");
        group.MapPost("/", CreateAsync).RequireAuthorization("jobs.create");
        group.MapPut("/{id:guid}", UpdateJobAsync).RequireAuthorization("jobs.edit");
        group.MapPatch("/{id:guid}/status", UpdateStatusAsync).RequireAuthorization("jobs.edit");
        group.MapPost("/{id:guid}/parts", AddPartAsync).RequireAuthorization("jobs.edit");
        group.MapPost("/{id:guid}/labor", AddLaborAsync).RequireAuthorization("jobs.edit");
        group.MapDelete("/{id:guid}/parts/{lineId:guid}", RemovePartAsync).RequireAuthorization("jobs.edit");
        group.MapDelete("/{id:guid}/labor/{lineId:guid}", RemoveLaborAsync).RequireAuthorization("jobs.edit");
        group.MapDelete("/{id:guid}", DeleteAsync).RequireAuthorization("jobs.delete");
        group.MapPost("/{id:guid}/archive", ArchiveAsync).RequireAuthorization("jobs.delete");
        group.MapPost("/{id:guid}/unarchive", UnarchiveAsync).RequireAuthorization("jobs.delete");
    }

    private static async Task<Ok<PagedResult<JobListItemDto>>> ListAsync(IJobService svc, int page = 1, int pageSize = 25, string? status = null, string? search = null, bool includeArchived = false, CancellationToken ct = default) =>
        TypedResults.Ok(await svc.ListAsync(page, pageSize, status, search, includeArchived, ct));

    private static async Task<NoContent> DeleteAsync(IJobService svc, Guid id, CancellationToken ct)
    {
        await svc.DeleteAsync(id, ct);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<ArchiveResultDto>> ArchiveAsync(IJobService svc, Guid id, CancellationToken ct) =>
        TypedResults.Ok(await svc.ArchiveAsync(id, ct));

    private static async Task<Ok<ArchiveResultDto>> UnarchiveAsync(IJobService svc, Guid id, CancellationToken ct) =>
        TypedResults.Ok(await svc.UnarchiveAsync(id, ct));

    private static async Task<Ok<JobDetailDto>> GetAsync(IJobService svc, Guid id, CancellationToken ct) =>
        TypedResults.Ok(await svc.GetAsync(id, ct));

    private static async Task<Created<JobCreatedDto>> CreateAsync(IJobService svc, CreateJobRequest request, CancellationToken ct)
    {
        var result = await svc.CreateAsync(request, ct);
        return TypedResults.Created($"/api/jobs/{result.Id}", result);
    }

    private static async Task<Ok<JobSummaryDto>> UpdateJobAsync(IJobService svc, Guid id, UpdateJobRequest request, CancellationToken ct) =>
        TypedResults.Ok(await svc.UpdateJobAsync(id, request, ct));

    private static async Task<Ok<JobStatusDto>> UpdateStatusAsync(IJobService svc, Guid id, UpdateJobStatusRequest request, CancellationToken ct) =>
        TypedResults.Ok(await svc.UpdateStatusAsync(id, request, ct));

    private static async Task<Created<PartLineDto>> AddPartAsync(IJobService svc, Guid id, AddPartToJobRequest request, CancellationToken ct)
    {
        var result = await svc.AddPartAsync(id, request, ct);
        return TypedResults.Created($"/api/jobs/{id}/parts/{result.Id}", result);
    }

    private static async Task<Created<LaborLineDto>> AddLaborAsync(IJobService svc, Guid id, AddLaborLineRequest request, CancellationToken ct)
    {
        var result = await svc.AddLaborAsync(id, request, ct);
        return TypedResults.Created($"/api/jobs/{id}/labor/{result.Id}", result);
    }

    private static async Task<NoContent> RemovePartAsync(IJobService svc, Guid id, Guid lineId, CancellationToken ct)
    {
        await svc.RemovePartAsync(id, lineId, ct);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> RemoveLaborAsync(IJobService svc, Guid id, Guid lineId, CancellationToken ct)
    {
        await svc.RemoveLaborAsync(id, lineId, ct);
        return TypedResults.NoContent();
    }
}
