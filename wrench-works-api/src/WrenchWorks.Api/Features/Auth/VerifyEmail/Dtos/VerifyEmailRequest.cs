using Microsoft.EntityFrameworkCore;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Auth.VerifyEmail;

public record VerifyEmailRequest(string Email, string Token);
