using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using WrenchWorks.Api.Auth;
using WrenchWorks.Api.Features.Common;
using WrenchWorks.Api.Middleware;
using WrenchWorks.Domain.Entities;
using WrenchWorks.Infrastructure.Persistence;
using WrenchWorks.Infrastructure.Services;

namespace WrenchWorks.Api.Features.Messaging;

public class SendMessageValidator : AbstractValidator<SendMessageRequest>
{
    public SendMessageValidator()
    {
        RuleFor(x => x.Channel).Must(c => c is "Email" or "Sms").WithMessage("Channel must be Email or Sms");
        RuleFor(x => x.To).NotEmpty().MaximumLength(320);
        RuleFor(x => x.Body).NotEmpty();
        RuleFor(x => x.Subject).NotEmpty().When(x => x.Channel == "Email");
    }
}
