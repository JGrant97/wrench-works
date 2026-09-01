using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Features.Common;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Jobs;

public record JobListItemDto(Guid Id, string Title, string Status, string Priority, string CustomerName, string? VehicleDisplay, string? ZoneName, DateTime? ScheduledStartUtc, decimal LaborTotal, decimal PartsTotal, DateTime CreatedAtUtc);
