using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.IdentityModel.JsonWebTokens;

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

    private static IResult Handle(ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var gid = user.FindFirstValue("gid");
        var auth = user.FindFirstValue("auth");

        if (sub is null || gid is null
            || !Guid.TryParse(sub, out var playerId)
            || !Guid.TryParse(gid, out var gameId))
        {
            return Results.Problem("Malformed token.", statusCode: StatusCodes.Status401Unauthorized);
        }

        var providers = string.IsNullOrEmpty(auth)
            ? Array.Empty<string>()
            : auth.Split(',', StringSplitOptions.RemoveEmptyEntries);

        return Results.Ok(new MeResponse(playerId, gameId, providers));
    }
}

public sealed record MeResponse(Guid PlayerId, Guid GameId, IReadOnlyList<string> LinkedProviders);
