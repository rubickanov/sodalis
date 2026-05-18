using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Sodalis.Core;

namespace Sodalis.Modules.Profile.Features;

internal static class RequestContext
{
    /// <summary>
    /// Resolves the player from JWT, verifying that the JWT's `gid` claim matches
    /// the API-key-resolved IGameContext. A mismatch (JWT for game A presented with
    /// API key for game B) is treated as 401.
    /// </summary>
    public static bool TryResolvePlayer(
        ClaimsPrincipal user,
        IGameContext gameContext,
        out Guid playerId)
    {
        playerId = default;

        var sub = user.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var gid = user.FindFirstValue("gid");

        if (sub is null
            || gid is null
            || !Guid.TryParse(sub, out playerId)
            || !Guid.TryParse(gid, out var jwtGameId))
        {
            return false;
        }

        return jwtGameId == gameContext.GameId;
    }
}
