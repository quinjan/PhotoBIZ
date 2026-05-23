using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace PhotoBIZ.Api.Data;

public sealed class PhotoBizDbContext(DbContextOptions<PhotoBizDbContext> options) : DbContext(options)
{
    public DbSet<ClientAccount> ClientAccounts => Set<ClientAccount>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<ClientSubscription> ClientSubscriptions => Set<ClientSubscription>();
    public DbSet<ClientPaymentProviderConfig> ClientPaymentProviderConfigs => Set<ClientPaymentProviderConfig>();
    public DbSet<ClientMayaEcrDevice> ClientMayaEcrDevices => Set<ClientMayaEcrDevice>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<Booth> Booths => Set<Booth>();
    public DbSet<BoothAppearanceConfig> BoothAppearanceConfigs => Set<BoothAppearanceConfig>();
    public DbSet<BoothPaymentOptionAssignment> BoothPaymentOptionAssignments => Set<BoothPaymentOptionAssignment>();
    public DbSet<BoothOffer> BoothOffers => Set<BoothOffer>();
    public DbSet<PrintEntitlement> PrintEntitlements => Set<PrintEntitlement>();
    public DbSet<BoothOfferActivation> BoothOfferActivations => Set<BoothOfferActivation>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<PaymentAttempt> PaymentAttempts => Set<PaymentAttempt>();
    public DbSet<BoothSession> BoothSessions => Set<BoothSession>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureClientAccounts(modelBuilder);
        ConfigureSubscriptions(modelBuilder);
        ConfigurePaymentResources(modelBuilder);
        ConfigureUsersAndBooths(modelBuilder);
        ConfigureBoothOffers(modelBuilder);
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

    private static void ConfigurePaymentResources(ModelBuilder modelBuilder)
    {
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

        modelBuilder.Entity<ClientMayaEcrDevice>(entity =>
        {
            entity.ToTable("client_maya_ecr_devices");
            entity.HasKey(device => device.Id);
            entity.Property(device => device.DisplayName).HasMaxLength(200);
            entity.Property(device => device.DeviceId).HasMaxLength(200);
            entity.Property(device => device.Provider).HasMaxLength(80);
            entity.Property(device => device.TerminalModel).HasMaxLength(120);
            entity.Property(device => device.TerminalReference).HasMaxLength(200);
            entity.Property(device => device.SerialOrAssetTag).HasMaxLength(200);
            entity.Property(device => device.Status).HasMaxLength(40);
            entity.HasOne(device => device.ClientAccount)
                .WithMany(client => client.MayaEcrDevices)
                .HasForeignKey(device => device.ClientAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(device => new { device.ClientAccountId, device.DeviceId }).IsUnique();
            entity.HasIndex(device => new { device.ClientAccountId, device.Status });
        });
    }

    private static void ConfigureUsersAndBooths(ModelBuilder modelBuilder)
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
            entity.Property(user => user.MustChangePassword).HasDefaultValue(false);
            entity.Property(user => user.CanApproveCash).HasDefaultValue(true);
            entity.Property(user => user.CanReturnBoothToWelcome).HasDefaultValue(true);
            entity.Property(user => user.CanCancelTransaction).HasDefaultValue(true);
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
            entity.HasIndex(user => user.ClientAccountId)
                .IsUnique()
                .HasFilter("role = 'CLIENT_OWNER' AND client_account_id IS NOT NULL")
                .HasDatabaseName("ix_users_one_client_owner_per_client");
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

        modelBuilder.Entity<BoothAppearanceConfig>(entity =>
        {
            entity.ToTable("booth_appearance_configs");
            entity.HasKey(config => config.Id);
            entity.Property(config => config.ThemePreset).HasMaxLength(40);
            entity.Property(config => config.PrimaryColor).HasMaxLength(20);
            entity.Property(config => config.AccentColor).HasMaxLength(20);
            entity.Property(config => config.BackgroundImageUrl).HasMaxLength(1000);
            entity.Property(config => config.BackgroundImageDataUrl).HasColumnType("text");
            entity.Property(config => config.SessionLabel).HasMaxLength(200);
            entity.Property(config => config.DefaultWelcomeHeadline).HasMaxLength(200);
            entity.Property(config => config.DefaultWelcomeSubtitle).HasMaxLength(500);
            entity.Property(config => config.CompletionThankYouMessage).HasMaxLength(500);
            entity.HasOne(config => config.Booth)
                .WithOne(booth => booth.AppearanceConfig)
                .HasForeignKey<BoothAppearanceConfig>(config => config.BoothId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(config => config.BoothId).IsUnique();
        });

        modelBuilder.Entity<BoothPaymentOptionAssignment>(entity =>
        {
            entity.ToTable("booth_payment_option_assignments");
            entity.HasKey(assignment => assignment.Id);
            entity.Property(assignment => assignment.PaymentMethod).HasMaxLength(80);
            entity.Property(assignment => assignment.Status).HasMaxLength(40);
            entity.Property(assignment => assignment.AssignedAt).HasDefaultValueSql("now()");
            entity.HasOne(assignment => assignment.Booth)
                .WithMany(booth => booth.PaymentOptionAssignments)
                .HasForeignKey(assignment => assignment.BoothId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(assignment => assignment.ClientPaymentProviderConfig)
                .WithMany(config => config.BoothPaymentOptionAssignments)
                .HasForeignKey(assignment => assignment.ClientPaymentProviderConfigId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(assignment => assignment.ClientMayaEcrDevice)
                .WithMany(device => device.BoothPaymentOptionAssignments)
                .HasForeignKey(assignment => assignment.ClientMayaEcrDeviceId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(assignment => new { assignment.BoothId, assignment.PaymentMethod })
                .IsUnique()
                .HasFilter("client_payment_provider_config_id IS NULL AND client_maya_ecr_device_id IS NULL")
                .HasDatabaseName("ix_booth_payment_assignments_unique_builtin_method");
            entity.HasIndex(assignment => new { assignment.BoothId, assignment.PaymentMethod, assignment.ClientPaymentProviderConfigId })
                .IsUnique()
                .HasFilter("client_payment_provider_config_id IS NOT NULL")
                .HasDatabaseName("ix_booth_payment_assignments_unique_provider_config");
            entity.HasIndex(assignment => new { assignment.BoothId, assignment.PaymentMethod, assignment.ClientMayaEcrDeviceId })
                .IsUnique()
                .HasFilter("client_maya_ecr_device_id IS NOT NULL")
                .HasDatabaseName("ix_booth_payment_assignments_unique_ecr_device");
            entity.HasIndex(assignment => new { assignment.BoothId, assignment.RuntimeEnabled });
        });
    }

    private static void ConfigureBoothOffers(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BoothOffer>(entity =>
        {
            entity.ToTable("booth_offers");
            entity.HasKey(offer => offer.Id);
            entity.Property(offer => offer.Name).HasMaxLength(200);
            entity.Property(offer => offer.Description).HasMaxLength(1000);
            entity.Property(offer => offer.OfferType).HasMaxLength(60);
            entity.Property(offer => offer.Currency).HasMaxLength(3);
            entity.Property(offer => offer.IncludedPrintEntitlement).HasMaxLength(120);
            entity.Property(offer => offer.LumaboothSessionMode).HasMaxLength(200);
            entity.Property(offer => offer.CreatedAt).HasDefaultValueSql("now()");
            entity.HasOne(offer => offer.ClientAccount)
                .WithMany(client => client.BoothOffers)
                .HasForeignKey(offer => offer.ClientAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(offer => new { offer.ClientAccountId, offer.Name }).IsUnique();
            entity.HasIndex(offer => new { offer.ClientAccountId, offer.OfferType, offer.Active });
        });

        modelBuilder.Entity<PrintEntitlement>(entity =>
        {
            entity.ToTable("print_entitlements");
            entity.HasKey(entitlement => entitlement.Id);
            entity.Property(entitlement => entitlement.Name).HasMaxLength(120);
            entity.Property(entitlement => entitlement.CreatedAt).HasDefaultValueSql("now()");
            entity.HasOne(entitlement => entitlement.ClientAccount)
                .WithMany(client => client.PrintEntitlements)
                .HasForeignKey(entitlement => entitlement.ClientAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(entitlement => new { entitlement.ClientAccountId, entitlement.Name }).IsUnique();
        });

        modelBuilder.Entity<BoothOfferActivation>(entity =>
        {
            entity.ToTable("booth_offer_activations");
            entity.HasKey(activation => activation.Id);
            entity.Property(activation => activation.Status).HasMaxLength(40);
            entity.Property(activation => activation.ActivatedAt).HasDefaultValueSql("now()");
            entity.HasOne(activation => activation.Booth)
                .WithMany(booth => booth.BoothOfferActivations)
                .HasForeignKey(activation => activation.BoothId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(activation => activation.BoothOffer)
                .WithMany(offer => offer.BoothOfferActivations)
                .HasForeignKey(activation => activation.BoothOfferId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(activation => activation.BoothId)
                .IsUnique()
                .HasFilter("status = 'ACTIVE'")
                .HasDatabaseName("ix_booth_offer_activations_one_active_per_booth");
            entity.HasIndex(activation => new { activation.BoothOfferId, activation.Status });
        });
    }

    private static void ConfigureTransactions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.ToTable("transactions");
            entity.HasKey(transaction => transaction.Id);
            entity.Property(transaction => transaction.TransactionNumber).HasMaxLength(80);
            entity.Property(transaction => transaction.TransactionType).HasMaxLength(80);
            entity.Property(transaction => transaction.PaymentMethod).HasMaxLength(80);
            entity.Property(transaction => transaction.Status).HasMaxLength(60);
            entity.Property(transaction => transaction.Currency).HasMaxLength(3);
            entity.Property(transaction => transaction.OfferSnapshot).HasColumnType("jsonb");
            entity.Property(transaction => transaction.CancelledByActorType).HasMaxLength(40);
            entity.Property(transaction => transaction.CancellationSource).HasMaxLength(80);
            entity.Property(transaction => transaction.CancellationPreviousStatus).HasMaxLength(60);
            entity.Property(transaction => transaction.FailureReason).HasMaxLength(1000);
            entity.Property(transaction => transaction.CreatedAt).HasDefaultValueSql("now()");
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
            entity.HasOne(transaction => transaction.BoothOffer)
                .WithMany(offer => offer.Transactions)
                .HasForeignKey(transaction => transaction.BoothOfferId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(transaction => transaction.BoothOfferActivation)
                .WithMany(activation => activation.Transactions)
                .HasForeignKey(transaction => transaction.BoothOfferActivationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(transaction => transaction.ParentTransaction)
                .WithMany(parent => parent.AddOnTransactions)
                .HasForeignKey(transaction => transaction.ParentTransactionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(transaction => transaction.ApprovedByUser)
                .WithMany(user => user.ApprovedTransactions)
                .HasForeignKey(transaction => transaction.ApprovedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(transaction => transaction.CancelledByUser)
                .WithMany(user => user.CancelledTransactions)
                .HasForeignKey(transaction => transaction.CancelledByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(transaction => transaction.TransactionNumber).IsUnique();
            entity.HasIndex(transaction => new { transaction.ClientAccountId, transaction.Status });
            entity.HasIndex(transaction => new { transaction.BoothId, transaction.Status });
            entity.HasIndex(transaction => new { transaction.BoothOfferId, transaction.TransactionType });
            entity.HasIndex(transaction => transaction.ParentTransactionId);
            entity.HasIndex(transaction => transaction.CancelledByUserId);
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
            entity.Property(attempt => attempt.CreatedAt).HasDefaultValueSql("now()");
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
