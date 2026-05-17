using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sodalis.Core;
using Sodalis.Modules.Identity.Features.Login;

namespace Sodalis.Modules.Identity.Features.Register;

public static class RegisterEndpoint
{
    public static void Map(IEndpointRouteBuilder routes)
    {
        routes.MapPost("/register", HandleAsync)
            .WithValidation<RegisterRequest>()
            .WithName("Register")
            .WithSummary("Register a new email/password player.")
            .WithDescription("Creates a new player with the email auth provider, hashes the password, and returns access + refresh tokens.")
            .Produces<LoginResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest);
    }

    private static async Task<IResult> HandleAsync(
        RegisterRequest request,
        RegisterHandler handler,
        HttpContext http,
        CancellationToken ct)
    {
        var gameId = RequestContext.ResolveGameId(http);
        var userAgent = RequestContext.UserAgent(http);
        var ipAddress = RequestContext.IpAddress(http);

        var result = await handler.HandleAsync(request, gameId, userAgent, ipAddress, ct);

        return result.Success
            ? Results.Ok(result.Response)
            : Results.Problem(result.Error, statusCode: StatusCodes.Status400BadRequest);
    }
}
