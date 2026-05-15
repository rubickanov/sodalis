using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sodalis.Modules.Identity.Auth;
using Sodalis.Modules.Identity.Features.Login;

namespace Sodalis.Modules.Identity.Features.Logout;

public static class LogoutEndpoint
{
    public static void Map(IEndpointRouteBuilder routes)
    {
        routes.MapPost("/auth/logout", HandleAsync);
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
