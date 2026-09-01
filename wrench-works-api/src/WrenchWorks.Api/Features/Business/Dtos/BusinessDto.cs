using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Business;

public record BusinessDto(Guid Id, string Name, string? Address, string? Phone, string Timezone, string Currency, string? LogoUrl, string? WorkingHoursJson, bool PricesIncludeTax, string? TaxRegistrationNumber, string TaxLabel, DateTime CreatedAtUtc);
