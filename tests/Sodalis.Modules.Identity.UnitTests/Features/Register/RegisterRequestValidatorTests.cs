using FluentAssertions;
using Sodalis.Modules.Identity.Features.Register;

namespace Sodalis.Modules.Identity.UnitTests.Features.Register;

public class RegisterRequestValidatorTests
{
    private readonly RegisterRequestValidator _validator = new();

    [Fact]
    public void Valid_Passes()
    {
        var result = _validator.Validate(new RegisterRequest("vasya@example.com", "validpass123"));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("@nope.com")]
    public void InvalidEmail_Fails(string email)
    {
        var result = _validator.Validate(new RegisterRequest(email, "validpass123"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterRequest.Email));
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    public void TooShortPassword_Fails(string password)
    {
        var result = _validator.Validate(new RegisterRequest("vasya@example.com", password));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterRequest.Password));
    }

    [Fact]
    public void TooLongPassword_Fails()
    {
        var result = _validator.Validate(new RegisterRequest("vasya@example.com", new string('a', 300)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterRequest.Password));
    }

    [Fact]
    public void MultipleViolations_AllReported()
    {
        var result = _validator.Validate(new RegisterRequest("bad-email", "x"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterRequest.Email));
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterRequest.Password));
    }
}
