using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Features.Common;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Jobs;

public record PartLineDto(Guid Id, Guid InventoryItemId, string ItemName, string? Sku, decimal Quantity, decimal UnitPrice, decimal Total, decimal TaxRatePercent, decimal TaxAmount);
