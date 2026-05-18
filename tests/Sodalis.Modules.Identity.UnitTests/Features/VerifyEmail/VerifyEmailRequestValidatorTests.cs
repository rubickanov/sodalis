using Shouldly;
using Sodalis.Modules.Identity.Features.VerifyEmail;

namespace Sodalis.Modules.Identity.UnitTests.Features.VerifyEmail;

public class VerifyEmailRequestValidatorTests
{
    private readonly VerifyEmailRequestValidator _validator = new();

    [Fact]
    public void Valid_Passes()
    {
        var result = _validator.Validate(new VerifyEmailRequest("some-token"));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void EmptyToken_Fails()
    {
        var result = _validator.Validate(new VerifyEmailRequest(""));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(VerifyEmailRequest.Token));
    }

    [Fact]
    public void TooLongToken_Fails()
    {
        var result = _validator.Validate(new VerifyEmailRequest(new string('a', 300)));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(VerifyEmailRequest.Token));
    }
}
