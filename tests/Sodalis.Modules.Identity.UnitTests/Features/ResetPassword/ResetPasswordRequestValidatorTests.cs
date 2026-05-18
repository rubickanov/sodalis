using Shouldly;
using Sodalis.Modules.Identity.Features.ResetPassword;

namespace Sodalis.Modules.Identity.UnitTests.Features.ResetPassword;

public class ResetPasswordRequestValidatorTests
{
    private readonly ResetPasswordRequestValidator _validator = new();

    [Fact]
    public void Valid_Passes()
    {
        var result = _validator.Validate(new ResetPasswordRequest("a-token", "newpassword1"));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void EmptyToken_Fails()
    {
        var result = _validator.Validate(new ResetPasswordRequest("", "newpassword1"));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(ResetPasswordRequest.Token));
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("1234567")]
    public void TooShortOrEmptyNewPassword_Fails(string newPassword)
    {
        var result = _validator.Validate(new ResetPasswordRequest("a-token", newPassword));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(ResetPasswordRequest.NewPassword));
    }

    [Fact]
    public void TooLongNewPassword_Fails()
    {
        var result = _validator.Validate(new ResetPasswordRequest("a-token", new string('a', 300)));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(ResetPasswordRequest.NewPassword));
    }

    [Fact]
    public void NewPasswordAtMinBoundary_IsValid()
    {
        var result = _validator.Validate(new ResetPasswordRequest("a-token", "12345678"));
        result.IsValid.ShouldBeTrue();
    }
}
