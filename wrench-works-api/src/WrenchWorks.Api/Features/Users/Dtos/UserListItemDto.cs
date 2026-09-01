using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;
using WrenchWorks.Infrastructure.Services;

namespace WrenchWorks.Api.Features.Users;

public record UserListItemDto(Guid Id, Guid BusinessUserId, string Name, string Email, string Status, IEnumerable<string> Roles, DateTime CreatedAtUtc);
