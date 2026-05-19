using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
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
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        using var activity = IdentityTelemetry.ActivitySource.StartActivity("identity.logout_all");
        activity?.SetTag("sodalis.game.id", gameContext.GameId);

        var logger = loggerFactory.CreateLogger("Sodalis.Modules.Identity.Features.LogoutAll");

        if (!RequestContext.TryResolvePlayer(user, gameContext, out var playerId))
        {
            logger.LogWarning("LogoutAll rejected: malformed token");
            activity?.SetStatus(ActivityStatusCode.Error, "malformed_token");
            return Results.Problem("Malformed token.", statusCode: StatusCodes.Status401Unauthorized);
        }

        var count = await refreshTokens.RevokeAllForPlayerAsync(playerId, gameContext.GameId, ct);
        activity?.SetTag("sodalis.player.id", playerId);
        activity?.SetTag("sodalis.refresh.revoked_count", count);
        logger.LogInformation(
            "LogoutAll: revoked {Count} refresh token(s) for player {PlayerId}",
            count, playerId);
        return Results.NoContent();
    }
}
