namespace PhotoBIZ.Api.Data;

public static class StatusValues
{
    public static class ClientAccount
    {
        public const string Active = "ACTIVE";
        public const string Suspended = "SUSPENDED";
        public const string Archived = "ARCHIVED";
    }

    public static class Subscription
    {
        public const string Trial = "TRIAL";
        public const string Active = "ACTIVE";
        public const string PastDue = "PAST_DUE";
        public const string Suspended = "SUSPENDED";
        public const string Cancelled = "CANCELLED";
    }

    public static class User
    {
        public const string ApplicationOwner = "APPLICATION_OWNER";
        public const string ClientOwner = "CLIENT_OWNER";
        public const string ClientAdmin = "CLIENT_ADMIN";
        public const string Cashier = "CASHIER";
        public const string Active = "ACTIVE";
        public const string Inactive = "INACTIVE";
    }

    public static class Booth
    {
        public const string Active = "ACTIVE";
        public const string Inactive = "INACTIVE";
        public const string Offline = "OFFLINE";
        public const string Welcome = "WELCOME";
        public const string OfferConfirmed = "OFFER_CONFIRMED";
        public const string PaymentMethodSelected = "PAYMENT_METHOD_SELECTED";
        public const string PaymentPending = "PAYMENT_PENDING";
        public const string Paid = "PAID";
        public const string StartingLumabooth = "STARTING_LUMABOOTH";
        public const string InLumaboothSession = "IN_LUMABOOTH_SESSION";
        public const string PrintingOrSharing = "PRINTING_OR_SHARING";
        public const string Completed = "COMPLETED";
        public const string ReturningToWelcome = "RETURNING_TO_WELCOME";
        public const string Error = "ERROR";
    }

    public static class Theme
    {
        public const string VintageFilm = "VINTAGE_FILM";
        public const string ModernPop = "MODERN_POP";
    }

    public static class OfferType
    {
        public const string PerSession = "PER_SESSION";
        public const string TimeUnlimited = "TIME_UNLIMITED";
        public const string SessionCount = "SESSION_COUNT";
    }

    public static class OfferActivation
    {
        public const string Active = "ACTIVE";
        public const string Inactive = "INACTIVE";
        public const string Completed = "COMPLETED";
        public const string Cancelled = "CANCELLED";
    }

    public static class PrintEntitlement
    {
        public const string TwoBySixOrOneByFour = "2 pcs 6x2 or 1 pc 6x4";
    }

    public static class LumaboothSessionMode
    {
        public const string Print = "PRINT";
        public const string Gif = "GIF";
        public const string Boomerang = "BOOMERANG";
        public const string Video = "VIDEO";
        public const string LegacySessionStandard = "SESSION_STANDARD";
    }

    public static class PaymentProvider
    {
        public const string Maya = "MAYA";
    }

    public static class PaymentMethod
    {
        public const string Cash = "CASH";
        public const string MayaCheckoutQr = "MAYA_CHECKOUT_QR";
        public const string MayaTerminalEcr = "MAYA_TERMINAL_ECR";
    }

    public static class PaymentResource
    {
        public const string NotConfigured = "NOT_CONFIGURED";
        public const string Draft = "DRAFT";
        public const string Verified = "VERIFIED";
        public const string Disabled = "DISABLED";
    }

    public static class PaymentAssignment
    {
        public const string Assigned = "ASSIGNED";
        public const string Locked = "LOCKED";
        public const string Disabled = "DISABLED";
    }

    public static class TransactionType
    {
        public const string SessionPurchase = "SESSION_PURCHASE";
        public const string PlanActivation = "PLAN_ACTIVATION";
        public const string CoveredPlanSession = "COVERED_PLAN_SESSION";
        public const string ExtraPrintAddOn = "EXTRA_PRINT_ADD_ON";
    }

    public static class Transaction
    {
        public const string Created = "CREATED";
        public const string PendingCash = "PENDING_CASH";
        public const string Paid = "PAID";
        public const string StartingSession = "STARTING_SESSION";
        public const string InSession = "IN_SESSION";
        public const string Completed = "COMPLETED";
        public const string Expired = "EXPIRED";
        public const string Cancelled = "CANCELLED";
        public const string PaymentFailed = "PAYMENT_FAILED";
        public const string SessionFailed = "SESSION_FAILED";
    }

    public static class Session
    {
        public const string Starting = "STARTING";
        public const string InSession = "IN_SESSION";
        public const string Completed = "COMPLETED";
        public const string Failed = "FAILED";
    }
}
