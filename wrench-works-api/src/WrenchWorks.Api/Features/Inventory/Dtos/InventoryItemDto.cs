using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Features.Common;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Inventory;

public record InventoryItemDto(Guid Id, string Name, string? Sku, Guid? CategoryId, string? CategoryName, decimal UnitCost, decimal? RetailPrice, int StockOnHand, int ReorderThreshold, bool LowStock, bool IsConsumable, DateTime CreatedAtUtc);
