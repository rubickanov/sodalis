using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Sodalis.Modules.Identity.Features.Login;

public static class LoginEndpoint
{
    public static void Map(IEndpointRouteBuilder routes)
    {
        routes.MapPost("/auth/login", HandleAsync);
    }

    private static async Task<IResult> HandleAsync(
        LoginRequest request,
        LoginHandler handler,
        HttpContext http,
        CancellationToken ct)
    {
        // todo TEMPORARY: hardcoded gameId until API key middleware is in.
        // Real flow: middleware reads X-Sodalis-Game-Key header → looks up Game → puts GameId in IGameContext.
        var gameId = ResolveGameId(http);

        var result = await handler.HandleAsync(request, gameId, ct);

        return result.Success
            ? Results.Ok(result.Response)
            : Results.Problem(result.Error, statusCode: StatusCodes.Status401Unauthorized);
    }

    private static Guid ResolveGameId(HttpContext http)
    {
        // Stub: allow passing game id via header for manual testing.
        // Will be replaced with real API key middleware in Phase 1.
        if (http.Request.Headers.TryGetValue("X-Game-Id", out var raw)
            && Guid.TryParse(raw.ToString(), out var parsed))
        {
            return parsed;
        }
        return Guid.Parse("00000000-0000-0000-0000-000000000001");
    }
}
