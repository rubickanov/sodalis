using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
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
        HttpContext http,
        CancellationToken ct)
    {
        var gameId = RequestContext.ResolveGameId(http);
        await refreshTokens.RevokeAsync(request.RefreshToken, gameId, ct);

        // Always 204 — never reveal whether the token existed (timing-attack mitigation).
        return Results.NoContent();
    }
}
