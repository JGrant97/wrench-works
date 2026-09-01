using FluentValidation;
using Microsoft.EntityFrameworkCore;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Features.Common;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Tax;

public record TaxRateComponentDto(Guid Id, string Name, decimal Rate, int SortOrder);

public record TaxRateDto(
    Guid Id, string Name, decimal Rate,
    IEnumerable<string> Categories,
    bool IsArchived,
    IEnumerable<TaxRateComponentDto> Components);

public record TaxRateComponentRequest(string Name, decimal Rate, int SortOrder);

public record SaveTaxRateRequest(
    string Name, decimal Rate,
    IEnumerable<string>? Categories,
    IEnumerable<TaxRateComponentRequest>? Components);

public class SaveTaxRateValidator : AbstractValidator<SaveTaxRateRequest>
{
    public SaveTaxRateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);

        // 0–100%. Rejecting >1 catches the commonest data-entry error by far: typing "20"
        // for 20% instead of 0.2, which would otherwise charge 2000% tax.
        RuleFor(x => x.Rate)
            .InclusiveBetween(0m, 1m)
            .WithMessage("Rate must be a fraction between 0 and 1 — enter 0.2 for 20%");

        // An unrecognised category would silently apply to nothing, which looks like a
        // rate that simply does not work.
        RuleForEach(x => x.Categories)
            .Must(c => Enum.TryParse<TaxCategory>(c, true, out _))
            .WithMessage($"Category must be one of {string.Join(", ", Enum.GetNames<TaxCategory>())}");

        RuleForEach(x => x.Components).ChildRules(c =>
        {
            c.RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
            c.RuleFor(x => x.Rate).InclusiveBetween(0m, 1m);
        });
    }
}

/// <summary>
/// Tax rates the business configures for itself. See docs/tax.md for why the product does
/// not ship a rate table.
/// </summary>
public static class TaxEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tax").WithTags("Tax").RequireAuthorization();

        group.MapGet("/rates", ListAsync).RequireAuthorization("settings.manage")
             .Produces<List<TaxRateDto>>();
        group.MapPost("/rates", CreateAsync).RequireAuthorization("settings.manage")
             .Produces<TaxRateDto>();
        group.MapPut("/rates/{id:guid}", UpdateAsync).RequireAuthorization("settings.manage")
             .Produces<TaxRateDto>();
        group.MapDelete("/rates/{id:guid}", DeleteAsync).RequireAuthorization("settings.manage");
        group.MapPost("/rates/{id:guid}/archive", ArchiveAsync).RequireAuthorization("settings.manage")
             .Produces<ArchiveResultDto>();
        group.MapPost("/rates/{id:guid}/unarchive", UnarchiveAsync).RequireAuthorization("settings.manage")
             .Produces<ArchiveResultDto>();
    }

    private static TaxRateDto ToDto(TaxRate r) => new(
        r.Id, r.Name, r.Rate,
        r.Categories.Select(c => c.Category.ToString()).OrderBy(c => c),
        r.ArchivedAtUtc != null,
        r.Components.OrderBy(c => c.SortOrder)
            .Select(c => new TaxRateComponentDto(c.Id, c.Name, c.Rate, c.SortOrder)));

    private static async Task<IResult> ListAsync(AppDbContext db, bool includeArchived = false, CancellationToken ct = default)
    {
        var query = db.TaxRates.Include(r => r.Components).Include(r => r.Categories).AsQueryable();
        if (!includeArchived) query = query.Where(r => r.ArchivedAtUtc == null);

        var rates = await query.OrderBy(r => r.Name).ToListAsync(ct);
        return Results.Ok(rates.Select(ToDto).ToList());
    }

    private static async Task<IResult> CreateAsync(
        SaveTaxRateRequest request, AppDbContext db, CurrentUserService currentUser, CancellationToken ct)
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
        db.TaxRates.Add(rate);

        await AssignCategoriesAsync(db, rate, businessId, request.Categories, ct);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/tax/rates/{rate.Id}", ToDto(rate));
    }

    private static async Task<IResult> UpdateAsync(
        Guid id, SaveTaxRateRequest request, AppDbContext db, CancellationToken ct)
    {
        await new SaveTaxRateValidator().ValidateAndThrowAsync(request, ct);

        var rate = await db.TaxRates
            .Include(r => r.Components).Include(r => r.Categories)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException("Tax rate not found");

        rate.Name = request.Name.Trim();
        rate.Rate = request.Rate;

        // Replace rather than merge: components are an ordered list the user edits whole,
        // and matching them up by name would break the moment one is renamed.
        db.TaxRateComponents.RemoveRange(rate.Components);
        rate.Components.Clear();
        ApplyComponents(rate, request.Components);

        await AssignCategoriesAsync(db, rate, rate.BusinessId, request.Categories, ct);
        await db.SaveChangesAsync(ct);

        return Results.Ok(ToDto(rate));
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
    // whichever rate held it — the rows are deleted and recreated rather than edited,
    // because the unique index on (BusinessId, Category) would otherwise be violated
    // mid-transaction by two rows briefly holding the same category.
    //
    // Plain // rather than ///: Task-returning method, which the .NET 10 preview OpenAPI
    // comment generator mishandles. See CLAUDE.md.
    private static async Task AssignCategoriesAsync(
        AppDbContext db, TaxRate rate, Guid businessId,
        IEnumerable<string>? categories, CancellationToken ct)
    {
        var wanted = (categories ?? [])
            .Select(c => Enum.Parse<TaxCategory>(c, true))
            .Distinct()
            .ToList();

        // Everything currently pointing at this rate, plus anything pointing elsewhere that
        // this rate is about to claim.
        var affected = await db.TaxRateCategories
            .Where(m => m.TaxRateId == rate.Id || wanted.Contains(m.Category))
            .ToListAsync(ct);

        db.TaxRateCategories.RemoveRange(affected);

        foreach (var category in wanted)
        {
            db.TaxRateCategories.Add(new TaxRateCategory
            {
                BusinessId = businessId,
                Category = category,
                TaxRateId = rate.Id
            });
        }
    }

    private static async Task<IResult> DeleteAsync(Guid id, AppDbContext db, CancellationToken ct)
    {
        var rate = await db.TaxRates.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException("Tax rate not found");

        // Lines snapshot the rate they were charged at, but they also keep the id — a rate
        // still referenced is archived, never removed, or an invoice loses what it names.
        Archiving.EnsureDeletable("tax rate",
            new Dependent("labour lines", await db.JobLaborLines.CountAsync(l => l.TaxRateId == id, ct)),
            new Dependent("part lines", await db.JobPartLines.CountAsync(p => p.TaxRateId == id, ct)));

        db.TaxRates.Remove(rate);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ArchiveAsync(Guid id, AppDbContext db, CancellationToken ct)
    {
        var rate = await db.TaxRates.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException("Tax rate not found");

        // An archived rate must stop being a default, or new lines would keep picking it.
        var mappings = await db.TaxRateCategories.Where(m => m.TaxRateId == id).ToListAsync(ct);
        db.TaxRateCategories.RemoveRange(mappings);

        var result = Archiving.Archive(rate, id);
        await db.SaveChangesAsync(ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> UnarchiveAsync(Guid id, AppDbContext db, CancellationToken ct)
    {
        var rate = await db.TaxRates.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException("Tax rate not found");

        var result = Archiving.Unarchive(rate, id);
        await db.SaveChangesAsync(ct);
        return Results.Ok(result);
    }
}
