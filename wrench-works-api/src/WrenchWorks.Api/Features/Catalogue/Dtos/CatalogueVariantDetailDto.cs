using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.HttpResults;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Catalogue;

/// <summary>
/// A single variant with its place in the cascade (make and model) attached.
///
/// This exists so an edit form holding only a VariantId can rebuild the whole
/// make -> model -> year -> facet selection without the client guessing. Without it the
/// picker cannot show what a vehicle already is.
/// </summary>
public record CatalogueVariantDetailDto(
    Guid Id, Guid ModelId, string ModelName, Guid MakeId, string MakeName,
    string Label, int YearFrom, int YearTo,
    string? Trim, string? BodyStyle,
    decimal? EngineDisplacementL, int? EngineCylinders,
    string FuelType, string Transmission, string? DriveType, string Market);
