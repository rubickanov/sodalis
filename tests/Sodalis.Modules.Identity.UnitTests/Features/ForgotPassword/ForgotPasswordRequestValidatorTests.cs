using Shouldly;
using Sodalis.Modules.Identity.Features.ForgotPassword;

namespace Sodalis.Modules.Identity.UnitTests.Features.ForgotPassword;

public class ForgotPasswordRequestValidatorTests
{
    private readonly ForgotPasswordRequestValidator _validator = new();

    [Fact]
    public void Valid_Passes()
    {
        var result = _validator.Validate(new ForgotPasswordRequest("user@example.com"));
        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("@nope.com")]
    public void InvalidEmail_Fails(string email)
    {
        var result = _validator.Validate(new ForgotPasswordRequest(email));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(ForgotPasswordRequest.Email));
    }

    [Fact]
    public void TooLongEmail_Fails()
    {
        var local = new string('a', 250);
        var result = _validator.Validate(new ForgotPasswordRequest($"{local}@x.com"));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(ForgotPasswordRequest.Email));
    }
}
