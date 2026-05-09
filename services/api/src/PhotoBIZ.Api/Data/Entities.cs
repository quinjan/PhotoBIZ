namespace PhotoBIZ.Api.Data;

public sealed class ClientAccount
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = StatusValues.ClientAccount.Active;
    public DateTimeOffset CreatedAt { get; set; }

    public ClientBoothTheme? BoothTheme { get; set; }
    public ICollection<Location> Locations { get; } = [];
    public ICollection<ApplicationUser> Users { get; } = [];
    public ICollection<Package> Packages { get; } = [];
    public ICollection<ClientSubscription> Subscriptions { get; } = [];
    public ICollection<ClientPaymentProviderConfig> PaymentProviderConfigs { get; } = [];
}

public sealed class SubscriptionPlan
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int PricePerBoothCents { get; set; }
    public string Currency { get; set; } = "PHP";
    public bool Active { get; set; } = true;

    public ICollection<ClientSubscription> ClientSubscriptions { get; } = [];
}

public sealed class ClientSubscription
{
    public Guid Id { get; set; }
    public Guid ClientAccountId { get; set; }
    public Guid SubscriptionPlanId { get; set; }
    public string Status { get; set; } = StatusValues.Subscription.Trial;
    public int ActiveBoothAllowance { get; set; }
    public DateOnly StartsOn { get; set; }
    public DateOnly? EndsOn { get; set; }
    public string? Notes { get; set; }

    public ClientAccount? ClientAccount { get; set; }
    public SubscriptionPlan? SubscriptionPlan { get; set; }
}

public sealed class ClientBoothTheme
{
    public Guid Id { get; set; }
    public Guid ClientAccountId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string ThemePreset { get; set; } = StatusValues.Theme.VintageFilm;
    public string PrimaryColor { get; set; } = "#2f6868";
    public string AccentColor { get; set; } = "#f5d27e";
    public string? BackgroundImageUrl { get; set; }
    public string? LogoUrl { get; set; }
    public string DefaultWelcomeHeadline { get; set; } = string.Empty;
    public string DefaultWelcomeSubtitle { get; set; } = string.Empty;

    public ClientAccount? ClientAccount { get; set; }
}

public sealed class ClientPaymentProviderConfig
{
    public Guid Id { get; set; }
    public Guid ClientAccountId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string IntegrationType { get; set; } = string.Empty;
    public string Status { get; set; } = StatusValues.Payment.NotConfigured;
    public string? BusinessAccountName { get; set; }
    public string? PublicKeyMasked { get; set; }
    public string? EncryptedSecretKey { get; set; }
    public string? WebhookUrl { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }

    public ClientAccount? ClientAccount { get; set; }
}

public sealed class Location
{
    public Guid Id { get; set; }
    public Guid ClientAccountId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string Status { get; set; } = StatusValues.ClientAccount.Active;

    public ClientAccount? ClientAccount { get; set; }
    public ICollection<Booth> Booths { get; } = [];
}

public sealed class ApplicationUser
{
    public Guid Id { get; set; }
    public Guid? ClientAccountId { get; set; }
    public Guid? AssignedBoothId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = StatusValues.User.ClientOwner;
    public string Status { get; set; } = StatusValues.User.Active;
    public DateTimeOffset CreatedAt { get; set; }

    public ClientAccount? ClientAccount { get; set; }
    public Booth? AssignedBooth { get; set; }
    public ICollection<Transaction> ApprovedTransactions { get; } = [];
    public ICollection<AuditLog> AuditLogs { get; } = [];
}

public sealed class Booth
{
    public Guid Id { get; set; }
    public Guid ClientAccountId { get; set; }
    public Guid LocationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Status { get; set; } = StatusValues.Booth.Active;
    public string CurrentState { get; set; } = StatusValues.Booth.Offline;
    public DateTimeOffset? LastHeartbeatAt { get; set; }
    public string? KioskTokenHash { get; set; }
    public string? AgentCredentialHash { get; set; }

    public ClientAccount? ClientAccount { get; set; }
    public Location? Location { get; set; }
    public ApplicationUser? AssignedCashier { get; set; }
    public ICollection<BoothTerminalConfig> TerminalConfigs { get; } = [];
    public ICollection<BoothPackage> BoothPackages { get; } = [];
    public ICollection<Transaction> Transactions { get; } = [];
    public ICollection<BoothSession> BoothSessions { get; } = [];
}

public sealed class BoothTerminalConfig
{
    public Guid Id { get; set; }
    public Guid BoothId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string? TerminalModel { get; set; }
    public string? TerminalReference { get; set; }
    public string? SerialOrAssetTag { get; set; }
    public string? ComPort { get; set; }
    public string Status { get; set; } = StatusValues.Payment.NotConfigured;
    public DateTimeOffset? LastConnectionTestAt { get; set; }

    public Booth? Booth { get; set; }
}

public sealed class Package
{
    public Guid Id { get; set; }
    public Guid ClientAccountId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int PriceCents { get; set; }
    public string Currency { get; set; } = "PHP";
    public int PrintCount { get; set; }
    public string PaperSize { get; set; } = string.Empty;
    public string LumaboothPresetRef { get; set; } = string.Empty;
    public bool Active { get; set; } = true;

    public ClientAccount? ClientAccount { get; set; }
    public ICollection<BoothPackage> BoothPackages { get; } = [];
    public ICollection<Transaction> Transactions { get; } = [];
}

public sealed class BoothPackage
{
    public Guid BoothId { get; set; }
    public Guid PackageId { get; set; }
    public bool Active { get; set; } = true;

    public Booth? Booth { get; set; }
    public Package? Package { get; set; }
}

public sealed class Transaction
{
    public Guid Id { get; set; }
    public Guid ClientAccountId { get; set; }
    public Guid LocationId { get; set; }
    public Guid BoothId { get; set; }
    public Guid PackageId { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public string TransactionNumber { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = StatusValues.Payment.Cash;
    public string Status { get; set; } = StatusValues.Transaction.Created;
    public int AmountCents { get; set; }
    public string Currency { get; set; } = "PHP";
    public string PackageSnapshot { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public string? FailureReason { get; set; }

    public ClientAccount? ClientAccount { get; set; }
    public Location? Location { get; set; }
    public Booth? Booth { get; set; }
    public Package? Package { get; set; }
    public ApplicationUser? ApprovedByUser { get; set; }
    public ICollection<PaymentAttempt> PaymentAttempts { get; } = [];
    public ICollection<BoothSession> BoothSessions { get; } = [];
}

public sealed class PaymentAttempt
{
    public Guid Id { get; set; }
    public Guid TransactionId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string? ProviderReference { get; set; }
    public string Status { get; set; } = StatusValues.Transaction.Created;
    public string RawPayload { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }

    public Transaction? Transaction { get; set; }
}

public sealed class BoothSession
{
    public Guid Id { get; set; }
    public Guid TransactionId { get; set; }
    public Guid BoothId { get; set; }
    public string? LumaboothSessionRef { get; set; }
    public string Status { get; set; } = StatusValues.Session.Starting;
    public string WelcomeHeadline { get; set; } = string.Empty;
    public string WelcomeSubtitle { get; set; } = string.Empty;
    public string SessionLabel { get; set; } = string.Empty;
    public string AssignedPackageIds { get; set; } = "[]";
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }

    public Transaction? Transaction { get; set; }
    public Booth? Booth { get; set; }
}

public sealed class AuditLog
{
    public Guid Id { get; set; }
    public Guid? ClientAccountId { get; set; }
    public Guid? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public string Metadata { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }

    public ClientAccount? ClientAccount { get; set; }
    public ApplicationUser? User { get; set; }
}
