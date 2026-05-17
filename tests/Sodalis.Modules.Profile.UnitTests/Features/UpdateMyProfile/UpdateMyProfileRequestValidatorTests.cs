using Shouldly;
using Sodalis.Modules.Profile.Features.UpdateMyProfile;

namespace Sodalis.Modules.Profile.UnitTests.Features.UpdateMyProfile;

public class UpdateMyProfileRequestValidatorTests
{
    private readonly UpdateMyProfileRequestValidator _validator = new();

    [Fact]
    public void AllNull_IsValid()
    {
        var result = _validator.Validate(new UpdateMyProfileRequest(null, null));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void EmptyDisplayName_Fails()
    {
        var result = _validator.Validate(new UpdateMyProfileRequest("", null));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateMyProfileRequest.DisplayName));
    }

    [Fact]
    public void DisplayNameTooLong_Fails()
    {
        var result = _validator.Validate(new UpdateMyProfileRequest(new string('a', 65), null));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateMyProfileRequest.DisplayName));
    }

    [Fact]
    public void DisplayNameAtBoundary_IsValid()
    {
        var result = _validator.Validate(new UpdateMyProfileRequest(new string('a', 64), null));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void EmptyAvatarUrl_IsValid_AsClearSentinel()
    {
        var result = _validator.Validate(new UpdateMyProfileRequest(null, ""));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("ftp://example.com/x.png")]
    [InlineData("javascript:alert(1)")]
    [InlineData("/relative/path.png")]
    public void InvalidAvatarUrl_Fails(string url)
    {
        var result = _validator.Validate(new UpdateMyProfileRequest(null, url));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateMyProfileRequest.AvatarUrl));
    }

    [Theory]
    [InlineData("https://i.imgur.com/abc.png")]
    [InlineData("http://example.com/avatar.jpg")]
    public void ValidAvatarUrl_Passes(string url)
    {
        var result = _validator.Validate(new UpdateMyProfileRequest(null, url));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void AvatarUrlTooLong_Fails()
    {
        var url = "https://example.com/" + new string('a', 2048);
        var result = _validator.Validate(new UpdateMyProfileRequest(null, url));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateMyProfileRequest.AvatarUrl));
    }

    [Fact]
    public void BothFieldsValid_Passes()
    {
        var result = _validator.Validate(new UpdateMyProfileRequest("Pingvin", "https://cdn.example.com/x.png"));

        result.IsValid.ShouldBeTrue();
    }
}
