using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Business;

public class BusinessService(AppDbContext db, CurrentUserService currentUser) : IBusinessService
{
    public async Task<BusinessDto> GetAsync(CancellationToken ct)
    {
        var businessId = currentUser.RequireBusinessId();
        var business = await db.Businesses.FindAsync([businessId], ct)
            ?? throw new NotFoundException("Business not found");

        return new BusinessDto(
            business.Id, business.Name, business.Address, business.Phone,
            business.Timezone, business.Currency, business.LogoUrl,
            business.WorkingHoursJson,
            business.PricesIncludeTax, business.TaxRegistrationNumber, business.TaxLabel,
            business.CreatedAtUtc);
    }

    public async Task<BusinessDto> UpdateAsync(UpdateBusinessRequest request, CancellationToken ct)
    {
        await new UpdateBusinessValidator().ValidateAndThrowAsync(request, ct);

        var businessId = currentUser.RequireBusinessId();
        var business = await db.Businesses.FindAsync([businessId], ct)
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

        await db.SaveChangesAsync(ct);

        return new BusinessDto(
            business.Id, business.Name, business.Address, business.Phone,
            business.Timezone, business.Currency, business.LogoUrl,
            business.WorkingHoursJson,
            business.PricesIncludeTax, business.TaxRegistrationNumber, business.TaxLabel,
            business.CreatedAtUtc);
    }
}
