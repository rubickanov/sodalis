using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sodalis.Core;

namespace Sodalis.Modules.Tenancy.ApiKeys;

public sealed class ApiKeyMiddleware(RequestDelegate next, ILogger<ApiKeyMiddleware> logger)
{
    public const string HeaderName = "X-Sodalis-Api-Key";

    public async Task InvokeAsync(HttpContext context, GameContext gameContext, ApiKeyResolver resolver)
    {
        if (!context.Request.Headers.TryGetValue(HeaderName, out var headerValues)
            || headerValues.Count == 0
            || string.IsNullOrWhiteSpace(headerValues[0]))
        {
            logger.LogWarning("API key validation failed: {Reason}", "missing_header");
            TenancyTelemetry.ApiKeyResolutionTotal.Add(1,
                new KeyValuePair<string, object?>("outcome", "invalid"),
                new KeyValuePair<string, object?>("reason", "missing_header"));
            await WriteUnauthorizedAsync(context, $"Missing '{HeaderName}' header.");
            return;
        }

        var rawKey = headerValues[0]!;

        var gameId = await resolver.ResolveGameIdAsync(rawKey, context.RequestAborted);
        if (gameId is null)
        {
            logger.LogWarning("API key validation failed: {Reason}", "invalid_key");
            TenancyTelemetry.ApiKeyResolutionTotal.Add(1,
                new KeyValuePair<string, object?>("outcome", "invalid"),
                new KeyValuePair<string, object?>("reason", "unknown_or_revoked"));
            await WriteUnauthorizedAsync(context, "Invalid or revoked API key.");
            return;
        }

        gameContext.SetGameId(gameId.Value);

        // Fire-and-forget LastUsedAt update — capture scope-bound resolver before
        // continuing, since `resolver` is scoped and goes away when request ends.
        // For MVP this is acceptable; under high load consider batching.
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = context.RequestServices
                    .GetRequiredService<IServiceScopeFactory>()
                    .CreateScope();
                var bgResolver = scope.ServiceProvider.GetRequiredService<ApiKeyResolver>();
                await bgResolver.TouchAsync(rawKey, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to update API key LastUsedAt");
            }
        });

        // Stamp GameId on every downstream log line for the rest of the request scope.
        // Provider-agnostic: Serilog's MEL provider translates BeginScope dictionaries
        // into log properties when Enrich.FromLogContext is on.
        using (logger.BeginScope(new Dictionary<string, object> { ["GameId"] = gameId.Value }))
        {
            await next(context);
        }
    }

    private static async Task WriteUnauthorizedAsync(HttpContext context, string detail)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new
        {
            type = "about:blank",
            title = "Unauthorized",
            status = 401,
            detail
        });
    }
}
