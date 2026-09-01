using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.HttpResults;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Catalogue;

public record CatalogueVariantDto(
    Guid Id, string Label, int YearFrom, int YearTo,
    string? Trim, string? BodyStyle,
    decimal? EngineDisplacementL, int? EngineCylinders,
    string FuelType, string Transmission, string? DriveType, string Market);
