using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sodalis.Core;
using Sodalis.Modules.Identity.Auth;
using Sodalis.Modules.Identity.Features.Login;

namespace Sodalis.Modules.Identity.Features.LogoutAll;

public static class LogoutAllEndpoint
{
    public static void Map(IEndpointRouteBuilder routes)
    {
        routes.MapPost("/logout-all", HandleAsync)
            .RequireAuthorization()
            .WithName("LogoutAll")
            .WithSummary("Revoke ALL refresh tokens for the authenticated player.")
            .WithDescription("Kills every active session for the calling player in the current game. Always returns 204.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> HandleAsync(
        ClaimsPrincipal user,
        RefreshTokenService refreshTokens,
        IGameContext gameContext,
        CancellationToken ct)
    {
        if (!RequestContext.TryResolvePlayer(user, gameContext, out var playerId))
        {
            return Results.Problem("Malformed token.", statusCode: StatusCodes.Status401Unauthorized);
        }

        await refreshTokens.RevokeAllForPlayerAsync(playerId, gameContext.GameId, ct);
        return Results.NoContent();
    }
}
