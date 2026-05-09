using Microsoft.EntityFrameworkCore;

namespace PhotoBIZ.Api.Data;

public static class DatabaseMigrationExtensions
{
    public static async Task ApplyDatabaseMigrationsAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PhotoBizDbContext>();

        await dbContext.Database.MigrateAsync();
    }
}
