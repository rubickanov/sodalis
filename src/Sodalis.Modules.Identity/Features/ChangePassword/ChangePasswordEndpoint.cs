using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sodalis.Core;
using Sodalis.Modules.Identity.Features.Login;

namespace Sodalis.Modules.Identity.Features.ChangePassword;

public static class ChangePasswordEndpoint
{
    public static void Map(IEndpointRouteBuilder routes)
    {
        routes.MapPost("/change-password", HandleAsync)
            .WithValidation<ChangePasswordRequest>()
            .RequireAuthorization()
            .WithName("ChangePassword")
            .WithSummary("Change the authenticated player's password.")
            .WithDescription("Verifies the current password, sets the new one, revokes ALL existing refresh tokens for the player, and returns a fresh access + refresh pair. Requires an email/password identity on the account.")
            .Produces<LoginResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> HandleAsync(
        ChangePasswordRequest request,
        ClaimsPrincipal user,
        ChangePasswordHandler handler,
        IGameContext gameContext,
        HttpContext http,
        CancellationToken ct)
    {
        if (!RequestContext.TryResolvePlayer(user, gameContext, out var playerId))
        {
            return Results.Problem("Malformed token.", statusCode: StatusCodes.Status401Unauthorized);
        }

        var userAgent = RequestContext.UserAgent(http);
        var ipAddress = RequestContext.IpAddress(http);

        var result = await handler.HandleAsync(request, playerId, gameContext.GameId, userAgent, ipAddress, ct);

        return result.Success
            ? Results.Ok(result.Response)
            : Results.Problem(result.Error, statusCode: StatusCodes.Status400BadRequest);
    }
}
