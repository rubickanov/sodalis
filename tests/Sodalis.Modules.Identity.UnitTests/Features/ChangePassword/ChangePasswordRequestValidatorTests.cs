using Shouldly;
using Sodalis.Modules.Identity.Features.ChangePassword;

namespace Sodalis.Modules.Identity.UnitTests.Features.ChangePassword;

public class ChangePasswordRequestValidatorTests
{
    private readonly ChangePasswordRequestValidator _validator = new();

    [Fact]
    public void Valid_Passes()
    {
        var result = _validator.Validate(new ChangePasswordRequest("oldpassword1", "newpassword1"));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void EmptyCurrent_Fails()
    {
        var result = _validator.Validate(new ChangePasswordRequest("", "newpassword1"));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(ChangePasswordRequest.CurrentPassword));
    }

    [Fact]
    public void EmptyNew_Fails()
    {
        var result = _validator.Validate(new ChangePasswordRequest("oldpassword1", ""));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(ChangePasswordRequest.NewPassword));
    }

    [Theory]
    [InlineData("short")]
    [InlineData("1234567")]
    public void TooShortNew_Fails(string newPassword)
    {
        var result = _validator.Validate(new ChangePasswordRequest("oldpassword1", newPassword));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(ChangePasswordRequest.NewPassword));
    }

    [Fact]
    public void TooLongNew_Fails()
    {
        var result = _validator.Validate(new ChangePasswordRequest("oldpassword1", new string('a', 300)));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(ChangePasswordRequest.NewPassword));
    }

    [Fact]
    public void TooLongCurrent_Fails()
    {
        var result = _validator.Validate(new ChangePasswordRequest(new string('a', 300), "newpassword1"));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(ChangePasswordRequest.CurrentPassword));
    }

    [Fact]
    public void NewAtMinBoundary_IsValid()
    {
        var result = _validator.Validate(new ChangePasswordRequest("oldpassword1", "12345678"));

        result.IsValid.ShouldBeTrue();
    }
}
