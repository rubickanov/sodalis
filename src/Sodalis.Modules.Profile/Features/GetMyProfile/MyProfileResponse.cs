namespace Sodalis.Modules.Profile.Features.GetMyProfile;

public sealed record MyProfileResponse(
    Guid PlayerId,
    Guid GameId,
    string DisplayName,
    string AvatarUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
