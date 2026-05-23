namespace PhotoBIZ.Api.Data;

public sealed class ClientAccount
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = StatusValues.ClientAccount.Active;
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<Location> Locations { get; } = [];
    public ICollection<ApplicationUser> Users { get; } = [];
    public ICollection<BoothOffer> BoothOffers { get; } = [];
    public ICollection<PrintEntitlement> PrintEntitlements { get; } = [];
    public ICollection<ClientSubscription> Subscriptions { get; } = [];
    public ICollection<ClientPaymentProviderConfig> PaymentProviderConfigs { get; } = [];
    public ICollection<ClientMayaEcrDevice> MayaEcrDevices { get; } = [];
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

public sealed class ClientPaymentProviderConfig
{
    public Guid Id { get; set; }
    public Guid ClientAccountId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string IntegrationType { get; set; } = string.Empty;
    public string Status { get; set; } = StatusValues.PaymentResource.NotConfigured;
    public string? BusinessAccountName { get; set; }
    public string? PublicKeyMasked { get; set; }
    public string? EncryptedSecretKey { get; set; }
    public string? WebhookUrl { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }

    public ClientAccount? ClientAccount { get; set; }
    public ICollection<BoothPaymentOptionAssignment> BoothPaymentOptionAssignments { get; } = [];
}

public sealed class ClientMayaEcrDevice
{
    public Guid Id { get; set; }
    public Guid ClientAccountId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string Provider { get; set; } = StatusValues.PaymentProvider.Maya;
    public string? TerminalModel { get; set; }
    public string? TerminalReference { get; set; }
    public string? SerialOrAssetTag { get; set; }
    public string Status { get; set; } = StatusValues.PaymentResource.Draft;
    public DateTimeOffset? VerifiedAt { get; set; }

    public ClientAccount? ClientAccount { get; set; }
    public ICollection<BoothPaymentOptionAssignment> BoothPaymentOptionAssignments { get; } = [];
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
    public bool MustChangePassword { get; set; }
    public bool CanApproveCash { get; set; } = true;
    public bool CanReturnBoothToWelcome { get; set; } = true;
    public bool CanCancelTransaction { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }

    public ClientAccount? ClientAccount { get; set; }
    public Booth? AssignedBooth { get; set; }
    public ICollection<Transaction> ApprovedTransactions { get; } = [];
    public ICollection<Transaction> CancelledTransactions { get; } = [];
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
    public BoothAppearanceConfig? AppearanceConfig { get; set; }
    public ICollection<BoothPaymentOptionAssignment> PaymentOptionAssignments { get; } = [];
    public ICollection<BoothOfferActivation> BoothOfferActivations { get; } = [];
    public ICollection<Transaction> Transactions { get; } = [];
    public ICollection<BoothSession> BoothSessions { get; } = [];
}

public sealed class BoothAppearanceConfig
{
    public Guid Id { get; set; }
    public Guid BoothId { get; set; }
    public string ThemePreset { get; set; } = StatusValues.Theme.Vintage;
    public string PrimaryColor { get; set; } = "#2f6868";
    public string AccentColor { get; set; } = "#f5d27e";
    public string? BackgroundImageUrl { get; set; }
    public string? BackgroundImageDataUrl { get; set; }
    public string SessionLabel { get; set; } = string.Empty;
    public string DefaultWelcomeHeadline { get; set; } = string.Empty;
    public string DefaultWelcomeSubtitle { get; set; } = string.Empty;
    public string CompletionThankYouMessage { get; set; } = string.Empty;

    public Booth? Booth { get; set; }
}

public sealed class BoothPaymentOptionAssignment
{
    public Guid Id { get; set; }
    public Guid BoothId { get; set; }
    public Guid? ClientPaymentProviderConfigId { get; set; }
    public Guid? ClientMayaEcrDeviceId { get; set; }
    public string PaymentMethod { get; set; } = StatusValues.PaymentMethod.Cash;
    public string Status { get; set; } = StatusValues.PaymentAssignment.Assigned;
    public bool RuntimeEnabled { get; set; }
    public DateTimeOffset AssignedAt { get; set; }

    public Booth? Booth { get; set; }
    public ClientPaymentProviderConfig? ClientPaymentProviderConfig { get; set; }
    public ClientMayaEcrDevice? ClientMayaEcrDevice { get; set; }
}

public sealed class BoothOffer
{
    public Guid Id { get; set; }
    public Guid ClientAccountId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string OfferType { get; set; } = StatusValues.OfferType.PerSession;
    public int PriceCents { get; set; }
    public string Currency { get; set; } = "PHP";
    public string IncludedPrintEntitlement { get; set; } = StatusValues.PrintEntitlement.TwoBySixOrOneByFour;
    public int? DurationHours { get; set; }
    public int? SessionAllowance { get; set; }
    public bool AllowsExtraPrintAddOn { get; set; }
    public int? ExtraPrintPriceCents { get; set; }
    public string LumaboothSessionMode { get; set; } = string.Empty;
    public bool Active { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }

    public ClientAccount? ClientAccount { get; set; }
    public ICollection<BoothOfferActivation> BoothOfferActivations { get; } = [];
    public ICollection<Transaction> Transactions { get; } = [];
}

public sealed class PrintEntitlement
{
    public Guid Id { get; set; }
    public Guid ClientAccountId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    public ClientAccount? ClientAccount { get; set; }
}

public sealed class BoothOfferActivation
{
    public Guid Id { get; set; }
    public Guid BoothId { get; set; }
    public Guid BoothOfferId { get; set; }
    public string Status { get; set; } = StatusValues.OfferActivation.Active;
    public DateTimeOffset ActivatedAt { get; set; }
    public DateTimeOffset? DeactivatedAt { get; set; }
    public DateTimeOffset? StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
    public int? SessionAllowance { get; set; }
    public int SessionsUsed { get; set; }

    public Booth? Booth { get; set; }
    public BoothOffer? BoothOffer { get; set; }
    public ICollection<Transaction> Transactions { get; } = [];
}

public sealed class Transaction
{
    public Guid Id { get; set; }
    public Guid ClientAccountId { get; set; }
    public Guid LocationId { get; set; }
    public Guid BoothId { get; set; }
    public Guid BoothOfferId { get; set; }
    public Guid? BoothOfferActivationId { get; set; }
    public Guid? ParentTransactionId { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public string TransactionNumber { get; set; } = string.Empty;
    public string TransactionType { get; set; } = StatusValues.TransactionType.SessionPurchase;
    public string PaymentMethod { get; set; } = StatusValues.PaymentMethod.Cash;
    public string Status { get; set; } = StatusValues.Transaction.Created;
    public int AmountCents { get; set; }
    public string Currency { get; set; } = "PHP";
    public string OfferSnapshot { get; set; } = "{}";
    public int ExtraPrintCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public string? CancelledByActorType { get; set; }
    public Guid? CancelledByUserId { get; set; }
    public string? CancellationSource { get; set; }
    public string? CancellationPreviousStatus { get; set; }
    public DateTimeOffset? TerminalNoticeAcknowledgedAt { get; set; }
    public string? FailureReason { get; set; }

    public ClientAccount? ClientAccount { get; set; }
    public Location? Location { get; set; }
    public Booth? Booth { get; set; }
    public BoothOffer? BoothOffer { get; set; }
    public BoothOfferActivation? BoothOfferActivation { get; set; }
    public Transaction? ParentTransaction { get; set; }
    public ApplicationUser? ApprovedByUser { get; set; }
    public ApplicationUser? CancelledByUser { get; set; }
    public ICollection<Transaction> AddOnTransactions { get; } = [];
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
