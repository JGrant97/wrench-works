using Microsoft.AspNetCore.Http.HttpResults;
using WrenchWorks.Api.Features.Common;
using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Tax;

public class TaxEndpointHandler(ITaxService service) : ITaxEndpointHandler
{
    // Rates are stored and returned as fractions (0.2); the web layer converts to a
    // percentage for display. See docs/tax.md for why the column is decimal(9,6).
    private static TaxRateDto ToDto(TaxRate r) =>
        new(r.Id, r.Name, r.Rate,
            r.Categories.Select(c => c.Category.ToString()).OrderBy(c => c),
            r.ArchivedAtUtc != null,
            r.Components.OrderBy(c => c.SortOrder)
                        .Select(c => new TaxRateComponentDto(c.Id, c.Name, c.Rate, c.SortOrder)));

    public async Task<Ok<List<TaxRateDto>>> ListAsync(bool includeArchived, CancellationToken ct)
    {
        var rates = await service.ListAsync(includeArchived, ct);
        return TypedResults.Ok(rates.Select(ToDto).ToList());
    }

    public async Task<Created<TaxRateDto>> CreateAsync(SaveTaxRateRequest request, CancellationToken ct)
    {
        var rate = await service.CreateAsync(request, ct);
        return TypedResults.Created($"/api/tax/rates/{rate.Id}", ToDto(rate));
    }

    public async Task<Ok<TaxRateDto>> UpdateAsync(Guid id, SaveTaxRateRequest request, CancellationToken ct) =>
        TypedResults.Ok(ToDto(await service.UpdateAsync(id, request, ct)));

    public async Task<NoContent> DeleteAsync(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return TypedResults.NoContent();
    }

    // ArchiveResultDto comes from the shared Archiving helper in Features/Common rather
    // than from this slice, so it passes through unmapped -- the one place a service
    // returns something DTO-shaped, and deliberately so.
    public async Task<Ok<ArchiveResultDto>> ArchiveAsync(Guid id, CancellationToken ct) =>
        TypedResults.Ok(await service.ArchiveAsync(id, ct));

    public async Task<Ok<ArchiveResultDto>> UnarchiveAsync(Guid id, CancellationToken ct) =>
        TypedResults.Ok(await service.UnarchiveAsync(id, ct));
}
