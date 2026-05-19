using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Sodalis.Core;
using Sodalis.Modules.Identity.Auth;
using Sodalis.Modules.Identity.Features.Login;

namespace Sodalis.Modules.Identity.Features.Logout;

public static class LogoutEndpoint
{
    public static void Map(IEndpointRouteBuilder routes)
    {
        routes.MapPost("/logout", HandleAsync)
            .WithValidation<LogoutRequest>()
            .WithName("Logout")
            .WithSummary("Revoke a refresh token.")
            .WithDescription("Revokes the supplied refresh token. Always returns 204 regardless of whether the token existed (anti-enumeration).")
            .Produces(StatusCodes.Status204NoContent);
    }

    private static async Task<IResult> HandleAsync(
        LogoutRequest request,
        RefreshTokenService refreshTokens,
        IGameContext gameContext,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        using var activity = IdentityTelemetry.ActivitySource.StartActivity("identity.logout");
        activity?.SetTag("sodalis.game.id", gameContext.GameId);

        var logger = loggerFactory.CreateLogger("Sodalis.Modules.Identity.Features.Logout");
        var revoked = await refreshTokens.RevokeAsync(request.RefreshToken, gameContext.GameId, ct);

        if (revoked)
        {
            logger.LogInformation("Logout: refresh token revoked (game {GameId})", gameContext.GameId);
        }
        else
        {
            // Anti-enumeration: client sees 204 either way; we capture the event at Debug only.
            logger.LogDebug("Logout: token unknown or already revoked (game {GameId})", gameContext.GameId);
        }

        // Always 204 — never reveal whether the token existed (timing-attack mitigation).
        return Results.NoContent();
    }
}
