using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Features.Common;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Vehicles;

/// <summary>
/// Everything after DisplayName is nullable because a vehicle created before the
/// catalogue existed has no VariantId — only the deprecated free-text columns. Declaring
/// these non-null was what turned such a row into a NullReferenceException and a generic
/// 500 (docs/review-findings.md finding 10). A legacy row now returns what it actually
/// has; the client renders "—" for the rest.
/// </summary>
public record VehicleDto(
    Guid Id, Guid CustomerId, string? CustomerName,
    string DisplayName,
    Guid? VariantId, int? Year,
    string? MakeName, string? ModelName,
    string? Trim, string? BodyStyle,
    decimal? EngineDisplacementL, string? FuelType, string? Transmission,
    Guid? ColourId, string? ColourName,
    string? Vin, string? Registration, string? Notes, DateTime CreatedAtUtc);
