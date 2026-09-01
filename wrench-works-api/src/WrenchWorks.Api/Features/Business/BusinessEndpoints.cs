using FluentValidation;
using Microsoft.EntityFrameworkCore;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Business;

/// <summary>
/// The currencies the product supports. A closed set rather than free text: every amount
/// in the app is formatted from this value, so an unrecognised code would render as the
/// raw string next to the number and there would be no way to spot it from the server.
///
/// The UI offers exactly these; this is the check that makes the dropdown non-negotiable,
/// since a request need not come from the dropdown.
/// </summary>
public static class SupportedCurrencies
{
    public static readonly string[] Codes = ["GBP", "USD", "EUR"];

    public static bool IsSupported(string? code) =>
        code is not null && Codes.Contains(code, StringComparer.OrdinalIgnoreCase);
}

public record UpdateBusinessRequest(string Name, string? Address, string? Phone, string Timezone, string Currency, string? WorkingHoursJson, bool PricesIncludeTax = false, string? TaxRegistrationNumber = null, string? TaxLabel = null);
public record BusinessDto(Guid Id, string Name, string? Address, string? Phone, string Timezone, string Currency, string? LogoUrl, string? WorkingHoursJson, bool PricesIncludeTax, string? TaxRegistrationNumber, string TaxLabel, DateTime CreatedAtUtc);

public class UpdateBusinessValidator : AbstractValidator<UpdateBusinessRequest>
{
    public UpdateBusinessValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Timezone).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Currency)
            .NotEmpty()
            .Must(SupportedCurrencies.IsSupported)
            .WithMessage($"Currency must be one of {string.Join(", ", SupportedCurrencies.Codes)}");
    }
}

public static class BusinessEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/business").WithTags("Business").RequireAuthorization();

        group.MapGet("/", GetAsync);
        group.MapPut("/", UpdateAsync).RequireAuthorization("settings.manage");
    }

    private static async Task<IResult> GetAsync(
        AppDbContext db,
        CurrentUserService currentUser,
        CancellationToken ct)
    {
        var businessId = currentUser.RequireBusinessId();
        var business = await db.Businesses.FindAsync([businessId], ct)
            ?? throw new NotFoundException("Business not found");

        return Results.Ok(new BusinessDto(
            business.Id, business.Name, business.Address, business.Phone,
            business.Timezone, business.Currency, business.LogoUrl,
            business.WorkingHoursJson,
            business.PricesIncludeTax, business.TaxRegistrationNumber, business.TaxLabel,
            business.CreatedAtUtc));
    }

    private static async Task<IResult> UpdateAsync(
        UpdateBusinessRequest request,
        AppDbContext db,
        CurrentUserService currentUser,
        CancellationToken ct)
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

        return Results.Ok(new BusinessDto(
            business.Id, business.Name, business.Address, business.Phone,
            business.Timezone, business.Currency, business.LogoUrl,
            business.WorkingHoursJson,
            business.PricesIncludeTax, business.TaxRegistrationNumber, business.TaxLabel,
            business.CreatedAtUtc));
    }
}
