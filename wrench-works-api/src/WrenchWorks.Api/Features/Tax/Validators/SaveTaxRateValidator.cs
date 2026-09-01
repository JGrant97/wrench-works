using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Features.Common;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Tax;

public class SaveTaxRateValidator : AbstractValidator<SaveTaxRateRequest>
{
    public SaveTaxRateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);

        // 0–100%. Rejecting >1 catches the commonest data-entry error by far: typing "20"
        // for 20% instead of 0.2, which would otherwise charge 2000% tax.
        RuleFor(x => x.Rate)
            .InclusiveBetween(0m, 1m)
            .WithMessage("Rate must be a fraction between 0 and 1 — enter 0.2 for 20%");

        // An unrecognised category would silently apply to nothing, which looks like a
        // rate that simply does not work.
        RuleForEach(x => x.Categories)
            .Must(c => Enum.TryParse<TaxCategory>(c, true, out _))
            .WithMessage($"Category must be one of {string.Join(", ", Enum.GetNames<TaxCategory>())}");

        RuleForEach(x => x.Components).ChildRules(c =>
        {
            c.RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
            c.RuleFor(x => x.Rate).InclusiveBetween(0m, 1m);
        });
    }
}
