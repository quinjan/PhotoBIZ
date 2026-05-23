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
        public const string PaymentPending = "PAYMENT_PENDING";
        public const string Paid = "PAID";
        public const string StartingLumabooth = "STARTING_LUMABOOTH";
        public const string InLumaboothSession = "IN_LUMABOOTH_SESSION";
        public const string PrintingOrSharing = "PRINTING_OR_SHARING";
        public const string Completed = "COMPLETED";
        public const string Error = "ERROR";
    }

    public static class Theme
    {
        public const string Vintage = "VINTAGE";
        public const string CleanModern = "CLEAN_MODERN";
        public const string Pop = "POP";
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
        public const string PendingPayment = "PENDING_PAYMENT";
        public const string Inactive = "INACTIVE";
        public const string Completed = "COMPLETED";
        public const string Cancelled = "CANCELLED";
    }

    public static class PrintEntitlement
    {
        public const string TwoBySixOrOneByFour = "2 pcs 6x2 or 1 pc 6x4";
        public const string TwoBySix = "2 pcs 6x2";
        public const string OneByFour = "1 pc 6x4";
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

    public static class CancellationActor
    {
        public const string BoothUser = "BOOTH_USER";
        public const string Cashier = "CASHIER";
        public const string System = "SYSTEM";
    }

    public static class CancellationSource
    {
        public const string BoothUiPaymentOptionsBack = "BOOTH_UI_PAYMENT_OPTIONS_BACK";
        public const string BoothUiPaymentOptionsIdleTimeout = "BOOTH_UI_PAYMENT_OPTIONS_IDLE_TIMEOUT";
        public const string BoothUiWaitingForPaymentBack = "BOOTH_UI_WAITING_FOR_PAYMENT_BACK";
        public const string CashierPosCancelTransaction = "CASHIER_POS_CANCEL_TRANSACTION";
        public const string CashierPosReturnToWelcome = "CASHIER_POS_RETURN_TO_WELCOME";
        public const string SystemExtraPrintTimeout = "SYSTEM_EXTRA_PRINT_TIMEOUT";
    }

    public static class BoothUiCancelTrigger
    {
        public const string BackButton = "BACK_BUTTON";
        public const string IdleTimeout = "IDLE_TIMEOUT";
    }

    public static class Session
    {
        public const string Starting = "STARTING";
        public const string InSession = "IN_SESSION";
        public const string Completed = "COMPLETED";
        public const string Failed = "FAILED";
    }
}
