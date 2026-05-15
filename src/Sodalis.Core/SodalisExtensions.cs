using Microsoft.AspNetCore.Builder;
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

    public static WebApplication MapSodalisModules(this WebApplication app)
    {
        var registry = app.Services.GetRequiredService<ModuleRegistry>();
        foreach (IModule module in registry.Modules)
        {
            module.MapEndpoints(app);
        }

        return app;
    }
}
