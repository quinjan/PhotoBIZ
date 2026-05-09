using Microsoft.EntityFrameworkCore;
using PhotoBIZ.Api.Data;

var builder = WebApplication.CreateBuilder(args);

var postgresConnectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Connection string 'Postgres' is required.");

builder.Services.AddDbContext<PhotoBizDbContext>(options =>
{
    options.UseNpgsql(postgresConnectionString);
});

builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Configuration.GetValue("Database:ApplyMigrationsOnStartup", false))
{
    await app.ApplyDatabaseMigrationsAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapHealthChecks("/health");

app.MapGet("/api/platform/status", () =>
    Results.Ok(new PlatformStatusResponse("PhotoBIZ.Api", "ok", "net10.0")))
    .WithName("GetPlatformStatus");

app.Run();

internal sealed record PlatformStatusResponse(string Service, string Status, string Runtime);

public partial class Program;
