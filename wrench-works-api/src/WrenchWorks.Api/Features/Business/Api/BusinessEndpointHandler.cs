using Microsoft.AspNetCore.Http.HttpResults;
using Entities = WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Business;

public class BusinessEndpointHandler(IBusinessService service) : IBusinessEndpointHandler
{
    private static BusinessDto ToDto(Entities.Business b) =>
        new(b.Id, b.Name, b.Address, b.Phone, b.Timezone, b.Currency, b.LogoUrl,
            b.WorkingHoursJson, b.PricesIncludeTax, b.TaxRegistrationNumber, b.TaxLabel,
            b.CreatedAtUtc);

    public async Task<Ok<BusinessDto>> GetAsync(CancellationToken ct) =>
        TypedResults.Ok(ToDto(await service.GetAsync(ct)));

    public async Task<Ok<BusinessDto>> UpdateAsync(UpdateBusinessRequest request, CancellationToken ct) =>
        TypedResults.Ok(ToDto(await service.UpdateAsync(request, ct)));
}
