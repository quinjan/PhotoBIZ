using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PhotoBIZ.Api.Data;

public sealed class PhotoBizDbContextFactory : IDesignTimeDbContextFactory<PhotoBizDbContext>
{
    private const string DefaultConnectionString =
        "Host=localhost;Port=55432;Database=photobiz;Username=photobiz;Password=photobiz_dev_password";

    public PhotoBizDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Postgres") ??
            Environment.GetEnvironmentVariable("PHOTO_BIZ_POSTGRES") ??
            DefaultConnectionString;

        var options = new DbContextOptionsBuilder<PhotoBizDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new PhotoBizDbContext(options);
    }
}
