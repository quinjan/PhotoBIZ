using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using PhotoBIZ.Api.Data;
using PhotoBizTransaction = PhotoBIZ.Api.Data.Transaction;

namespace PhotoBIZ.Api.Tests.Data;

public sealed class PhotoBizModelTests
{
    [Fact]
    public void ModelIncludesCurrentMvpDataFoundationEntities()
    {
        using var dbContext = CreateDbContext();
        var model = dbContext.Model;

        AssertEntityTable<ClientAccount>(model, "client_accounts");
        AssertEntityTable<SubscriptionPlan>(model, "subscription_plans");
        AssertEntityTable<ClientSubscription>(model, "client_subscriptions");
        AssertEntityTable<ClientPaymentProviderConfig>(model, "client_payment_provider_configs");
        AssertEntityTable<ClientMayaEcrDevice>(model, "client_maya_ecr_devices");
        AssertEntityTable<Location>(model, "locations");
        AssertEntityTable<ApplicationUser>(model, "users");
        AssertEntityTable<Booth>(model, "booths");
        AssertEntityTable<BoothAppearanceConfig>(model, "booth_appearance_configs");
        AssertEntityTable<BoothPaymentOptionAssignment>(model, "booth_payment_option_assignments");
        AssertEntityTable<BoothOffer>(model, "booth_offers");
        AssertEntityTable<BoothOfferActivation>(model, "booth_offer_activations");
        AssertEntityTable<PhotoBizTransaction>(model, "transactions");
        AssertEntityTable<PaymentAttempt>(model, "payment_attempts");
        AssertEntityTable<BoothSession>(model, "booth_sessions");
        AssertEntityTable<AuditLog>(model, "audit_logs");
    }

    [Fact]
    public void ModelDoesNotExposeRetiredPackageThemeOrBoothTerminalEntities()
    {
        using var dbContext = CreateDbContext();
        var entityClrTypes = dbContext.Model.GetEntityTypes()
            .Select(entity => entity.ClrType.Name)
            .ToArray();

        Assert.DoesNotContain("Package", entityClrTypes);
        Assert.DoesNotContain("BoothPackage", entityClrTypes);
        Assert.DoesNotContain("ClientBoothTheme", entityClrTypes);
        Assert.DoesNotContain("BoothTerminalConfig", entityClrTypes);
    }

    [Fact]
    public void ClientScopedTablesExposeTenantIndexes()
    {
        using var dbContext = CreateDbContext();

        AssertHasIndex<ClientAccount>(dbContext.Model, nameof(ClientAccount.Name));
        AssertHasIndex<Location>(dbContext.Model, nameof(Location.ClientAccountId), nameof(Location.Name));
        AssertHasIndex<Booth>(dbContext.Model, nameof(Booth.ClientAccountId), nameof(Booth.Code));
        AssertHasIndex<BoothOffer>(dbContext.Model, nameof(BoothOffer.ClientAccountId), nameof(BoothOffer.Name));
        AssertHasIndex<ClientMayaEcrDevice>(dbContext.Model, nameof(ClientMayaEcrDevice.ClientAccountId), nameof(ClientMayaEcrDevice.DeviceId));
        AssertHasIndex<PhotoBizTransaction>(dbContext.Model, nameof(PhotoBizTransaction.ClientAccountId), nameof(PhotoBizTransaction.Status));
        AssertHasIndex<AuditLog>(dbContext.Model, nameof(AuditLog.ClientAccountId), nameof(AuditLog.CreatedAt));
    }

    [Fact]
    public void BoothOfferActivationEnforcesOneActiveOfferPerBooth()
    {
        using var dbContext = CreateDbContext();

        var entityType = dbContext.Model.FindEntityType(typeof(BoothOfferActivation));
        Assert.NotNull(entityType);

        var index = entityType.GetIndexes().SingleOrDefault(index =>
            index.Properties.Select(property => property.Name).SequenceEqual([nameof(BoothOfferActivation.BoothId)]) &&
            index.IsUnique);

        Assert.NotNull(index);
        Assert.Equal("status = 'ACTIVE'", index.GetFilter());
    }

    [Fact]
    public void JsonPayloadColumnsUsePostgresJsonb()
    {
        using var dbContext = CreateDbContext();

        AssertColumnType<PhotoBizTransaction>(dbContext.Model, nameof(PhotoBizTransaction.OfferSnapshot), "jsonb");
        AssertColumnType<PaymentAttempt>(dbContext.Model, nameof(PaymentAttempt.RawPayload), "jsonb");
        AssertColumnType<AuditLog>(dbContext.Model, nameof(AuditLog.Metadata), "jsonb");
    }

    [Fact]
    public void ModelCanGeneratePostgresCreateScript()
    {
        using var dbContext = CreateDbContext();

        var script = dbContext.Database.GenerateCreateScript();

        Assert.Contains("CREATE TABLE client_accounts", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE booth_offers", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE booth_offer_activations", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE booth_payment_option_assignments", script, StringComparison.Ordinal);
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
