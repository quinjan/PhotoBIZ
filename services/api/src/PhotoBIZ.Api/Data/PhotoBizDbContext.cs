using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace PhotoBIZ.Api.Data;

public sealed class PhotoBizDbContext(DbContextOptions<PhotoBizDbContext> options) : DbContext(options)
{
    public DbSet<ClientAccount> ClientAccounts => Set<ClientAccount>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<ClientSubscription> ClientSubscriptions => Set<ClientSubscription>();
    public DbSet<ClientBoothTheme> ClientBoothThemes => Set<ClientBoothTheme>();
    public DbSet<ClientPaymentProviderConfig> ClientPaymentProviderConfigs => Set<ClientPaymentProviderConfig>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<Booth> Booths => Set<Booth>();
    public DbSet<BoothTerminalConfig> BoothTerminalConfigs => Set<BoothTerminalConfig>();
    public DbSet<Package> Packages => Set<Package>();
    public DbSet<BoothPackage> BoothPackages => Set<BoothPackage>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<PaymentAttempt> PaymentAttempts => Set<PaymentAttempt>();
    public DbSet<BoothSession> BoothSessions => Set<BoothSession>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureClientAccounts(modelBuilder);
        ConfigureSubscriptions(modelBuilder);
        ConfigureThemeAndPaymentConfigs(modelBuilder);
        ConfigureUsersAndOperations(modelBuilder);
        ConfigureTransactions(modelBuilder);

        ApplySnakeCaseNames(modelBuilder);
    }

    private static void ConfigureClientAccounts(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ClientAccount>(entity =>
        {
            entity.ToTable("client_accounts");
            entity.HasKey(client => client.Id);
            entity.Property(client => client.Name).HasMaxLength(200);
            entity.Property(client => client.Status).HasMaxLength(40);
            entity.Property(client => client.CreatedAt).HasDefaultValueSql("now()");
            entity.HasIndex(client => client.Name);
        });

        modelBuilder.Entity<Location>(entity =>
        {
            entity.ToTable("locations");
            entity.HasKey(location => location.Id);
            entity.Property(location => location.Name).HasMaxLength(200);
            entity.Property(location => location.Address).HasMaxLength(500);
            entity.Property(location => location.Status).HasMaxLength(40);
            entity.HasOne(location => location.ClientAccount)
                .WithMany(client => client.Locations)
                .HasForeignKey(location => location.ClientAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(location => new { location.ClientAccountId, location.Name }).IsUnique();
        });
    }

    private static void ConfigureSubscriptions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SubscriptionPlan>(entity =>
        {
            entity.ToTable("subscription_plans");
            entity.HasKey(plan => plan.Id);
            entity.Property(plan => plan.Name).HasMaxLength(120);
            entity.Property(plan => plan.Currency).HasMaxLength(3);
            entity.HasIndex(plan => plan.Name).IsUnique();
        });

        modelBuilder.Entity<ClientSubscription>(entity =>
        {
            entity.ToTable("client_subscriptions");
            entity.HasKey(subscription => subscription.Id);
            entity.Property(subscription => subscription.Status).HasMaxLength(40);
            entity.Property(subscription => subscription.Notes).HasMaxLength(1000);
            entity.HasOne(subscription => subscription.ClientAccount)
                .WithMany(client => client.Subscriptions)
                .HasForeignKey(subscription => subscription.ClientAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(subscription => subscription.SubscriptionPlan)
                .WithMany(plan => plan.ClientSubscriptions)
                .HasForeignKey(subscription => subscription.SubscriptionPlanId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(subscription => new { subscription.ClientAccountId, subscription.Status });
        });
    }

    private static void ConfigureThemeAndPaymentConfigs(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ClientBoothTheme>(entity =>
        {
            entity.ToTable("client_booth_themes");
            entity.HasKey(theme => theme.Id);
            entity.Property(theme => theme.DisplayName).HasMaxLength(200);
            entity.Property(theme => theme.ThemePreset).HasMaxLength(40);
            entity.Property(theme => theme.PrimaryColor).HasMaxLength(20);
            entity.Property(theme => theme.AccentColor).HasMaxLength(20);
            entity.Property(theme => theme.BackgroundImageUrl).HasMaxLength(1000);
            entity.Property(theme => theme.LogoUrl).HasMaxLength(1000);
            entity.Property(theme => theme.DefaultWelcomeHeadline).HasMaxLength(200);
            entity.Property(theme => theme.DefaultWelcomeSubtitle).HasMaxLength(500);
            entity.HasOne(theme => theme.ClientAccount)
                .WithOne(client => client.BoothTheme)
                .HasForeignKey<ClientBoothTheme>(theme => theme.ClientAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(theme => theme.ClientAccountId).IsUnique();
        });

        modelBuilder.Entity<ClientPaymentProviderConfig>(entity =>
        {
            entity.ToTable("client_payment_provider_configs");
            entity.HasKey(config => config.Id);
            entity.Property(config => config.Provider).HasMaxLength(80);
            entity.Property(config => config.IntegrationType).HasMaxLength(80);
            entity.Property(config => config.Status).HasMaxLength(40);
            entity.Property(config => config.BusinessAccountName).HasMaxLength(200);
            entity.Property(config => config.PublicKeyMasked).HasMaxLength(200);
            entity.Property(config => config.EncryptedSecretKey).HasMaxLength(2000);
            entity.Property(config => config.WebhookUrl).HasMaxLength(1000);
            entity.HasOne(config => config.ClientAccount)
                .WithMany(client => client.PaymentProviderConfigs)
                .HasForeignKey(config => config.ClientAccountId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_payment_configs_client_account");
            entity.HasIndex(config => new { config.ClientAccountId, config.Provider, config.IntegrationType })
                .IsUnique()
                .HasDatabaseName("ix_payment_configs_client_provider_type");
        });
    }

    private static void ConfigureUsersAndOperations(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Name).HasMaxLength(200);
            entity.Property(user => user.Email).HasMaxLength(320);
            entity.Property(user => user.PasswordHash).HasMaxLength(1000);
            entity.Property(user => user.Role).HasMaxLength(60);
            entity.Property(user => user.Status).HasMaxLength(40);
            entity.Property(user => user.CreatedAt).HasDefaultValueSql("now()");
            entity.HasOne(user => user.ClientAccount)
                .WithMany(client => client.Users)
                .HasForeignKey(user => user.ClientAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(user => user.AssignedBooth)
                .WithOne(booth => booth.AssignedCashier)
                .HasForeignKey<ApplicationUser>(user => user.AssignedBoothId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(user => user.Email).IsUnique();
            entity.HasIndex(user => new { user.ClientAccountId, user.Role });
        });

        modelBuilder.Entity<Booth>(entity =>
        {
            entity.ToTable("booths");
            entity.HasKey(booth => booth.Id);
            entity.Property(booth => booth.Name).HasMaxLength(200);
            entity.Property(booth => booth.Code).HasMaxLength(80);
            entity.Property(booth => booth.Status).HasMaxLength(40);
            entity.Property(booth => booth.CurrentState).HasMaxLength(60);
            entity.Property(booth => booth.KioskTokenHash).HasMaxLength(1000);
            entity.Property(booth => booth.AgentCredentialHash).HasMaxLength(1000);
            entity.HasOne(booth => booth.ClientAccount)
                .WithMany()
                .HasForeignKey(booth => booth.ClientAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(booth => booth.Location)
                .WithMany(location => location.Booths)
                .HasForeignKey(booth => booth.LocationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(booth => new { booth.ClientAccountId, booth.Code }).IsUnique();
            entity.HasIndex(booth => new { booth.ClientAccountId, booth.Status });
            entity.HasIndex(booth => new { booth.ClientAccountId, booth.CurrentState });
        });

        modelBuilder.Entity<BoothTerminalConfig>(entity =>
        {
            entity.ToTable("booth_terminal_configs");
            entity.HasKey(config => config.Id);
            entity.Property(config => config.Provider).HasMaxLength(80);
            entity.Property(config => config.TerminalModel).HasMaxLength(120);
            entity.Property(config => config.TerminalReference).HasMaxLength(200);
            entity.Property(config => config.SerialOrAssetTag).HasMaxLength(200);
            entity.Property(config => config.ComPort).HasMaxLength(40);
            entity.Property(config => config.Status).HasMaxLength(40);
            entity.HasOne(config => config.Booth)
                .WithMany(booth => booth.TerminalConfigs)
                .HasForeignKey(config => config.BoothId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(config => new { config.BoothId, config.Provider }).IsUnique();
        });

        modelBuilder.Entity<Package>(entity =>
        {
            entity.ToTable("packages");
            entity.HasKey(package => package.Id);
            entity.Property(package => package.Name).HasMaxLength(200);
            entity.Property(package => package.Description).HasMaxLength(1000);
            entity.Property(package => package.Currency).HasMaxLength(3);
            entity.Property(package => package.PaperSize).HasMaxLength(80);
            entity.Property(package => package.LumaboothPresetRef).HasMaxLength(200);
            entity.HasOne(package => package.ClientAccount)
                .WithMany(client => client.Packages)
                .HasForeignKey(package => package.ClientAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(package => new { package.ClientAccountId, package.Name }).IsUnique();
        });

        modelBuilder.Entity<BoothPackage>(entity =>
        {
            entity.ToTable("booth_packages");
            entity.HasKey(boothPackage => new { boothPackage.BoothId, boothPackage.PackageId });
            entity.HasOne(boothPackage => boothPackage.Booth)
                .WithMany(booth => booth.BoothPackages)
                .HasForeignKey(boothPackage => boothPackage.BoothId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(boothPackage => boothPackage.Package)
                .WithMany(package => package.BoothPackages)
                .HasForeignKey(boothPackage => boothPackage.PackageId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureTransactions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.ToTable("transactions");
            entity.HasKey(transaction => transaction.Id);
            entity.Property(transaction => transaction.TransactionNumber).HasMaxLength(80);
            entity.Property(transaction => transaction.PaymentMethod).HasMaxLength(80);
            entity.Property(transaction => transaction.Status).HasMaxLength(60);
            entity.Property(transaction => transaction.Currency).HasMaxLength(3);
            entity.Property(transaction => transaction.PackageSnapshot).HasColumnType("jsonb");
            entity.Property(transaction => transaction.FailureReason).HasMaxLength(1000);
            entity.HasOne(transaction => transaction.ClientAccount)
                .WithMany()
                .HasForeignKey(transaction => transaction.ClientAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(transaction => transaction.Location)
                .WithMany()
                .HasForeignKey(transaction => transaction.LocationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(transaction => transaction.Booth)
                .WithMany(booth => booth.Transactions)
                .HasForeignKey(transaction => transaction.BoothId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(transaction => transaction.Package)
                .WithMany(package => package.Transactions)
                .HasForeignKey(transaction => transaction.PackageId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(transaction => transaction.ApprovedByUser)
                .WithMany(user => user.ApprovedTransactions)
                .HasForeignKey(transaction => transaction.ApprovedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(transaction => transaction.TransactionNumber).IsUnique();
            entity.HasIndex(transaction => new { transaction.ClientAccountId, transaction.Status });
            entity.HasIndex(transaction => new { transaction.BoothId, transaction.Status });
            entity.HasIndex(transaction => transaction.ExpiresAt);
        });

        modelBuilder.Entity<PaymentAttempt>(entity =>
        {
            entity.ToTable("payment_attempts");
            entity.HasKey(attempt => attempt.Id);
            entity.Property(attempt => attempt.Provider).HasMaxLength(80);
            entity.Property(attempt => attempt.ProviderReference).HasMaxLength(200);
            entity.Property(attempt => attempt.Status).HasMaxLength(60);
            entity.Property(attempt => attempt.RawPayload).HasColumnType("jsonb");
            entity.HasOne(attempt => attempt.Transaction)
                .WithMany(transaction => transaction.PaymentAttempts)
                .HasForeignKey(attempt => attempt.TransactionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(attempt => new { attempt.Provider, attempt.ProviderReference });
        });

        modelBuilder.Entity<BoothSession>(entity =>
        {
            entity.ToTable("booth_sessions");
            entity.HasKey(session => session.Id);
            entity.Property(session => session.LumaboothSessionRef).HasMaxLength(200);
            entity.Property(session => session.Status).HasMaxLength(60);
            entity.Property(session => session.WelcomeHeadline).HasMaxLength(200);
            entity.Property(session => session.WelcomeSubtitle).HasMaxLength(500);
            entity.Property(session => session.SessionLabel).HasMaxLength(200);
            entity.Property(session => session.AssignedPackageIds).HasColumnType("jsonb");
            entity.HasOne(session => session.Transaction)
                .WithMany(transaction => transaction.BoothSessions)
                .HasForeignKey(session => session.TransactionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(session => session.Booth)
                .WithMany(booth => booth.BoothSessions)
                .HasForeignKey(session => session.BoothId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(session => new { session.BoothId, session.Status });
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(log => log.Id);
            entity.Property(log => log.Action).HasMaxLength(160);
            entity.Property(log => log.EntityType).HasMaxLength(120);
            entity.Property(log => log.Metadata).HasColumnType("jsonb");
            entity.Property(log => log.CreatedAt).HasDefaultValueSql("now()");
            entity.HasOne(log => log.ClientAccount)
                .WithMany()
                .HasForeignKey(log => log.ClientAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(log => log.User)
                .WithMany(user => user.AuditLogs)
                .HasForeignKey(log => log.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(log => new { log.ClientAccountId, log.CreatedAt });
            entity.HasIndex(log => new { log.EntityType, log.EntityId });
        });
    }

    private static void ApplySnakeCaseNames(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));
            }

            foreach (var key in entity.GetKeys())
            {
                key.SetName(ToSnakeCase(key.GetName() ?? string.Empty));
            }

            foreach (var foreignKey in entity.GetForeignKeys())
            {
                foreignKey.SetConstraintName(ToSnakeCase(foreignKey.GetConstraintName() ?? string.Empty));
            }

            foreach (var index in entity.GetIndexes())
            {
                index.SetDatabaseName(ToSnakeCase(index.GetDatabaseName() ?? string.Empty));
            }
        }
    }

    private static string ToSnakeCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var result = new List<char>(value.Length + 8);

        for (var i = 0; i < value.Length; i++)
        {
            var current = value[i];

            if (char.IsUpper(current))
            {
                if (i > 0 && value[i - 1] != '_' && (!char.IsUpper(value[i - 1]) || (i + 1 < value.Length && char.IsLower(value[i + 1]))))
                {
                    result.Add('_');
                }

                result.Add(char.ToLower(current, CultureInfo.InvariantCulture));
                continue;
            }

            result.Add(current);
        }

        return new string(result.ToArray());
    }
}
