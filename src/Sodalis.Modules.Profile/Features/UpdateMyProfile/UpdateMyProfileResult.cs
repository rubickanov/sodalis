using Sodalis.Modules.Profile.Features.GetMyProfile;

namespace Sodalis.Modules.Profile.Features.UpdateMyProfile;

public sealed record UpdateMyProfileResult(bool Success, MyProfileResponse? Response, string? Error)
{
    public static UpdateMyProfileResult Ok(MyProfileResponse response) => new(true, response, null);
    public static UpdateMyProfileResult Failed(string error) => new(false, null, error);
}
