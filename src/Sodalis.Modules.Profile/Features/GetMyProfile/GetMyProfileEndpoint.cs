using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Sodalis.Modules.Profile.Features.GetMyProfile;

public static class GetMyProfileEndpoint
{
    public static void Map(IEndpointRouteBuilder routes)
    {
        routes.MapGet("/me", HandleAsync)
            .RequireAuthorization()
            .WithName("GetMyProfile")
            .WithSummary("Get the authenticated player's profile.")
            .WithDescription("Returns the player's profile in the current game. Auto-creates a default profile on first call.")
            .Produces<MyProfileResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> HandleAsync(
        ClaimsPrincipal user,
        GetMyProfileHandler handler,
        Sodalis.Core.IGameContext gameContext,
        CancellationToken ct)
    {
        if (!RequestContext.TryResolvePlayer(user, gameContext, out var playerId))
        {
            return Results.Problem("Malformed token.", statusCode: StatusCodes.Status401Unauthorized);
        }

        var response = await handler.HandleAsync(playerId, gameContext.GameId, ct);
        return Results.Ok(response);
    }
}
