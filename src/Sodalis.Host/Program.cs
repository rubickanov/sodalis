using Sodalis.Core;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSodalisCore(out ModuleRegistry moduleRegistry);


var app = builder.Build();

app.MapGet("/health", () => Results.Ok("Healthy"));
app.MapSodalisModules();

app.Run();
