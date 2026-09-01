using FluentValidation;
using WrenchWorks.Api.Middleware;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;
using WrenchWorks.Infrastructure.Services;

namespace WrenchWorks.Api.Features.Auth.Register;

public record RegisterRequest(string BusinessName, string OwnerName, string Email, string Password);
