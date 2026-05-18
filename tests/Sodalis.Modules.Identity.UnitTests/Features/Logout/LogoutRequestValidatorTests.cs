using Shouldly;
using Sodalis.Modules.Identity.Features.Logout;

namespace Sodalis.Modules.Identity.UnitTests.Features.Logout;

public class LogoutRequestValidatorTests
{
    private readonly LogoutRequestValidator _validator = new();

    [Fact]
    public void ValidToken_Passes()
    {
        var result = _validator.Validate(new LogoutRequest("a-real-looking-refresh-token"));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void EmptyToken_Fails()
    {
        var result = _validator.Validate(new LogoutRequest(""));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(LogoutRequest.RefreshToken));
    }

    [Fact]
    public void TokenTooLong_Fails()
    {
        var result = _validator.Validate(new LogoutRequest(new string('a', 257)));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(LogoutRequest.RefreshToken));
    }

    [Fact]
    public void TokenAtBoundary_Passes()
    {
        var result = _validator.Validate(new LogoutRequest(new string('a', 256)));

        result.IsValid.ShouldBeTrue();
    }
}
