using Microsoft.AspNetCore.Http.HttpResults;
using WrenchWorks.Domain.Entities;

namespace WrenchWorks.Api.Features.Catalogue;

public class CatalogueEndpointHandler(ICatalogueService service) : ICatalogueEndpointHandler
{
    // Describe() is the domain's own display string, so the label a picker shows and the
    // label stamped onto Vehicle.DisplayName cannot drift apart.
    private static CatalogueVariantDto ToDto(VehicleVariant v) =>
        new(v.Id, v.Describe(), v.YearFrom, v.YearTo,
            v.Trim, v.BodyStyle, v.EngineDisplacementL, v.EngineCylinders,
            v.FuelType.ToString(), v.Transmission.ToString(),
            v.DriveType?.ToString(), v.Market.ToString());

    public async Task<Ok<List<CatalogueMakeDto>>> GetMakesAsync(CancellationToken ct)
    {
        var makes = await service.GetMakesAsync(ct);
        return TypedResults.Ok(makes.Select(m => new CatalogueMakeDto(m.Id, m.Name)).ToList());
    }

    public async Task<Ok<List<CatalogueModelDto>>> GetModelsAsync(Guid makeId, CancellationToken ct)
    {
        var models = await service.GetModelsAsync(makeId, ct);
        return TypedResults.Ok(models.Select(m => new CatalogueModelDto(m.Id, m.Name)).ToList());
    }

    public async Task<Ok<List<int>>> GetYearsAsync(Guid modelId, CancellationToken ct) =>
        TypedResults.Ok(await service.GetYearsAsync(modelId, ct));

    public async Task<Ok<List<CatalogueVariantDto>>> GetVariantsAsync(Guid modelId, int? year, CancellationToken ct)
    {
        var variants = await service.GetVariantsAsync(modelId, year, ct);
        return TypedResults.Ok(variants.Select(ToDto).ToList());
    }

    public async Task<Ok<CatalogueVariantDetailDto>> GetVariantAsync(Guid variantId, CancellationToken ct)
    {
        var v = await service.GetVariantAsync(variantId, ct);
        return TypedResults.Ok(new CatalogueVariantDetailDto(
            v.Id,
            v.ModelId, v.Model.Name,
            v.Model.MakeId, v.Model.Make.Name,
            v.Describe(), v.YearFrom, v.YearTo,
            v.Trim, v.BodyStyle,
            v.EngineDisplacementL, v.EngineCylinders,
            v.FuelType.ToString(), v.Transmission.ToString(),
            v.DriveType?.ToString(), v.Market.ToString()));
    }

    public async Task<Ok<List<CatalogueColourDto>>> GetColoursAsync(CancellationToken ct)
    {
        var colours = await service.GetColoursAsync(ct);
        return TypedResults.Ok(colours.Select(c => new CatalogueColourDto(c.Id, c.Name, c.HexCode)).ToList());
    }
}
