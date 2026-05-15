using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sodalis.Modules.Identity.Features.Login;

namespace Sodalis.Modules.Identity.Features.Refresh;

public static class RefreshEndpoint
{
    public static void Map(IEndpointRouteBuilder routes)
    {
        routes.MapPost("/auth/refresh", HandleAsync);
    }

    private static async Task<IResult> HandleAsync(
        RefreshRequest request,
        RefreshHandler handler,
        HttpContext http,
        CancellationToken ct)
    {
        var gameId = RequestContext.ResolveGameId(http);
        var userAgent = RequestContext.UserAgent(http);
        var ipAddress = RequestContext.IpAddress(http);

        var result = await handler.HandleAsync(request, gameId, userAgent, ipAddress, ct);

        if (result.Success)
            return Results.Ok(result.Response);

        return result.Compromised
            ? Results.Problem(
                title: "session_compromised",
                detail: result.Error,
                statusCode: StatusCodes.Status401Unauthorized)
            : Results.Problem(
                detail: result.Error,
                statusCode: StatusCodes.Status401Unauthorized);
    }
}
