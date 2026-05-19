using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sodalis.Modules.Profile.Features.GetMyProfile;
using Sodalis.Modules.Profile.Persistence;
using ProfileEntity = Sodalis.Modules.Profile.Domain.Profile;

namespace Sodalis.Modules.Profile.Features.UpdateMyProfile;

public sealed class UpdateMyProfileHandler(
    ProfileDbContext db,
    ILogger<UpdateMyProfileHandler> logger)
{
    public async Task<UpdateMyProfileResult> HandleAsync(
        UpdateMyProfileRequest request,
        Guid playerId,
        Guid gameId,
        CancellationToken ct)
    {
        using var activity = ProfileTelemetry.ActivitySource.StartActivity("profile.update_my");
        activity?.SetTag("sodalis.game.id", gameId);
        activity?.SetTag("sodalis.player.id", playerId);

        var profile = await db.Profiles
            .FirstOrDefaultAsync(p => p.PlayerId == playerId && p.GameId == gameId, ct);

        var now = DateTimeOffset.UtcNow;
        bool created = false;

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
            created = true;
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

        logger.LogInformation(
            "Profile {Outcome} for player {PlayerId} (game {GameId})",
            created ? "auto-created and updated" : "updated", playerId, gameId);

        return UpdateMyProfileResult.Ok(GetMyProfileHandler.Map(profile));
    }
}
