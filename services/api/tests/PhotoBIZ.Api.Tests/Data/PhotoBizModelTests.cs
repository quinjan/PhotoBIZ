using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using PhotoBIZ.Api.Data;
using PhotoBizTransaction = PhotoBIZ.Api.Data.Transaction;

namespace PhotoBIZ.Api.Tests.Data;

public sealed class PhotoBizModelTests
{
    [Fact]
    public void ModelIncludesMvpDataFoundationEntities()
    {
        using var dbContext = CreateDbContext();
        var model = dbContext.Model;

        AssertEntityTable<ClientAccount>(model, "client_accounts");
        AssertEntityTable<SubscriptionPlan>(model, "subscription_plans");
        AssertEntityTable<ClientSubscription>(model, "client_subscriptions");
        AssertEntityTable<ClientBoothTheme>(model, "client_booth_themes");
        AssertEntityTable<ClientPaymentProviderConfig>(model, "client_payment_provider_configs");
        AssertEntityTable<Location>(model, "locations");
        AssertEntityTable<ApplicationUser>(model, "users");
        AssertEntityTable<Booth>(model, "booths");
        AssertEntityTable<BoothTerminalConfig>(model, "booth_terminal_configs");
        AssertEntityTable<Package>(model, "packages");
        AssertEntityTable<BoothPackage>(model, "booth_packages");
        AssertEntityTable<PhotoBizTransaction>(model, "transactions");
        AssertEntityTable<PaymentAttempt>(model, "payment_attempts");
        AssertEntityTable<BoothSession>(model, "booth_sessions");
        AssertEntityTable<AuditLog>(model, "audit_logs");
    }

    [Fact]
    public void ClientScopedTablesExposeTenantIndexes()
    {
        using var dbContext = CreateDbContext();

        AssertHasIndex<ClientAccount>(dbContext.Model, nameof(ClientAccount.Name));
        AssertHasIndex<Location>(dbContext.Model, nameof(Location.ClientAccountId), nameof(Location.Name));
        AssertHasIndex<Booth>(dbContext.Model, nameof(Booth.ClientAccountId), nameof(Booth.Code));
        AssertHasIndex<Package>(dbContext.Model, nameof(Package.ClientAccountId), nameof(Package.Name));
        AssertHasIndex<PhotoBizTransaction>(dbContext.Model, nameof(PhotoBizTransaction.ClientAccountId), nameof(PhotoBizTransaction.Status));
        AssertHasIndex<AuditLog>(dbContext.Model, nameof(AuditLog.ClientAccountId), nameof(AuditLog.CreatedAt));
    }

    [Fact]
    public void JsonPayloadColumnsUsePostgresJsonb()
    {
        using var dbContext = CreateDbContext();

        AssertColumnType<PhotoBizTransaction>(dbContext.Model, nameof(PhotoBizTransaction.PackageSnapshot), "jsonb");
        AssertColumnType<PaymentAttempt>(dbContext.Model, nameof(PaymentAttempt.RawPayload), "jsonb");
        AssertColumnType<BoothSession>(dbContext.Model, nameof(BoothSession.AssignedPackageIds), "jsonb");
        AssertColumnType<AuditLog>(dbContext.Model, nameof(AuditLog.Metadata), "jsonb");
    }

    [Fact]
    public void ModelCanGeneratePostgresCreateScript()
    {
        using var dbContext = CreateDbContext();

        var script = dbContext.Database.GenerateCreateScript();

        Assert.Contains("CREATE TABLE client_accounts", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE transactions", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE audit_logs", script, StringComparison.Ordinal);
        Assert.Contains("jsonb", script, StringComparison.Ordinal);
    }

    private static PhotoBizDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PhotoBizDbContext>()
            .UseNpgsql("Host=localhost;Port=55432;Database=photobiz_model_tests;Username=photobiz;Password=photobiz_dev_password")
            .Options;

        return new PhotoBizDbContext(options);
    }

    private static void AssertEntityTable<TEntity>(IModel model, string tableName)
    {
        var entityType = model.FindEntityType(typeof(TEntity));

        Assert.NotNull(entityType);
        Assert.Equal(tableName, entityType.GetTableName());
    }

    private static void AssertHasIndex<TEntity>(IModel model, params string[] propertyNames)
    {
        var entityType = model.FindEntityType(typeof(TEntity));

        Assert.NotNull(entityType);
        Assert.Contains(entityType.GetIndexes(), index =>
            propertyNames.SequenceEqual(index.Properties.Select(property => property.Name)));
    }

    private static void AssertColumnType<TEntity>(IModel model, string propertyName, string expectedColumnType)
    {
        var entityType = model.FindEntityType(typeof(TEntity));
        Assert.NotNull(entityType);

        var property = entityType.FindProperty(propertyName);

        Assert.NotNull(property);
        Assert.Equal(expectedColumnType, property.GetColumnType());
    }
}
