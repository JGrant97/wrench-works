using FluentValidation;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Features.Common;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Tax;

public class TaxService(ITaxRepository repository, CurrentUserService currentUser) : ITaxService
{
    public Task<List<TaxRate>> ListAsync(bool includeArchived, CancellationToken ct) =>
        repository.ListAsync(includeArchived, ct);

    public async Task<TaxRate> CreateAsync(SaveTaxRateRequest request, CancellationToken ct)
    {
        await new SaveTaxRateValidator().ValidateAndThrowAsync(request, ct);

        var businessId = currentUser.RequireBusinessId();

        var rate = new TaxRate
        {
            BusinessId = businessId,
            Name = request.Name.Trim(),
            Rate = request.Rate
        };

        ApplyComponents(rate, request.Components);
        repository.Add(rate);

        await AssignCategoriesAsync(rate, businessId, request.Categories, ct);
        await repository.SaveChangesAsync(ct);

        return rate;
    }

    public async Task<TaxRate> UpdateAsync(Guid id, SaveTaxRateRequest request, CancellationToken ct)
    {
        await new SaveTaxRateValidator().ValidateAndThrowAsync(request, ct);

        var rate = await repository.FindWithComponentsAndCategoriesAsync(id, ct)
            ?? throw new NotFoundException("Tax rate not found");

        rate.Name = request.Name.Trim();
        rate.Rate = request.Rate;

        // Replace rather than merge: components are an ordered list the user edits whole,
        // and matching them up by name would break the moment one is renamed.
        repository.RemoveComponents(rate.Components);
        rate.Components.Clear();
        ApplyComponents(rate, request.Components);

        await AssignCategoriesAsync(rate, rate.BusinessId, request.Categories, ct);
        await repository.SaveChangesAsync(ct);

        return rate;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var rate = await repository.FindAsync(id, ct)
            ?? throw new NotFoundException("Tax rate not found");

        // Lines snapshot the rate they were charged at, but they also keep the id -- a rate
        // still referenced is archived, never removed, or an invoice loses what it names.
        Archiving.EnsureDeletable("tax rate",
            new Dependent("labour lines", await repository.CountLabourLinesUsingAsync(id, ct)),
            new Dependent("part lines", await repository.CountPartLinesUsingAsync(id, ct)));

        repository.Remove(rate);
        await repository.SaveChangesAsync(ct);
    }

    public async Task<ArchiveResultDto> ArchiveAsync(Guid id, CancellationToken ct)
    {
        var rate = await repository.FindAsync(id, ct)
            ?? throw new NotFoundException("Tax rate not found");

        // An archived rate must stop being a default, or new lines would keep picking it.
        var mappings = await repository.GetCategoryMappingsAsync(id, [], ct);
        repository.RemoveCategoryMappings(mappings);

        var result = Archiving.Archive(rate, id);
        await repository.SaveChangesAsync(ct);
        return result;
    }

    public async Task<ArchiveResultDto> UnarchiveAsync(Guid id, CancellationToken ct)
    {
        var rate = await repository.FindAsync(id, ct)
            ?? throw new NotFoundException("Tax rate not found");

        var result = Archiving.Unarchive(rate, id);
        await repository.SaveChangesAsync(ct);
        return result;
    }

    private static void ApplyComponents(TaxRate rate, IEnumerable<TaxRateComponentRequest>? components)
    {
        if (components is null) return;

        foreach (var c in components)
        {
            rate.Components.Add(new TaxRateComponent
            {
                TaxRateId = rate.Id,
                Name = c.Name.Trim(),
                Rate = c.Rate,
                SortOrder = c.SortOrder
            });
        }
    }

    // Points the requested categories at this rate, and releases any it no longer claims.
    //
    // A category maps to exactly one rate, so assigning it here necessarily takes it from
    // whichever rate held it -- the rows are deleted and recreated rather than edited,
    // because the unique index on (BusinessId, Category) would otherwise be violated
    // mid-transaction by two rows briefly holding the same category.
    //
    // Plain // rather than ///: Task-returning method, which the .NET 10 preview OpenAPI
    // comment generator mishandles. See CLAUDE.md.
    private async Task AssignCategoriesAsync(
        TaxRate rate, Guid businessId, IEnumerable<string>? categories, CancellationToken ct)
    {
        var wanted = (categories ?? [])
            .Select(c => Enum.Parse<TaxCategory>(c, true))
            .Distinct()
            .ToList();

        var affected = await repository.GetCategoryMappingsAsync(rate.Id, wanted, ct);
        repository.RemoveCategoryMappings(affected);

        foreach (var category in wanted)
        {
            repository.AddCategoryMapping(new TaxRateCategory
            {
                BusinessId = businessId,
                Category = category,
                TaxRateId = rate.Id
            });
        }
    }
}
