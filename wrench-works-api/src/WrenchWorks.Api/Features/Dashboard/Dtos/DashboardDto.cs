using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.HttpResults;
using WrenchWorks.Api.Auth;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Dashboard;

/// <summary>
/// Everything the opening screen needs, in one request.
///
/// Deliberately one endpoint rather than six: the dashboard is the first thing loaded
/// after login, and six round trips through the proxy would each pay the cookie→bearer
/// hop. It also keeps "what counts as today" decided in one place on the server.
/// </summary>
public record DashboardDto(
    IEnumerable<DashboardBookingDto> TodaysBookings,
    IEnumerable<DashboardJobDto> ActiveJobs,
    IEnumerable<StatusCountDto> JobsByStatus,
    IEnumerable<LowStockItemDto> LowStockItems,
    int OpenJobCount,
    int UnscheduledJobCount,
    decimal RevenueThisMonth,
    decimal RevenueLastMonth,
    int CustomerCount,
    int VehicleCount);
