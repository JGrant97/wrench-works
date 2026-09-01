using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Features.Common;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Jobs;

public record JobDetailDto(
    Guid Id, string Title, string Status, string Priority,
    Guid CustomerId, string CustomerName,
    Guid VehicleId, string? VehicleDisplay,
    Guid? ZoneId, string? ZoneName,
    string? InternalNotes, string? CustomerNotes,
    DateTime? ScheduledStartUtc, DateTime? ScheduledEndUtc,
    IEnumerable<LaborLineDto> LaborLines,
    IEnumerable<PartLineDto> PartLines,
    decimal LaborTotal, decimal PartsTotal, decimal GrandTotal,
    // Net excludes tax, Gross includes it. With tax-inclusive pricing GrandTotal == Gross
    // and the labour/parts totals are already gross, which is why SubTotal is stated
    // separately rather than left for the client to derive.
    decimal SubTotal, decimal TaxTotal, string TaxLabel, bool PricesIncludeTax,
    bool CustomerIsTaxExempt,
    IEnumerable<TaxLineDto> TaxBreakdown,
    DateTime CreatedAtUtc);
