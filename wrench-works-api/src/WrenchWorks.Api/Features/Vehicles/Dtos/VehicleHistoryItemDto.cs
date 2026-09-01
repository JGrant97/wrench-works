using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Features.Common;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Vehicles;

public record VehicleHistoryItemDto(Guid JobId, string Title, string Status, DateTime? ScheduledStartUtc, DateTime CreatedAtUtc, IEnumerable<string> PartsUsed, decimal LaborTotal, decimal PartsTotal);
