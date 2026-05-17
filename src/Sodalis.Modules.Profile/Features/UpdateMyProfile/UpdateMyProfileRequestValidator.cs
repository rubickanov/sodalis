using FluentValidation;

namespace Sodalis.Modules.Profile.Features.UpdateMyProfile;

public sealed class UpdateMyProfileRequestValidator : AbstractValidator<UpdateMyProfileRequest>
{
    public UpdateMyProfileRequestValidator()
    {
        When(x => x.DisplayName is not null, () =>
        {
            RuleFor(x => x.DisplayName)
                .NotEmpty()
                .MaximumLength(64);
        });

        When(x => !string.IsNullOrEmpty(x.AvatarUrl), () =>
        {
            RuleFor(x => x.AvatarUrl)
                .MaximumLength(2048)
                .Must(BeAbsoluteHttpUrl)
                .WithMessage("AvatarUrl must be an absolute http or https URL.");
        });

        When(x => x.AvatarUrl == "", () =>
        {
            // Empty string is the explicit "clear" sentinel — valid.
        });
    }

    private static bool BeAbsoluteHttpUrl(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
            return true;

        return Uri.TryCreate(raw, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
