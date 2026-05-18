using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sodalis.Core;

namespace Sodalis.Modules.Identity.Features.ResetPassword;

public static class ResetPasswordEndpoint
{
    public static void Map(IEndpointRouteBuilder routes)
    {
        routes.MapPost("/reset-password", HandleAsync)
            .WithValidation<ResetPasswordRequest>()
            .WithName("ResetPassword")
            .WithSummary("Set a new password using a reset token from the password-reset email.")
            .WithDescription("Anonymous endpoint. Validates the token, sets the new password, revokes all existing sessions for the player, and sends a 'password changed' notification. Tokens are single-use and expire after 1 hour. Does NOT return new tokens — the user must explicitly log in.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest);
    }

    private static async Task<IResult> HandleAsync(
        ResetPasswordRequest request,
        ResetPasswordHandler handler,
        IGameContext gameContext,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(request, gameContext.GameId, ct);
        return result.Success
            ? Results.NoContent()
            : Results.Problem(result.Error, statusCode: StatusCodes.Status400BadRequest);
    }
}
