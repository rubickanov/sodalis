using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sodalis.Core;

namespace Sodalis.Modules.Identity.Features.VerifyEmail;

public static class VerifyEmailEndpoint
{
    public static void Map(IEndpointRouteBuilder routes)
    {
        routes.MapPost("/verify-email", HandleAsync)
            .WithValidation<VerifyEmailRequest>()
            .WithName("VerifyEmail")
            .WithSummary("Verify an email address using the token from the verification email.")
            .WithDescription("Anonymous endpoint. Marks the associated email identity as verified. Tokens are single-use and expire after 24h.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest);
    }

    private static async Task<IResult> HandleAsync(
        VerifyEmailRequest request,
        VerifyEmailHandler handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(request, ct);
        return result.Success
            ? Results.NoContent()
            : Results.Problem(result.Error, statusCode: StatusCodes.Status400BadRequest);
    }
}
