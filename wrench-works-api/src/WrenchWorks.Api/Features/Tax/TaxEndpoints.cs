using Microsoft.AspNetCore.Http.HttpResults;
using WrenchWorks.Api.Features.Common;

namespace WrenchWorks.Api.Features.Tax;

/// <summary>
/// Tax rates the business configures for itself. See docs/tax.md for why the product does
/// not ship a rate table.
/// </summary>
public static class TaxEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tax").WithTags("Tax").RequireAuthorization();

        group.MapGet("/rates", ListAsync).RequireAuthorization("settings.manage");
        group.MapPost("/rates", CreateAsync).RequireAuthorization("settings.manage");
        group.MapPut("/rates/{id:guid}", UpdateAsync).RequireAuthorization("settings.manage");
        group.MapDelete("/rates/{id:guid}", DeleteAsync).RequireAuthorization("settings.manage");
        group.MapPost("/rates/{id:guid}/archive", ArchiveAsync).RequireAuthorization("settings.manage");
        group.MapPost("/rates/{id:guid}/unarchive", UnarchiveAsync).RequireAuthorization("settings.manage");
    }

    private static async Task<Ok<List<TaxRateDto>>> ListAsync(ITaxService svc, bool includeArchived = false, CancellationToken ct = default) =>
        TypedResults.Ok(await svc.ListAsync(includeArchived, ct));

    private static async Task<Created<TaxRateDto>> CreateAsync(ITaxService svc, SaveTaxRateRequest request, CancellationToken ct)
    {
        var result = await svc.CreateAsync(request, ct);
        return TypedResults.Created($"/api/tax/rates/{result.Id}", result);
    }

    private static async Task<Ok<TaxRateDto>> UpdateAsync(ITaxService svc, Guid id, SaveTaxRateRequest request, CancellationToken ct) =>
        TypedResults.Ok(await svc.UpdateAsync(id, request, ct));

    private static async Task<NoContent> DeleteAsync(ITaxService svc, Guid id, CancellationToken ct)
    {
        await svc.DeleteAsync(id, ct);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<ArchiveResultDto>> ArchiveAsync(ITaxService svc, Guid id, CancellationToken ct) =>
        TypedResults.Ok(await svc.ArchiveAsync(id, ct));

    private static async Task<Ok<ArchiveResultDto>> UnarchiveAsync(ITaxService svc, Guid id, CancellationToken ct) =>
        TypedResults.Ok(await svc.UnarchiveAsync(id, ct));
}
