using Microsoft.EntityFrameworkCore;
using PhotoBIZ.Api.Data;
using PhotoBIZ.Worker;

var builder = Host.CreateApplicationBuilder(args);
var postgresConnectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Connection string 'Postgres' is required.");

builder.Services.AddDbContext<PhotoBizDbContext>(options =>
{
    options.UseNpgsql(postgresConnectionString);
});
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
