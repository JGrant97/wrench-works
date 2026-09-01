using FluentValidation;
using WrenchWorks.Api.Middleware;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;
using WrenchWorks.Infrastructure.Services;

namespace WrenchWorks.Api.Features.Auth.Register;

public record RegisterResponse(Guid UserId, Guid BusinessId, string Message);
