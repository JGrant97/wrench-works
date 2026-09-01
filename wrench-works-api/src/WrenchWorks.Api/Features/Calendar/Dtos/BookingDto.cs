using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Calendar;

public record BookingDto(Guid Id, Guid ZoneId, string ZoneName, string? ZoneColor, Guid CustomerId, string CustomerName, Guid VehicleId, string? VehicleDisplay, string Title, DateTime StartUtc, DateTime EndUtc, string? Notes, string Status, Guid? JobId, DateTime CreatedAtUtc);
