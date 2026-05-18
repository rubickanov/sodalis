using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sodalis.Core;
using Sodalis.Modules.Identity.Features.Login;

namespace Sodalis.Modules.Identity.Features.Me;

public static class MeEndpoint
{
    public static void Map(IEndpointRouteBuilder routes)
    {
        routes.MapGet("/me", Handle)
            .RequireAuthorization()
            .WithName("Me")
            .WithSummary("Get the authenticated player's identity claims.")
            .WithDescription("Returns the player id, game id, and linked auth providers parsed from the access token.")
            .Produces<MeResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }

    private static IResult Handle(ClaimsPrincipal user, IGameContext gameContext)
    {
        if (!RequestContext.TryResolvePlayer(user, gameContext, out var playerId))
        {
            return Results.Problem("Malformed token.", statusCode: StatusCodes.Status401Unauthorized);
        }

        var auth = user.FindFirstValue("auth");
        var providers = string.IsNullOrEmpty(auth)
            ? Array.Empty<string>()
            : auth.Split(',', StringSplitOptions.RemoveEmptyEntries);

        return Results.Ok(new MeResponse(playerId, gameContext.GameId, providers));
    }
}

public sealed record MeResponse(Guid PlayerId, Guid GameId, IReadOnlyList<string> LinkedProviders);
