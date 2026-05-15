using Scalar.AspNetCore;
using Sodalis.Core;
using Sodalis.Modules.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSodalisCore(out ModuleRegistry moduleRegistry);
builder.Services.AddSodalisModule<IdentityModule>(moduleRegistry, builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

await app.ApplySodalisMigrationsAsync();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok("Healthy"));

var v1 = app.MapGroup("/api/v1");
v1.MapSodalisModules();

app.Run();

public partial class Program;
