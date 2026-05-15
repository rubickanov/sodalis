using FluentValidation;

namespace Sodalis.Modules.Identity.Features.Login;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Provider)
            .NotEmpty()
            .MaximumLength(64);

        // Note: payload shape varies per provider. Each IAuthProvider validates its own payload.
    }
}
