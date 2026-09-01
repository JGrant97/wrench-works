using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Business;

public class UpdateBusinessValidator : AbstractValidator<UpdateBusinessRequest>
{
    public UpdateBusinessValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Timezone).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Currency)
            .NotEmpty()
            .Must(SupportedCurrencies.IsSupported)
            .WithMessage($"Currency must be one of {string.Join(", ", SupportedCurrencies.Codes)}");
    }
}
