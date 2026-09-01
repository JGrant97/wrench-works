using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Features.Common;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Inventory;

public record CreateItemRequest(string Name, string? Sku, Guid? CategoryId, decimal UnitCost, decimal? RetailPrice, int StockOnHand, int ReorderThreshold, string? CompatibilityTagsJson, bool IsConsumable = false);
