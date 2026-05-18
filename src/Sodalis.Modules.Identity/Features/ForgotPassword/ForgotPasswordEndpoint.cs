using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sodalis.Core;
using Sodalis.Modules.Identity.Features.Login;

namespace Sodalis.Modules.Identity.Features.ForgotPassword;

public static class ForgotPasswordEndpoint
{
    public static void Map(IEndpointRouteBuilder routes)
    {
        routes.MapPost("/forgot-password", HandleAsync)
            .WithValidation<ForgotPasswordRequest>()
            .WithName("ForgotPassword")
            .WithSummary("Request a password reset email.")
            .WithDescription("Anonymous endpoint. Always returns 204 regardless of whether the email exists (anti-enumeration). If the email matches an account in the current game, a reset link is sent.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem();
    }

    private static async Task<IResult> HandleAsync(
        ForgotPasswordRequest request,
        ForgotPasswordHandler handler,
        IGameContext gameContext,
        HttpContext http,
        CancellationToken ct)
    {
        var ipAddress = RequestContext.IpAddress(http);
        await handler.HandleAsync(request, gameContext.GameId, ipAddress, ct);
        return Results.NoContent();
    }
}
