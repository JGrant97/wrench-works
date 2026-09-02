namespace WrenchWorks.Api.Features.Jobs;

public static class JobEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/jobs").WithTags("Jobs").RequireAuthorization();

        // Optional query parameters keep their defaults: a required parameter that fails
        // to bind throws, and ErrorHandlingMiddleware turns that into a 500 not a 400.
        group.MapGet("/",
            (IJobEndpointHandler handler, CancellationToken ct,
             int page = 1, int pageSize = 25, string? status = null,
             string? search = null, bool includeArchived = false) =>
                handler.ListAsync(page, pageSize, status, search, includeArchived, ct))
            .RequireAuthorization("jobs.view");

        group.MapGet("/{id:guid}",
            (Guid id, IJobEndpointHandler handler, CancellationToken ct) =>
                handler.GetAsync(id, ct))
            .RequireAuthorization("jobs.view");

        group.MapPost("/",
            (CreateJobRequest request, IJobEndpointHandler handler, CancellationToken ct) =>
                handler.CreateAsync(request, ct))
            .RequireAuthorization("jobs.create");

        group.MapPut("/{id:guid}",
            (Guid id, UpdateJobRequest request, IJobEndpointHandler handler, CancellationToken ct) =>
                handler.UpdateJobAsync(id, request, ct))
            .RequireAuthorization("jobs.edit");

        group.MapPatch("/{id:guid}/status",
            (Guid id, UpdateJobStatusRequest request, IJobEndpointHandler handler, CancellationToken ct) =>
                handler.UpdateStatusAsync(id, request, ct))
            .RequireAuthorization("jobs.edit");

        group.MapPost("/{id:guid}/parts",
            (Guid id, AddPartToJobRequest request, IJobEndpointHandler handler, CancellationToken ct) =>
                handler.AddPartAsync(id, request, ct))
            .RequireAuthorization("jobs.edit");

        group.MapPost("/{id:guid}/labor",
            (Guid id, AddLaborLineRequest request, IJobEndpointHandler handler, CancellationToken ct) =>
                handler.AddLaborAsync(id, request, ct))
            .RequireAuthorization("jobs.edit");

        group.MapDelete("/{id:guid}/parts/{lineId:guid}",
            (Guid id, Guid lineId, IJobEndpointHandler handler, CancellationToken ct) =>
                handler.RemovePartAsync(id, lineId, ct))
            .RequireAuthorization("jobs.edit");

        group.MapDelete("/{id:guid}/labor/{lineId:guid}",
            (Guid id, Guid lineId, IJobEndpointHandler handler, CancellationToken ct) =>
                handler.RemoveLaborAsync(id, lineId, ct))
            .RequireAuthorization("jobs.edit");

        group.MapDelete("/{id:guid}",
            (Guid id, IJobEndpointHandler handler, CancellationToken ct) =>
                handler.DeleteAsync(id, ct))
            .RequireAuthorization("jobs.delete");

        group.MapPost("/{id:guid}/archive",
            (Guid id, IJobEndpointHandler handler, CancellationToken ct) =>
                handler.ArchiveAsync(id, ct))
            .RequireAuthorization("jobs.delete");

        group.MapPost("/{id:guid}/unarchive",
            (Guid id, IJobEndpointHandler handler, CancellationToken ct) =>
                handler.UnarchiveAsync(id, ct))
            .RequireAuthorization("jobs.delete");
    }
}
