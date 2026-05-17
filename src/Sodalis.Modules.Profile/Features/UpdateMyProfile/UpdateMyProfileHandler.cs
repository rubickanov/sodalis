using Microsoft.EntityFrameworkCore;
using Sodalis.Modules.Profile.Features.GetMyProfile;
using Sodalis.Modules.Profile.Persistence;
using ProfileEntity = Sodalis.Modules.Profile.Domain.Profile;

namespace Sodalis.Modules.Profile.Features.UpdateMyProfile;

public sealed class UpdateMyProfileHandler(ProfileDbContext db)
{
    public async Task<UpdateMyProfileResult> HandleAsync(
        UpdateMyProfileRequest request,
        Guid playerId,
        Guid gameId,
        CancellationToken ct)
    {
        var profile = await db.Profiles
            .FirstOrDefaultAsync(p => p.PlayerId == playerId && p.GameId == gameId, ct);

        var now = DateTimeOffset.UtcNow;

        if (profile is null)
        {
            profile = new ProfileEntity
            {
                PlayerId = playerId,
                GameId = gameId,
                DisplayName = GetMyProfileHandler.DefaultDisplayName(playerId),
                AvatarUrl = "",
                CreatedAt = now,
                UpdatedAt = now
            };
            db.Profiles.Add(profile);
        }

        if (request.DisplayName is not null)
        {
            profile.DisplayName = request.DisplayName;
        }

        if (request.AvatarUrl is not null)
        {
            profile.AvatarUrl = request.AvatarUrl;
        }

        profile.UpdatedAt = now;
        await db.SaveChangesAsync(ct);

        return UpdateMyProfileResult.Ok(GetMyProfileHandler.Map(profile));
    }
}
