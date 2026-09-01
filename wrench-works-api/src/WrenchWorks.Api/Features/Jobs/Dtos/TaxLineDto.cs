using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Features.Common;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Jobs;

/// <summary>One row of the tax summary, with its jurisdiction split when the rate has one.</summary>
public record TaxLineDto(string Name, decimal RatePercent, decimal Amount, IEnumerable<TaxComponentLineDto> Components);
