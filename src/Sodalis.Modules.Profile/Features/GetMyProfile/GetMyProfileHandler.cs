using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Sodalis.Modules.Profile.Persistence;
using ProfileEntity = Sodalis.Modules.Profile.Domain.Profile;

namespace Sodalis.Modules.Profile.Features.GetMyProfile;

public sealed class GetMyProfileHandler(
    ProfileDbContext db,
    ILogger<GetMyProfileHandler> logger)
{
    public async Task<MyProfileResponse> HandleAsync(
        Guid playerId,
        Guid gameId,
        CancellationToken ct)
    {
        using var activity = ProfileTelemetry.ActivitySource.StartActivity("profile.get_my");
        activity?.SetTag("sodalis.game.id", gameId);
        activity?.SetTag("sodalis.player.id", playerId);

        var profile = await db.Profiles
            .FirstOrDefaultAsync(p => p.PlayerId == playerId && p.GameId == gameId, ct);

        if (profile is null)
        {
            var now = DateTimeOffset.UtcNow;
            profile = new ProfileEntity
            {
                PlayerId = playerId,
                GameId = gameId,
                DisplayName = DefaultDisplayName(playerId),
                AvatarUrl = "",
                CreatedAt = now,
                UpdatedAt = now
            };
            db.Profiles.Add(profile);
            try
            {
                await db.SaveChangesAsync(ct);

                activity?.SetTag("sodalis.profile.auto_created", true);
                logger.LogInformation(
                    "Profile auto-created for player {PlayerId} (game {GameId})",
                    playerId, gameId);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                // Concurrent request inserted the profile first; re-read it.
                db.Entry(profile).State = EntityState.Detached;
                profile = await db.Profiles
                    .FirstAsync(p => p.PlayerId == playerId && p.GameId == gameId, ct);
            }
        }

        return Map(profile);
    }

    internal static MyProfileResponse Map(ProfileEntity p) => new(
        p.PlayerId,
        p.GameId,
        p.DisplayName,
        p.AvatarUrl,
        p.CreatedAt,
        p.UpdatedAt);

    internal static string DefaultDisplayName(Guid playerId) =>
        $"Player-{playerId:N}"[..13];
}
