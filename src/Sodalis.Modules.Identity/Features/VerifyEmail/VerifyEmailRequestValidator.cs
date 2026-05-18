using FluentValidation;

namespace Sodalis.Modules.Identity.Features.VerifyEmail;

public sealed class VerifyEmailRequestValidator : AbstractValidator<VerifyEmailRequest>
{
    public VerifyEmailRequestValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty()
            .MaximumLength(256);
    }
}
