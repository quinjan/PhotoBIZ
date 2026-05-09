var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();

var app = builder.Build();

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
