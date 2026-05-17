using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Sodalis.Modules.Profile.Features;

internal static class RequestContext
{
    public static bool TryResolvePlayerAndGame(
        ClaimsPrincipal user,
        out Guid playerId,
        out Guid gameId)
    {
        playerId = default;
        gameId = default;

        var sub = user.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var gid = user.FindFirstValue("gid");

        return sub is not null
            && gid is not null
            && Guid.TryParse(sub, out playerId)
            && Guid.TryParse(gid, out gameId);
    }
}
