using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Sodalis.Core;
using Sodalis.Modules.Identity.Auth;
using Sodalis.Modules.Identity.AuthProviders;
using Sodalis.Modules.Identity.Features.ChangePassword;
using Sodalis.Modules.Identity.Features.ForgotPassword;
using Sodalis.Modules.Identity.Features.Login;
using Sodalis.Modules.Identity.Features.Logout;
using Sodalis.Modules.Identity.Features.LogoutAll;
using Sodalis.Modules.Identity.Features.Me;
using Sodalis.Modules.Identity.Features.Refresh;
using Sodalis.Modules.Identity.Features.Register;
using Sodalis.Modules.Identity.Features.ResetPassword;
using Sodalis.Modules.Identity.Features.VerifyEmail;
using Sodalis.Modules.Identity.Persistence;

namespace Sodalis.Modules.Identity;

public sealed class IdentityModule : IModule
{
    public string Name => "Identity";
    public bool IsRequired => true;

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Sodalis")
            ?? throw new InvalidOperationException("ConnectionStrings:Sodalis is not configured.");

        services.AddDbContext<IdentityDbContext>(opts => opts
            .UseNpgsql(connectionString, npg => npg
                .MigrationsHistoryTable("__ef_migrations_history", IdentityDbContext.SchemaName)
                .MigrationsAssembly(typeof(IdentityDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention());

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.Configure<RefreshTokenSettings>(configuration.GetSection(RefreshTokenSettings.SectionName));
        services.AddSingleton<JwtIssuer>();
        services.AddSingleton<PasswordHasher>();
        services.AddScoped<RefreshTokenService>();

        // Both providers Scoped to match the most-constrained lifetime in the
        // IEnumerable<IAuthProvider> set. Mixing lifetimes works today (Anonymous
        // is stateless), but adding a provider with a captive DbContext would
        // silently break only the Scoped resolution.
        services.AddScoped<IAuthProvider, AnonymousAuthProvider>();
        services.AddScoped<IAuthProvider, EmailPasswordAuthProvider>();

        services.AddScoped<LoginHandler>();
        services.AddScoped<RefreshHandler>();
        services.AddScoped<RegisterHandler>();
        services.AddScoped<ChangePasswordHandler>();
        services.AddScoped<VerifyEmailHandler>();
        services.AddScoped<ForgotPasswordHandler>();
        services.AddScoped<ResetPasswordHandler>();

        services.AddValidatorsFromAssemblyContaining<IdentityModule>(ServiceLifetime.Singleton);

        services.ConfigureOpenTelemetryTracerProvider(t => t.AddSource(IdentityTelemetry.Name));
        services.ConfigureOpenTelemetryMeterProvider(m => m.AddMeter(IdentityTelemetry.Name));

        ConfigureJwtAuthentication(services, configuration);
    }

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var auth = routes.MapGroup("/auth").WithTags("Auth");

        LoginEndpoint.Map(auth);
        RegisterEndpoint.Map(auth);
        RefreshEndpoint.Map(auth);
        LogoutEndpoint.Map(auth);
        LogoutAllEndpoint.Map(auth);
        ChangePasswordEndpoint.Map(auth);
        VerifyEmailEndpoint.Map(auth);
        ForgotPasswordEndpoint.Map(auth);
        ResetPasswordEndpoint.Map(auth);
        MeEndpoint.Map(auth);
    }

    public async Task ApplyMigrationsAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.MigrateAsync(ct);
    }

    private static void ConfigureJwtAuthentication(IServiceCollection services, IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
            ?? throw new InvalidOperationException($"JWT settings missing at '{JwtSettings.SectionName}'.");

        if (string.IsNullOrWhiteSpace(jwt.SigningKey))
            throw new InvalidOperationException("JWT SigningKey is not configured.");

        var keyBytes = Encoding.UTF8.GetBytes(jwt.SigningKey);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opts =>
            {
                // Do NOT map short JWT claims (sub, aud, ...) to legacy SOAP URIs.
                // We want User.FindFirstValue("sub") to return the actual "sub" claim.
                opts.MapInboundClaims = false;

                opts.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        services.AddAuthorization();
    }
}
