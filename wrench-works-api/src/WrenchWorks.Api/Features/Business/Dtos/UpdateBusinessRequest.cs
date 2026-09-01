using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Business;

public record UpdateBusinessRequest(string Name, string? Address, string? Phone, string Timezone, string Currency, string? WorkingHoursJson, bool PricesIncludeTax = false, string? TaxRegistrationNumber = null, string? TaxLabel = null);
