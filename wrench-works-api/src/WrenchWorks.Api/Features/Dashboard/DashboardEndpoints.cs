using Microsoft.AspNetCore.Http.HttpResults;

namespace WrenchWorks.Api.Features.Dashboard;

public static class DashboardEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/dashboard").WithTags("Dashboard").RequireAuthorization();

        group.MapGet("/", GetAsync).RequireAuthorization("jobs.view");
    }

    private static async Task<Ok<DashboardDto>> GetAsync(IDashboardService svc, CancellationToken ct) =>
        TypedResults.Ok(await svc.GetAsync(ct));
}
