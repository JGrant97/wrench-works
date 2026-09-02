using Microsoft.AspNetCore.Http.HttpResults;

namespace WrenchWorks.Api.Features.Dashboard;

public interface IDashboardEndpointHandler
{
    Task<Ok<DashboardDto>> GetAsync(CancellationToken ct);
}
