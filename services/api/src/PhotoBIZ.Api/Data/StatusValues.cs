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
        public const string PackageSelected = "PACKAGE_SELECTED";
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

    public static class Payment
    {
        public const string Cash = "CASH";
        public const string MayaCheckoutQr = "MAYA_CHECKOUT_QR";
        public const string MayaTerminalEcr = "MAYA_TERMINAL_ECR";
        public const string NotConfigured = "NOT_CONFIGURED";
        public const string Draft = "DRAFT";
        public const string Verified = "VERIFIED";
        public const string Disabled = "DISABLED";
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
