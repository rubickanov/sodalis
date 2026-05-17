using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Sodalis.Modules.Profile.Features.GetMyProfile;
using Sodalis.Modules.Profile.Persistence;

namespace Sodalis.Modules.Profile.Features.GetProfileById;

public static class GetProfileByIdEndpoint
{
    public static void Map(IEndpointRouteBuilder routes)
    {
        routes.MapGet("/{playerId:guid}", HandleAsync)
            .RequireAuthorization()
            .WithName("GetProfileById")
            .WithSummary("Get another player's profile in the same game.")
            .WithDescription("Returns the public profile of the player with the given id, scoped to the caller's game. 404 if not found.")
            .Produces<GetMyProfile.MyProfileResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> HandleAsync(
        Guid playerId,
        ClaimsPrincipal user,
        ProfileDbContext db,
        CancellationToken ct)
    {
        if (!RequestContext.TryResolvePlayerAndGame(user, out _, out var gameId))
        {
            return Results.Problem("Malformed token.", statusCode: StatusCodes.Status401Unauthorized);
        }

        var profile = await db.Profiles
            .FirstOrDefaultAsync(p => p.PlayerId == playerId && p.GameId == gameId, ct);

        return profile is null
            ? Results.NotFound()
            : Results.Ok(GetMyProfileHandler.Map(profile));
    }
}
