using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Sodalis.Core;

public static class SodalisExtensions
{
    public static IServiceCollection AddSodalisCore(
        this IServiceCollection services,
        out ModuleRegistry moduleRegistry)
    {
        moduleRegistry = new ModuleRegistry();
        services.AddSingleton(moduleRegistry);

        // Double-registered: middleware mutates the concrete GameContext,
        // consumers (DbContexts, handlers) read through IGameContext.
        services.AddScoped<GameContext>();
        services.AddScoped<IGameContext>(sp => sp.GetRequiredService<GameContext>());

        return services;
    }

    public static IServiceCollection AddSodalisModule<TModule>(
        this IServiceCollection services,
        ModuleRegistry registry,
        IConfiguration configuration)
        where TModule : IModule, new()
    {
        var module = new TModule();
        bool isEnabled = configuration.GetValue<bool>($"Sodalis:Modules:{module.Name}:Enabled");
        if (module.IsRequired || isEnabled)
        {
            module.RegisterServices(services, configuration);
            registry.Modules.Add(module);
        }

        return services;
    }

    public static IEndpointRouteBuilder MapSodalisModules(this IEndpointRouteBuilder routes)
    {
        var registry = routes.ServiceProvider.GetRequiredService<ModuleRegistry>();
        foreach (IModule module in registry.Modules)
        {
            module.MapEndpoints(routes);
        }

        return routes;
    }

    public static IApplicationBuilder ConfigureSodalisModules(this IApplicationBuilder app)
    {
        var registry = app.ApplicationServices.GetRequiredService<ModuleRegistry>();
        foreach (IModule module in registry.Modules)
        {
            module.ConfigureMiddleware(app);
        }

        return app;
    }

    public static async Task ApplySodalisMigrationsAsync(this WebApplication app, CancellationToken ct = default)
    {
        var registry = app.Services.GetRequiredService<ModuleRegistry>();
        foreach (IModule module in registry.Modules)
        {
            await module.ApplyMigrationsAsync(app.Services, ct);
        }
    }
}
