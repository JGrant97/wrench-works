using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.HttpResults;
using WrenchWorks.Api.Auth;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Dashboard;

public record DashboardJobDto(
    Guid Id, string Title, string Status, string Priority,
    string CustomerName, string? VehicleDisplay, DateTime? ScheduledStartUtc);
