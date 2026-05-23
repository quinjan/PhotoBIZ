using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PhotoBIZ.Api;
using PhotoBIZ.Api.Data;

var builder = WebApplication.CreateBuilder(args);

var postgresConnectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Connection string 'Postgres' is required.");

builder.Services.AddDbContext<PhotoBizDbContext>(options =>
{
    options.UseNpgsql(postgresConnectionString);
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<PhotoBizTokenHasher>();
builder.Services.AddScoped<PhotoBizAuditService>();
builder.Services.AddScoped<PhotoBizTransactionWorkflow>();
builder.Services.AddScoped<IPasswordHasher<ApplicationUser>, PasswordHasher<ApplicationUser>>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("PhotoBizLocal", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        policy.WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "photobiz.session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
        options.Events.OnValidatePrincipal = PhotoBizAuthenticationGuards.ValidatePrincipalAsync;
    });
builder.Services.AddAuthorization();
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Configuration.GetValue("Database:ApplyMigrationsOnStartup", false))
{
    await app.ApplyDatabaseMigrationsAsync();
}

await app.BootstrapDevelopmentAdminAsync();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("PhotoBizLocal");
app.UseAuthentication();
app.UseAuthorization();
app.UseHttpsRedirection();

app.MapHealthChecks("/health");

app.MapGet("/api/platform/status", () =>
    Results.Ok(new PlatformStatusResponse("PhotoBIZ.Api", "ok", "net10.0")))
    .WithName("GetPlatformStatus");

app.MapPhotoBizApi();

app.Run();

internal sealed record PlatformStatusResponse(string Service, string Status, string Runtime);

public partial class Program;
