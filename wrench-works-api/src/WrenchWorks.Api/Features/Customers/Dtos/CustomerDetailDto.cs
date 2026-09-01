using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Features.Common;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Customers;

public record CustomerDetailDto(Guid Id, string Name, string? Phone, string? Email, string? Address, string? PreferredContactMethod, string? Notes, bool IsTaxExempt, string? TaxExemptionReference, IEnumerable<CustomerVehicleDto> Vehicles, IEnumerable<CustomerJobDto> RecentJobs, DateTime CreatedAtUtc);
