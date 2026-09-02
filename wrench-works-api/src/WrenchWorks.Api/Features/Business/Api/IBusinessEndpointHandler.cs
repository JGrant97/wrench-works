using Microsoft.AspNetCore.Http.HttpResults;

namespace WrenchWorks.Api.Features.Business;

public interface IBusinessEndpointHandler
{
    Task<Ok<BusinessDto>> GetAsync(CancellationToken ct);
    Task<Ok<BusinessDto>> UpdateAsync(UpdateBusinessRequest request, CancellationToken ct);
}
