using Microsoft.EntityFrameworkCore;
using Sodalis.Modules.Profile.Persistence;
using ProfileEntity = Sodalis.Modules.Profile.Domain.Profile;

namespace Sodalis.Modules.Profile.Features.GetMyProfile;

public sealed class GetMyProfileHandler(ProfileDbContext db)
{
    public async Task<MyProfileResponse> HandleAsync(
        Guid playerId,
        Guid gameId,
        CancellationToken ct)
    {
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
            await db.SaveChangesAsync(ct);
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
