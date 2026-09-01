using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.HttpResults;
using WrenchWorks.Api.Auth;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Dashboard;

public record DashboardBookingDto(
    Guid Id, string Title, DateTime StartUtc, DateTime EndUtc,
    string CustomerName, string? VehicleDisplay, string ZoneName, string Status, Guid? JobId);
