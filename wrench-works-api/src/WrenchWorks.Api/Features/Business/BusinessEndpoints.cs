using FluentValidation;
using Microsoft.EntityFrameworkCore;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Business;

public record UpdateBusinessRequest(string Name, string? Address, string? Phone, string Timezone, string Currency, string? WorkingHoursJson);
public record BusinessDto(Guid Id, string Name, string? Address, string? Phone, string Timezone, string Currency, string? LogoUrl, string? WorkingHoursJson, DateTime CreatedAtUtc);

public class UpdateBusinessValidator : AbstractValidator<UpdateBusinessRequest>
{
    public UpdateBusinessValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Timezone).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Currency).NotEmpty().MaximumLength(10);
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
            business.WorkingHoursJson, business.CreatedAtUtc));
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
        business.WorkingHoursJson = request.WorkingHoursJson;

        await db.SaveChangesAsync(ct);

        return Results.Ok(new BusinessDto(
            business.Id, business.Name, business.Address, business.Phone,
            business.Timezone, business.Currency, business.LogoUrl,
            business.WorkingHoursJson, business.CreatedAtUtc));
    }
}
