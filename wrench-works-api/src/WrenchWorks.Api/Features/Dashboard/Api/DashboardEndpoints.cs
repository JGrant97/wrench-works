namespace WrenchWorks.Api.Features.Dashboard;

public static class DashboardEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/dashboard").WithTags("Dashboard").RequireAuthorization();

        group.MapGet("/",
            (IDashboardEndpointHandler handler, CancellationToken ct) =>
                handler.GetAsync(ct))
            .RequireAuthorization("jobs.view");
    }
}
