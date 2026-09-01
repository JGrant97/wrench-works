using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Calendar;

// DTOs
public record CreateBookingRequest(Guid ZoneId, Guid CustomerId, Guid VehicleId, string Title, DateTime StartUtc, DateTime EndUtc, string? Notes, bool CreateJob);
