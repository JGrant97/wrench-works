using FluentValidation;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Middleware;
using Entities = WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Business;

public class BusinessService(IBusinessRepository repository, CurrentUserService currentUser) : IBusinessService
{
    public async Task<Entities.Business> GetAsync(CancellationToken ct) =>
        await repository.FindAsync(currentUser.RequireBusinessId(), ct)
            ?? throw new NotFoundException("Business not found");

    public async Task<Entities.Business> UpdateAsync(UpdateBusinessRequest request, CancellationToken ct)
    {
        await new UpdateBusinessValidator().ValidateAndThrowAsync(request, ct);

        var business = await repository.FindAsync(currentUser.RequireBusinessId(), ct)
            ?? throw new NotFoundException("Business not found");

        business.Name = request.Name.Trim();
        business.Address = request.Address?.Trim();
        business.Phone = request.Phone?.Trim();
        business.Timezone = request.Timezone;
        business.Currency = request.Currency;
        business.PricesIncludeTax = request.PricesIncludeTax;
        business.TaxRegistrationNumber = request.TaxRegistrationNumber?.Trim();
        // Empty label would render an invoice line with no word in front of the number.
        business.TaxLabel = string.IsNullOrWhiteSpace(request.TaxLabel) ? "Tax" : request.TaxLabel.Trim();
        business.WorkingHoursJson = request.WorkingHoursJson;

        await repository.SaveChangesAsync(ct);
        return business;
    }
}
