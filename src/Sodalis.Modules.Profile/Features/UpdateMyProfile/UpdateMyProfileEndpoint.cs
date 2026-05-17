using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sodalis.Core;

namespace Sodalis.Modules.Profile.Features.UpdateMyProfile;

public static class UpdateMyProfileEndpoint
{
    public static void Map(IEndpointRouteBuilder routes)
    {
        routes.MapPatch("/me", HandleAsync)
            .WithValidation<UpdateMyProfileRequest>()
            .RequireAuthorization()
            .WithName("UpdateMyProfile")
            .WithSummary("Update the authenticated player's profile.")
            .WithDescription("Partially updates DisplayName and/or AvatarUrl. Null fields are left untouched; empty AvatarUrl clears it.")
            .Produces<GetMyProfile.MyProfileResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> HandleAsync(
        UpdateMyProfileRequest request,
        ClaimsPrincipal user,
        UpdateMyProfileHandler handler,
        CancellationToken ct)
    {
        if (!RequestContext.TryResolvePlayerAndGame(user, out var playerId, out var gameId))
        {
            return Results.Problem("Malformed token.", statusCode: StatusCodes.Status401Unauthorized);
        }

        var result = await handler.HandleAsync(request, playerId, gameId, ct);

        return result.Success
            ? Results.Ok(result.Response)
            : Results.Problem(result.Error, statusCode: StatusCodes.Status400BadRequest);
    }
}
