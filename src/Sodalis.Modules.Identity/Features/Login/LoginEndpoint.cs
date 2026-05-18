using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.IdentityModel.JsonWebTokens;
using Sodalis.Core;

namespace Sodalis.Modules.Identity.Features.Login;

public static class LoginEndpoint
{
    public static void Map(IEndpointRouteBuilder routes)
    {
        routes.MapPost("/login", HandleAsync)
            .WithValidation<LoginRequest>()
            .WithName("Login")
            .WithSummary("Authenticate via auth provider.")
            .WithDescription("Authenticates an existing player or creates a new one using the specified provider (anonymous, email, ...). Returns access + refresh tokens.")
            .Produces<LoginResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> HandleAsync(
        LoginRequest request,
        LoginHandler handler,
        IGameContext gameContext,
        HttpContext http,
        CancellationToken ct)
    {
        var userAgent = RequestContext.UserAgent(http);
        var ipAddress = RequestContext.IpAddress(http);

        var result = await handler.HandleAsync(request, gameContext.GameId, userAgent, ipAddress, ct);

        return result.Success
            ? Results.Ok(result.Response)
            : Results.Problem(result.Error, statusCode: StatusCodes.Status401Unauthorized);
    }
}

internal static class RequestContext
{
    public static string? UserAgent(HttpContext http) =>
        http.Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null;

    public static string? IpAddress(HttpContext http) =>
        http.Connection.RemoteIpAddress?.ToString();

    /// <summary>
    /// Resolves the calling player from JWT claims, and verifies that the JWT's
    /// game claim matches the API-key-resolved IGameContext. A mismatch means the
    /// caller mixed a JWT from one game with an API key for another — treat as 401.
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
