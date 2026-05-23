import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { BoothStageConfig, BoothStageScreenState } from '@photobiz/booth-stage';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatSnackBar } from '@angular/material/snack-bar';
import { firstValueFrom, interval } from 'rxjs';

export type Session = {
  readonly userId: string;
  readonly name: string;
  readonly email: string;
  readonly role: string;
  readonly clientAccountId: string | null;
  readonly assignedBoothId: string | null;
  readonly mustChangePassword: boolean;
  readonly canApproveCash: boolean;
  readonly canReturnBoothToWelcome: boolean;
  readonly canCancelTransaction: boolean;
};

export type ClientSummary = { readonly id: string; readonly name: string; readonly status: string };
export type SubscriptionSummary = {
  readonly id: string;
  readonly clientAccountId: string;
  readonly subscriptionPlanId: string;
  readonly status: string;
  readonly activeBoothAllowance: number;
};
export type SubscriptionPlanSummary = {
  readonly id: string;
  readonly name: string;
  readonly pricePerBoothCents: number;
  readonly currency: string;
  readonly active: boolean;
};
export type UserSummary = {
  readonly id: string;
  readonly clientAccountId: string | null;
  readonly name: string;
  readonly email: string;
  readonly role: string;
  readonly status: string;
  readonly assignedBoothId: string | null;
  readonly canApproveCash: boolean;
  readonly canReturnBoothToWelcome: boolean;
  readonly canCancelTransaction: boolean;
};
export type LocationSummary = {
  readonly id: string;
  readonly clientAccountId: string;
  readonly name: string;
  readonly address: string | null;
  readonly status: string;
};
export type BoothSummary = {
  readonly id: string;
  readonly clientAccountId: string;
  readonly locationId: string;
  readonly name: string;
  readonly code: string;
  readonly status: string;
  readonly currentState: string;
  readonly lastHeartbeatAt: string | null;
};
export type OfferSummary = {
  readonly id: string;
  readonly clientAccountId: string;
  readonly name: string;
  readonly description: string | null;
  readonly offerType: string;
  readonly priceCents: number;
  readonly currency: string;
  readonly includedPrintEntitlement: string;
  readonly durationHours: number | null;
  readonly sessionAllowance: number | null;
  readonly allowsExtraPrintAddOn: boolean;
  readonly extraPrintPriceCents: number | null;
  readonly lumaboothSessionMode: string;
  readonly active: boolean;
};
export type PrintEntitlementSummary = {
  readonly id: string;
  readonly clientAccountId: string;
  readonly name: string;
};
export type OfferActivationSummary = {
  readonly id: string;
  readonly boothId: string;
  readonly boothOfferId: string;
  readonly status: string;
  readonly startsAt: string | null;
  readonly endsAt: string | null;
  readonly sessionAllowance: number | null;
  readonly sessionsUsed: number;
};
export type PaymentResourceSummary = {
  readonly clientAccountId: string;
  readonly paymentMethod: string;
  readonly enabled: boolean;
  readonly status: string;
};
export type TenantPaymentResourceDisplay = {
  readonly method: string;
  readonly label: string;
  readonly description: string;
  readonly icon: string;
  readonly enabled: boolean;
  readonly locked: boolean;
  readonly status: string;
  readonly statusLabel: string;
};
export type PaymentAssignmentSummary = {
  readonly id: string;
  readonly boothId: string;
  readonly paymentMethod: string;
  readonly runtimeEnabled: boolean;
  readonly status: string;
};
export type BoothAppearanceSummary = {
  readonly id: string;
  readonly boothId: string;
  readonly themePreset: string;
  readonly primaryColor: string;
  readonly accentColor: string;
  readonly backgroundImageUrl: string | null;
  readonly backgroundImageDataUrl: string | null;
  readonly sessionLabel: string;
  readonly defaultWelcomeHeadline: string;
  readonly defaultWelcomeSubtitle: string;
  readonly completionThankYouMessage: string;
};
export type TransactionSummary = {
  readonly id: string;
  readonly boothId: string;
  readonly boothOfferActivationId: string | null;
  readonly transactionNumber: string;
  readonly transactionType: string;
  readonly status: string;
  readonly paymentMethod: string;
  readonly amountCents: number;
  readonly parentTransactionId: string | null;
  readonly extraPrintCount: number;
  readonly canCreateExtraPrintAddOn: boolean;
  readonly extraPrintUnitPriceCents: number | null;
  readonly offerName: string | null;
  readonly offerType: string | null;
  readonly includedPrintEntitlement: string | null;
  readonly sessionAllowance: number | null;
  readonly coveredSessionSequence: number | null;
  readonly createdAt: string;
  readonly paidAt: string | null;
  readonly completedAt: string | null;
  readonly cancelledAt: string | null;
  readonly failureReason: string | null;
  readonly cancelledByActorType: string | null;
  readonly cancelledByUserId: string | null;
  readonly cancellationSource: string | null;
  readonly cancellationPreviousStatus: string | null;
};
export type ActivityFilter = 'ALL' | 'SALES' | 'SESSIONS';
export type TransactionActivityDisplay = {
  readonly title: string;
  readonly detail: string;
  readonly auditText: string;
  readonly value: string;
};
export type BoothPackageStatusDisplay = {
  readonly packageName: string;
  readonly detail: string;
};
export type ReportSummary = {
  readonly platform: {
    readonly activeClients: number;
    readonly activeBooths: number;
    readonly offlineBooths: number;
    readonly trialSubscriptions: number;
    readonly activeSubscriptions: number;
    readonly suspendedSubscriptions: number;
    readonly cancelledSubscriptions: number;
    readonly manualMrrCents: number;
    readonly clientsOverAllowance: number;
  };
  readonly sales: {
    readonly todayGrossSalesCents: number;
    readonly todayCompletedSessions: number;
    readonly todayCashSalesCents: number;
    readonly pendingCashCount: number;
    readonly failedOrExpiredCount: number;
  };
  readonly boothSales: readonly {
    readonly boothId: string;
    readonly boothName: string;
    readonly completedSessions: number;
    readonly grossSalesCents: number;
  }[];
  readonly locationSales: readonly {
    readonly locationId: string;
    readonly locationName: string;
    readonly completedSessions: number;
    readonly grossSalesCents: number;
  }[];
  readonly offerSales: readonly {
    readonly offerId: string;
    readonly offerName: string;
    readonly offerType: string;
    readonly completedSessions: number;
    readonly grossSalesCents: number;
  }[];
};
export type AuditLogSummary = {
  readonly id: string;
  readonly clientAccountId: string | null;
  readonly userId: string | null;
  readonly action: string;
  readonly entityType: string;
  readonly entityId: string | null;
  readonly metadata: string;
  readonly createdAt: string;
};

export type CashierPermissionKey = 'approveCash' | 'returnBoothToWelcome' | 'cancelTransaction';
export type BoothDetailTab = 'details' | 'session';
export type BoothPreviewTransaction = NonNullable<BoothStageConfig['recentTransaction']>;
export type BoothPreviewScreenKey =
  | 'welcome'
  | 'payment'
  | 'cash-waiting'
  | 'approved'
  | 'session'
  | 'completed'
  | 'expired'
  | 'cancelled'
  | 'payment-failed'
  | 'offline'
  | 'unavailable'
  | 'error';
export type BoothPreviewScreen = {
  readonly key: BoothPreviewScreenKey;
  readonly label: string;
  readonly screen: BoothStageScreenState;
  readonly boothState: string;
  readonly description: string;
  readonly activeOfferStatus?: string;
  readonly activeTransaction?: BoothStageConfig['activeTransaction'];
  readonly recentTransaction?: BoothPreviewTransaction;
  readonly withoutActiveOffer?: boolean;
  readonly withoutPaymentOptions?: boolean;
};
export type ToastKind = 'success' | 'error' | 'info';
export type ToastNotification = {
  readonly id: number;
  readonly kind: ToastKind;
  readonly message: string;
};
export type RunOptions = {
  readonly errorMessage?: string;
  readonly notifyError?: boolean;
  readonly showBusy?: boolean;
};
export type PageInfo = {
  readonly page: number;
  readonly totalPages: number;
  readonly start: number;
  readonly end: number;
  readonly total: number;
  readonly hasPrevious: boolean;
  readonly hasNext: boolean;
};

export type Overview = {
  readonly session: Session;
  readonly clients: readonly ClientSummary[];
  readonly subscriptionPlans: readonly SubscriptionPlanSummary[];
  readonly subscriptions: readonly SubscriptionSummary[];
  readonly users: readonly UserSummary[];
  readonly locations: readonly LocationSummary[];
  readonly booths: readonly BoothSummary[];
  readonly offers: readonly OfferSummary[];
  readonly printEntitlements: readonly PrintEntitlementSummary[];
  readonly activations: readonly OfferActivationSummary[];
  readonly paymentResources: readonly PaymentResourceSummary[];
  readonly paymentAssignments: readonly PaymentAssignmentSummary[];
  readonly appearanceConfigs: readonly BoothAppearanceSummary[];
  readonly transactions: readonly TransactionSummary[];
  readonly reports: ReportSummary;
  readonly auditLogs: readonly AuditLogSummary[];
};

export type BoothSecret = {
  readonly boothId: string;
  readonly boothName: string;
  readonly boothCode: string;
  readonly kioskToken: string;
  readonly agentCredential: string;
};

export type ViewKey =
  | 'dashboard'
  | 'subscriptions'
  | 'subscription-detail'
  | 'clients'
  | 'client-detail'
  | 'users'
  | 'user-detail'
  | 'locations'
  | 'booths'
  | 'booth-detail'
  | 'packages'
  | 'package-detail'
  | 'transactions'
  | 'settings'
  | 'account'
  | 'pos'
  | 'reports'
  | 'audit';

@Injectable({ providedIn: 'root' })
export class AdminWorkspace {
  private static readonly apiBaseUrl = 'http://localhost:5082';
  private static readonly boothThemeDefaults = {
    VINTAGE: {
      sessionLabel: 'Self Photo Booth',
      welcomeHeadline: 'Ready To Pose?',
      welcomeSubtitle: 'Tap start when you are ready.',
      completionThankYouMessage: 'Thanks for sharing your smile.',
    },
    CLEAN_MODERN: {
      sessionLabel: 'Self Photo Booth',
      welcomeHeadline: 'Ready when you are.',
      welcomeSubtitle: 'Review the active package, pay, then begin your session.',
      completionThankYouMessage: 'Thanks for sharing your smile.',
    },
    POP: {
      sessionLabel: 'Self Photo Booth',
      welcomeHeadline: 'Ready To Pop?',
      welcomeSubtitle: 'Tap start when you are ready.',
      completionThankYouMessage: 'Thanks for sharing your smile.',
    },
  } as const;
  private readonly http = inject(HttpClient);
  private readonly destroyRef = inject(DestroyRef);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);

  readonly session = signal<Session | null>(null);
  readonly overview = signal<Overview | null>(null);
  readonly activeView = signal<ViewKey>('dashboard');
  readonly error = signal('');
  private readonly busyRequests = signal(0);
  readonly loading = computed(() => this.busyRequests() > 0);
  readonly toasts = signal<readonly ToastNotification[]>([]);
  private readonly toastTimers = new Map<number, number>();
  private nextToastId = 0;
  private readonly paymentResourceDefinitions = [
    {
      method: 'CASH',
      label: 'Cash',
      description: 'Cash payment approval is available for every tenant and booth.',
      icon: 'PHP',
    },
    {
      method: 'MAYA_CHECKOUT_QR',
      label: 'Maya Checkout QR',
      description: 'Enable tenant setup for QR checkout resources.',
      icon: 'QR',
    },
    {
      method: 'MAYA_TERMINAL_ECR',
      label: 'Maya Terminal ECR',
      description: 'Enable tenant setup for card terminal ECR resources.',
      icon: 'ECR',
    },
  ] as const;
  readonly boothSecret = signal<BoothSecret | null>(null);
  readonly gridPageSize = 5;
  private readonly gridPages = signal<Record<string, number>>({});
  readonly dashboardActivityFilter = signal<ActivityFilter>('ALL');
  readonly cashierActivityFilter = signal<ActivityFilter>('ALL');
  readonly activityFilters: readonly {
    readonly value: ActivityFilter;
    readonly label: string;
  }[] = [
    { value: 'ALL', label: 'All' },
    { value: 'SALES', label: 'Sales' },
    { value: 'SESSIONS', label: 'Sessions' },
  ];

  readonly loginEmail = signal('owner@photobiz.local');
  readonly loginPassword = signal('PhotoBIZ!123');
  readonly defaultInitialPassword = 'PhotoBIZ!123';
  readonly changePasswordCurrent = signal('');
  readonly changePasswordNew = signal('');
  readonly changePasswordConfirm = signal('');
  readonly changePasswordModalOpen = signal(false);
  readonly clientName = signal('The Memory Box');
  readonly planName = signal('Starter Booth');
  readonly planPrice = signal(150000);
  readonly subscriptionActive = signal(true);
  readonly subscriptionAllowance = signal(2);
  readonly locationModalOpen = signal(false);
  readonly selectedLocationDetailId = signal<string | null>(null);
  readonly locationDetailName = signal('');
  readonly boothName = signal('Booth A');
  readonly boothCode = signal('SMA-001');
  readonly ownerName = signal('Client Owner');
  readonly ownerEmail = signal('owner@memorybox.local');
  readonly cashierName = signal('Cashier');
  readonly cashierEmail = signal('cashier@memorybox.local');
  readonly selectedPackageDetailId = signal<string | null>(null);
  readonly packageName = signal('Per Session');
  readonly packageDescription = signal('Standard booth session');
  readonly packageOfferType = signal('PER_SESSION');
  readonly packagePriceCents = signal(25000);
  readonly packagePrintEntitlement = signal('2 pcs 6x2 or 1 pc 6x4');
  readonly packageDurationHours = signal(1);
  readonly packageSessionAllowance = signal(3);
  readonly packageExtraPrintPriceCents = signal(5000);
  readonly packageLumaBoothMode = signal('PRINT');
  readonly packageActive = signal(true);
  readonly printEntitlementModalOpen = signal(false);
  readonly printEntitlementDetailModalOpen = signal(false);
  readonly selectedPrintEntitlementDetailId = signal<string | null>(null);
  readonly printEntitlementName = signal('');
  readonly extraPrintCopies = signal(1);
  readonly clientModalOpen = signal(false);
  readonly selectedClientDetailId = signal<string | null>(null);
  readonly subscriptionModalOpen = signal(false);
  readonly selectedSubscriptionDetailId = signal<string | null>(null);
  readonly subscriptionClientId = signal<string | null>(null);
  readonly subscriptionPlanId = signal<string | null>(null);
  readonly subscriptionStatus = signal('ACTIVE');
  readonly ownerTransferUserId = signal<string | null>(null);
  readonly userModalOpen = signal(false);
  readonly selectedUserDetailId = signal<string | null>(null);
  readonly userDetailName = signal('');
  readonly userDetailRole = signal('CLIENT_ADMIN');
  readonly userDetailPermissions = signal<Record<CashierPermissionKey, boolean>>({
    approveCash: true,
    returnBoothToWelcome: true,
    cancelTransaction: true,
  });
  readonly newUserName = signal('');
  readonly newUserEmail = signal('');
  readonly newUserRole = signal('CLIENT_ADMIN');
  readonly cashierPermissions = signal<Record<CashierPermissionKey, boolean>>({
    approveCash: true,
    returnBoothToWelcome: true,
    cancelTransaction: true,
  });
  readonly cashierPermissionRows: readonly {
    readonly key: CashierPermissionKey;
    readonly label: string;
    readonly description: string;
  }[] = [
    {
      key: 'approveCash',
      label: 'Approve cash',
      description: 'Allowed for assigned booth',
    },
    {
      key: 'returnBoothToWelcome',
      label: 'Return booth to welcome',
      description: 'Recovery action for assigned booth',
    },
    {
      key: 'cancelTransaction',
      label: 'Cancel transaction',
      description: 'Writes audit log',
    },
  ];
  readonly boothModalOpen = signal(false);
  readonly boothLocationId = signal<string | null>(null);
  readonly boothCashierUserId = signal<string | null>(null);
  readonly selectedBoothDetailId = signal<string | null>(null);
  readonly boothDetailName = signal('');
  readonly boothDetailCode = signal('');
  readonly boothDetailLocationId = signal<string | null>(null);
  readonly boothDetailCashierUserId = signal<string | null>(null);
  readonly boothDetailStatus = signal('ACTIVE');
  readonly boothDetailOfferId = signal<string | null>(null);
  readonly boothDetailTab = signal<BoothDetailTab>('details');
  readonly boothAppearanceSessionLabel = signal<string>(
    AdminWorkspace.boothThemeDefaults.VINTAGE.sessionLabel,
  );
  readonly boothAppearanceHeadline = signal<string>(
    AdminWorkspace.boothThemeDefaults.VINTAGE.welcomeHeadline,
  );
  readonly boothAppearanceSubtitle = signal<string>(
    AdminWorkspace.boothThemeDefaults.VINTAGE.welcomeSubtitle,
  );
  readonly boothAppearanceCompletionMessage = signal<string>('Thanks for sharing your smile.');
  readonly boothAppearanceThemePreset = signal<string>('VINTAGE');
  readonly boothAppearanceBackgroundImageDataUrl = signal('');
  readonly boothPreviewScreenKey = signal<BoothPreviewScreenKey>('welcome');
  readonly boothThemePresets: readonly {
    readonly value: string;
    readonly label: string;
  }[] = [
    { value: 'VINTAGE', label: 'Vintage' },
    { value: 'CLEAN_MODERN', label: 'Clean Modern' },
    { value: 'POP', label: 'Pop' },
  ];
  private readonly defaultBoothPreviewScreen: BoothPreviewScreen = {
    key: 'welcome',
    label: 'Welcome',
    screen: 'offer',
    boothState: 'WELCOME',
    description: 'Starting offer screen',
  };
  readonly boothPreviewScreens: readonly BoothPreviewScreen[] = [
    this.defaultBoothPreviewScreen,
    {
      key: 'payment',
      label: 'Payment',
      screen: 'payment',
      boothState: 'OFFER_CONFIRMED',
      description: 'Cash selection screen',
      activeTransaction: {
        id: 'preview-created',
        transactionNumber: 'TXN-PREVIEW-001',
        transactionType: 'SESSION_PURCHASE',
        status: 'CREATED',
        paymentMethod: 'PENDING',
        amountCents: 25000,
        currency: 'PHP',
        createdAt: '2026-01-01T00:00:00Z',
        expiresAt: '2026-01-01T00:05:00Z',
      },
    },
    {
      key: 'cash-waiting',
      label: 'Cash Waiting',
      screen: 'waiting',
      boothState: 'PAYMENT_PENDING',
      description: 'Waiting for cashier approval',
      activeTransaction: {
        id: 'preview-pending-cash',
        transactionNumber: 'TXN-PREVIEW-002',
        transactionType: 'SESSION_PURCHASE',
        status: 'PENDING_CASH',
        paymentMethod: 'CASH',
        amountCents: 25000,
        currency: 'PHP',
        createdAt: '2099-01-01T00:00:00Z',
        expiresAt: '2099-01-01T00:05:00Z',
      },
    },
    {
      key: 'approved',
      label: 'Approved',
      screen: 'approved',
      boothState: 'STARTING_LUMABOOTH',
      description: 'Payment confirmed',
    },
    {
      key: 'session',
      label: 'In Session',
      screen: 'session',
      boothState: 'IN_LUMABOOTH_SESSION',
      description: 'LumaBooth handoff',
    },
    {
      key: 'completed',
      label: 'Completed',
      screen: 'completed',
      boothState: 'COMPLETED',
      description: 'Session finish screen',
    },
    {
      key: 'expired',
      label: 'Expired',
      screen: 'expired',
      boothState: 'WELCOME',
      description: 'Timed-out session state',
      recentTransaction: {
        id: 'preview-expired',
        status: 'EXPIRED',
        transactionType: 'SESSION_PURCHASE',
        occurredAt: '2026-01-01T00:00:00Z',
        reason: null,
      },
    },
    {
      key: 'cancelled',
      label: 'Cancelled',
      screen: 'cancelled',
      boothState: 'WELCOME',
      description: 'Cashier cancelled payment',
      recentTransaction: {
        id: 'preview-cancelled',
        status: 'CANCELLED',
        transactionType: 'SESSION_PURCHASE',
        occurredAt: '2026-01-01T00:00:00Z',
        reason: 'The cashier cancelled this payment request.',
      },
    },
    {
      key: 'payment-failed',
      label: 'Payment Failed',
      screen: 'payment-failed',
      boothState: 'WELCOME',
      description: 'Failed payment state',
      recentTransaction: {
        id: 'preview-payment-failed',
        status: 'PAYMENT_FAILED',
        transactionType: 'SESSION_PURCHASE',
        occurredAt: '2026-01-01T00:00:00Z',
        reason: 'Payment could not be completed. Please choose another method or ask the cashier.',
      },
    },
    {
      key: 'offline',
      label: 'Offline',
      screen: 'offline',
      boothState: 'OFFLINE',
      description: 'Agent offline screen',
    },
    {
      key: 'unavailable',
      label: 'Unavailable',
      screen: 'unavailable',
      boothState: 'WELCOME',
      description: 'Package needs activation',
      activeOfferStatus: 'PENDING_PAYMENT',
      withoutPaymentOptions: true,
    },
    {
      key: 'error',
      label: 'Error',
      screen: 'error',
      boothState: 'ERROR',
      description: 'Recovery-needed screen',
    },
  ];

  readonly selectedClientId = computed(() => this.overview()?.clients[0]?.id ?? null);
  readonly selectedPlanId = computed(() => this.overview()?.subscriptionPlans[0]?.id ?? null);
  readonly selectedLocationId = computed(() => this.overview()?.locations[0]?.id ?? null);
  readonly selectedLocationDetail = computed(() => {
    const selectedId = this.selectedLocationDetailId();
    return this.overview()?.locations.find((location) => location.id === selectedId) ?? null;
  });
  readonly selectedBoothId = computed(() => this.overview()?.booths[0]?.id ?? null);
  readonly selectedOfferId = computed(
    () =>
      this.overview()?.offers.find((offer) => offer.active)?.id ??
      this.overview()?.offers[0]?.id ??
      null,
  );
  readonly activeBooths = computed(
    () => this.overview()?.booths.filter((booth) => booth.status === 'ACTIVE') ?? [],
  );
  readonly offlineBooths = computed(() =>
    this.activeBooths().filter((booth) => booth.currentState === 'OFFLINE'),
  );
  readonly pendingTransactions = computed(
    () =>
      this.overview()?.transactions.filter(
        (transaction) => transaction.status === 'PENDING_CASH',
      ) ?? [],
  );
  readonly completedTransactions = computed(
    () =>
      this.overview()?.transactions.filter((transaction) => transaction.status === 'COMPLETED') ??
      [],
  );
  readonly grossSalesCents = computed(() =>
    this.completedTransactions().reduce((total, transaction) => total + transaction.amountCents, 0),
  );
  readonly assignedBooth = computed(() => {
    const assignedBoothId = this.session()?.assignedBoothId;
    const booths = this.overview()?.booths ?? [];
    return booths.find((booth) => booth.id === assignedBoothId) ?? null;
  });
  readonly dashboardActivityTransactions = computed(() =>
    this.filterActivityTransactions(
      this.overview()?.transactions ?? [],
      this.dashboardActivityFilter(),
    ),
  );
  readonly cashierActivityTransactions = computed(() => {
    const boothId = this.assignedBooth()?.id;
    const transactions =
      this.overview()?.transactions.filter((transaction) => transaction.boothId === boothId) ?? [];

    return this.filterActivityTransactions(transactions, this.cashierActivityFilter());
  });
  readonly cashierTransaction = computed(() => {
    const boothId = this.assignedBooth()?.id;
    return (
      this.overview()?.transactions.find(
        (transaction) =>
          boothId && transaction.boothId === boothId && transaction.status === 'PENDING_CASH',
      ) ?? null
    );
  });
  readonly pendingPlanActivation = computed(() => {
    const boothId = this.assignedBooth()?.id;
    if (!boothId || this.cashierTransaction()) {
      return null;
    }

    return this.pendingActivationFor(boothId);
  });
  readonly pendingPlanActivationOffer = computed(() => {
    const activation = this.pendingPlanActivation();
    return activation ? this.offerForActivation(activation) : null;
  });
  readonly extraPrintReferenceTransaction = computed(() => {
    const boothId = this.assignedBooth()?.id;
    if (this.pendingPlanActivation()) {
      return null;
    }

    const boothTransactions = (this.overview()?.transactions ?? [])
      .filter((transaction) => transaction.boothId === boothId)
      .sort((left, right) => Date.parse(right.createdAt) - Date.parse(left.createdAt));
    return this.resolveExtraPrintReferenceTransaction(boothTransactions);
  });
  readonly extraPrintCandidate = computed(() => {
    const reference = this.extraPrintReferenceTransaction();
    return reference?.canCreateExtraPrintAddOn ? reference : null;
  });
  readonly extraPrintTotalCents = computed(() => {
    const candidate = this.extraPrintCandidate();
    return (candidate?.extraPrintUnitPriceCents ?? 0) * this.extraPrintCopies();
  });
  readonly isApplicationOwner = computed(() => this.session()?.role === 'APPLICATION_OWNER');
  readonly platformClients = computed(() => {
    return this.overview()?.clients ?? [];
  });
  readonly subscriptionAuditLogs = computed(
    () =>
      this.overview()?.auditLogs.filter(
        (audit) =>
          audit.entityType === 'ClientSubscription' ||
          audit.action.startsWith('client_subscription.'),
      ) ?? [],
  );
  readonly selectedClient = computed(() => {
    const clients = this.overview()?.clients ?? [];
    const selectedId = this.selectedClientDetailId() ?? clients[0]?.id ?? null;
    return clients.find((client) => client.id === selectedId) ?? null;
  });
  readonly currentClient = computed<ClientSummary | null>(() => {
    const clientAccountId = this.session()?.clientAccountId;
    const clients = this.overview()?.clients ?? [];
    return clients.find((client) => client.id === clientAccountId) ?? clients[0] ?? null;
  });
  readonly currentClientOwner = computed(() => {
    const client = this.currentClient();
    return client ? this.ownerForClient(client.id) : null;
  });
  readonly currentClientSubscription = computed(() => {
    const client = this.currentClient();
    return client ? this.latestSubscriptionFor(client.id) : null;
  });
  readonly currentClientSubscriptionPlan = computed(() => {
    const subscription = this.currentClientSubscription();
    return (
      this.overview()?.subscriptionPlans.find(
        (plan) => plan.id === subscription?.subscriptionPlanId,
      ) ?? null
    );
  });
  readonly currentClientActiveBoothCount = computed(() => {
    const client = this.currentClient();
    return client ? this.activeBoothCountForClient(client.id) : 0;
  });
  readonly tenantPaymentResources = computed<TenantPaymentResourceDisplay[]>(() => {
    const clientId = this.currentClient()?.id ?? null;
    const resources = this.overview()?.paymentResources ?? [];

    return this.paymentResourceDefinitions.map((definition) => {
      const resource = resources.find(
        (item) => item.clientAccountId === clientId && item.paymentMethod === definition.method,
      );
      const status =
        definition.method === 'CASH' ? 'VERIFIED' : (resource?.status ?? 'NOT_CONFIGURED');
      const enabled = definition.method === 'CASH' ? true : (resource?.enabled ?? false);

      return {
        ...definition,
        enabled,
        locked: definition.method === 'CASH',
        status,
        statusLabel: this.paymentResourceStatusLabel(status),
      };
    });
  });
  readonly clientOwners = computed(
    () => this.overview()?.users.filter((user) => user.role === 'CLIENT_OWNER') ?? [],
  );
  readonly clientAdmins = computed(
    () => this.overview()?.users.filter((user) => user.role === 'CLIENT_ADMIN') ?? [],
  );
  readonly cashiers = computed(
    () => this.overview()?.users.filter((user) => user.role === 'CASHIER') ?? [],
  );
  readonly posAssignableUsers = computed(
    () =>
      this.overview()?.users.filter(
        (user) =>
          user.status === 'ACTIVE' &&
          ['CLIENT_OWNER', 'CLIENT_ADMIN', 'CASHIER'].includes(user.role),
      ) ?? [],
  );
  readonly availablePosStaff = computed(() =>
    this.posAssignableUsers().filter((user) => !user.assignedBoothId),
  );
  readonly boothDetailPosStaffOptions = computed(() => {
    const selectedBoothId = this.selectedBoothDetailId();
    return this.posAssignableUsers().filter(
      (user) => !user.assignedBoothId || user.assignedBoothId === selectedBoothId,
    );
  });
  readonly inactiveUsers = computed(
    () => this.overview()?.users.filter((user) => user.status !== 'ACTIVE') ?? [],
  );
  readonly selectedUser = computed(() => {
    const users = this.overview()?.users ?? [];
    const selectedId = this.selectedUserDetailId();
    return users.find((user) => user.id === selectedId) ?? null;
  });
  readonly canApproveCashAction = computed(() => {
    const session = this.session();
    return session?.role !== 'CASHIER' || session.canApproveCash !== false;
  });
  readonly canReturnBoothToWelcomeAction = computed(() => {
    const session = this.session();
    return session?.role !== 'CASHIER' || session.canReturnBoothToWelcome !== false;
  });
  readonly canCancelTransactionAction = computed(() => {
    const session = this.session();
    return session?.role !== 'CASHIER' || session.canCancelTransaction !== false;
  });
  readonly activeOffers = computed(
    () => this.overview()?.offers.filter((offer) => offer.active) ?? [],
  );
  readonly selectedBoothDetail = computed(() => {
    const selectedId = this.selectedBoothDetailId();
    return this.overview()?.booths.find((booth) => booth.id === selectedId) ?? null;
  });
  readonly selectedBoothCashier = computed(() => {
    const boothId = this.selectedBoothDetailId();
    return this.posAssignableUsers().find((user) => user.assignedBoothId === boothId) ?? null;
  });
  readonly selectedBoothAppearance = computed(() => {
    const boothId = this.selectedBoothDetailId();
    return (
      this.overview()?.appearanceConfigs?.find((appearance) => appearance.boothId === boothId) ??
      null
    );
  });
  readonly selectedBoothSecret = computed(() => {
    const booth = this.selectedBoothDetail();
    const secret = this.boothSecret();
    return booth && secret?.boothId === booth.id ? secret : null;
  });
  readonly boothPreviewConfig = computed<BoothStageConfig | null>(() => {
    const booth = this.selectedBoothDetail();
    const client = this.currentClient();

    if (!booth || !client) {
      return null;
    }

    const location = this.overview()?.locations.find((item) => item.id === booth.locationId);
    const offer = this.activeOffers().find((item) => item.id === this.boothDetailOfferId()) ?? null;
    const cashAssignment = this.cashAssignmentFor(booth.id);

    return {
      client: { displayName: client.name, logoUrl: null },
      theme: {
        preset: this.boothAppearanceThemePreset(),
        primaryColor: this.themeSchemeFor(this.boothAppearanceThemePreset()).primaryColor,
        accentColor: this.themeSchemeFor(this.boothAppearanceThemePreset()).accentColor,
        backgroundImageUrl: null,
        backgroundImageDataUrl: this.boothAppearanceBackgroundImageDataUrl() || null,
        fontMode: this.boothAppearanceThemePreset() === 'VINTAGE' ? 'serif' : 'sans',
      },
      session: {
        label: this.boothAppearanceSessionLabel(),
        welcomeHeadline: this.boothAppearanceHeadline(),
        welcomeSubtitle: this.boothAppearanceSubtitle(),
        completionThankYouMessage: this.boothAppearanceCompletionMessage(),
      },
      booth: {
        id: booth.id,
        state: booth.currentState,
        name: booth.name,
        code: booth.code,
        locationName: location?.name,
      },
      activeOffer: offer
        ? {
            id: offer.id,
            name: offer.name,
            type: offer.offerType,
            priceCents: offer.priceCents,
            currency: offer.currency,
            includedPrintEntitlement: offer.includedPrintEntitlement,
            allowsExtraPrintAddOn: offer.allowsExtraPrintAddOn,
            extraPrintPriceCents: offer.extraPrintPriceCents,
            activationStatus: this.selectedActivationFor(booth.id)?.status ?? 'ACTIVE',
            startsAt: this.selectedActivationFor(booth.id)?.startsAt ?? null,
            endsAt: this.selectedActivationFor(booth.id)?.endsAt ?? null,
            sessionAllowance: this.selectedActivationFor(booth.id)?.sessionAllowance ?? null,
            sessionsUsed: this.selectedActivationFor(booth.id)?.sessionsUsed ?? 0,
          }
        : null,
      paymentOptions:
        cashAssignment?.status === 'ASSIGNED'
          ? [
              {
                method: 'CASH',
                label: 'Pay Cash',
                runtimeEnabled: cashAssignment.runtimeEnabled,
              },
            ]
          : [],
      activeTransaction: null,
      recentTransaction: null,
    };
  });
  readonly selectedBoothPreviewScreen = computed<BoothPreviewScreen>(
    () =>
      this.boothPreviewScreens.find((screen) => screen.key === this.boothPreviewScreenKey()) ??
      this.defaultBoothPreviewScreen,
  );
  readonly selectedBoothPreviewConfig = computed<BoothStageConfig | null>(() => {
    const config = this.boothPreviewConfig();
    const preview = this.selectedBoothPreviewScreen();

    if (!config) {
      return null;
    }

    const activeOffer = preview.withoutActiveOffer
      ? null
      : preview.activeOfferStatus && config.activeOffer
        ? { ...config.activeOffer, activationStatus: preview.activeOfferStatus }
        : config.activeOffer;

    return {
      ...config,
      booth: { ...config.booth, state: preview.boothState },
      activeOffer,
      activeTransaction: preview.activeTransaction
        ? {
            ...preview.activeTransaction,
            createdAt:
              preview.activeTransaction.status === 'CREATED'
                ? new Date(Date.now()).toISOString()
                : preview.activeTransaction.createdAt,
            expiresAt: new Date(Date.now() + 60_000).toISOString(),
          }
        : null,
      paymentOptions: preview.withoutPaymentOptions ? [] : config.paymentOptions,
      recentTransaction: preview.recentTransaction ?? null,
    };
  });
  readonly selectedPackage = computed(() => {
    const selectedId = this.selectedPackageDetailId();
    return this.overview()?.offers.find((offer) => offer.id === selectedId) ?? null;
  });
  readonly packageCurrency = computed(() => this.selectedPackage()?.currency ?? 'PHP');
  readonly printEntitlements = computed(() => {
    return this.overview()?.printEntitlements ?? [];
  });
  readonly packagePrintEntitlementOptions = computed(() => {
    const names = this.printEntitlements().map((entitlement) => entitlement.name);
    const selectedName = this.packagePrintEntitlement();

    if (selectedName && !names.includes(selectedName)) {
      names.unshift(selectedName);
    }

    return names;
  });
  readonly selectedPrintEntitlement = computed(() => {
    const selectedId = this.selectedPrintEntitlementDetailId();
    return this.printEntitlements().find((entitlement) => entitlement.id === selectedId) ?? null;
  });
  isPersistedPrintEntitlement(entitlement: PrintEntitlementSummary | null): boolean {
    return Boolean(entitlement && !entitlement.id.startsWith('default-'));
  }
  isPrintEntitlementInUse(entitlement: PrintEntitlementSummary): boolean {
    return Boolean(
      this.overview()?.offers.some(
        (offer) =>
          offer.clientAccountId === entitlement.clientAccountId &&
          offer.includedPrintEntitlement === entitlement.name,
      ),
    );
  }
  printEntitlementUsageStatus(entitlement: PrintEntitlementSummary): string {
    return this.isPrintEntitlementInUse(entitlement) ? 'In Use' : 'Not Used';
  }
  canDeletePrintEntitlement(entitlement: PrintEntitlementSummary): boolean {
    return (
      this.isPersistedPrintEntitlement(entitlement) && !this.isPrintEntitlementInUse(entitlement)
    );
  }
  readonly selectedSubscriptionDefinition = computed(() => {
    const selectedId = this.selectedSubscriptionDetailId();
    return (
      this.overview()?.subscriptionPlans.find((subscription) => subscription.id === selectedId) ??
      null
    );
  });
  private readonly navItems: readonly { key: ViewKey; label: string }[] = [
    { key: 'dashboard', label: 'Dashboard' },
    { key: 'subscriptions', label: 'Subscriptions' },
    { key: 'clients', label: 'Clients' },
    { key: 'users', label: 'Users' },
    { key: 'locations', label: 'Locations' },
    { key: 'booths', label: 'Booths' },
    { key: 'packages', label: 'Packages' },
    { key: 'transactions', label: 'Transactions' },
    { key: 'pos', label: 'Cashier POS' },
    { key: 'reports', label: 'Reports' },
    { key: 'settings', label: 'Settings' },
    { key: 'audit', label: 'Audit Log' },
  ];
  private readonly viewPaths: Record<ViewKey, string> = {
    dashboard: '/dashboard',
    subscriptions: '/subscriptions',
    'subscription-detail': '/subscriptions/detail',
    clients: '/clients',
    'client-detail': '/clients/detail',
    users: '/users',
    'user-detail': '/users/detail',
    locations: '/locations',
    booths: '/booths',
    'booth-detail': '/booths/detail',
    packages: '/packages',
    'package-detail': '/packages/detail',
    transactions: '/transactions',
    settings: '/settings',
    account: '/account',
    pos: '/pos',
    reports: '/reports',
    audit: '/audit',
  };
  readonly visibleNavItems = computed(() =>
    this.navItems.filter((item) => this.canAccessView(item.key)),
  );

  constructor() {
    this.restoreSession();

    interval(5000)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        if (this.session() && !this.session()?.mustChangePassword) {
          this.run(() => this.loadOverview(), {
            errorMessage: 'Refresh failed.',
            notifyError: false,
            showBusy: false,
          });
        }
      });

    this.destroyRef.onDestroy(() => {
      for (const timer of this.toastTimers.values()) {
        window.clearTimeout(timer);
      }
      this.toastTimers.clear();
    });
  }

  async login(): Promise<void> {
    await this.run(
      async () => {
        const session = await firstValueFrom(
          this.http.post<Session>(
            `${AdminWorkspace.apiBaseUrl}/api/auth/login`,
            { email: this.loginEmail(), password: this.loginPassword() },
            { withCredentials: true },
          ),
        );

        this.session.set(session);
        if (session.mustChangePassword) {
          this.overview.set(null);
          this.succeed('Update your password to continue.');
          return;
        }

        await this.loadOverview();
        this.succeed('Signed in.');
      },
      { errorMessage: 'Login failed.' },
    );
  }

  async changeOwnPassword(): Promise<boolean> {
    return await this.run(
      async () => {
        const session = await firstValueFrom(
          this.http.post<Session>(
            `${AdminWorkspace.apiBaseUrl}/api/auth/change-password`,
            {
              currentPassword: this.changePasswordCurrent(),
              newPassword: this.changePasswordNew(),
              confirmPassword: this.changePasswordConfirm(),
            },
            { withCredentials: true },
          ),
        );

        this.session.set(session);
        this.resetChangePasswordForm();
        this.changePasswordModalOpen.set(false);

        if (!session.mustChangePassword) {
          await this.loadOverview();
        }

        this.succeed('Password updated.');
      },
      { errorMessage: 'Password update failed.' },
    );
  }

  openChangePasswordModal(): void {
    this.resetChangePasswordForm();
    this.changePasswordModalOpen.set(true);
  }

  closeChangePasswordModal(): void {
    this.changePasswordModalOpen.set(false);
    this.resetChangePasswordForm();
  }

  async logout(): Promise<void> {
    await this.run(
      async () => {
        await firstValueFrom(
          this.http.post(
            `${AdminWorkspace.apiBaseUrl}/api/auth/logout`,
            {},
            { withCredentials: true },
          ),
        );
        this.session.set(null);
        this.overview.set(null);
        this.changePasswordModalOpen.set(false);
        this.resetChangePasswordForm();
        this.succeed('Signed out.');
      },
      { errorMessage: 'Sign out failed.' },
    );
  }

  setView(view: ViewKey): void {
    if (!this.canAccessView(view)) {
      return;
    }

    this.activeView.set(view);
    void this.router.navigateByUrl(this.viewPaths[view]);
  }

  activateRouteView(view: ViewKey): void {
    if (!this.canAccessView(view)) {
      this.setView('dashboard');
      return;
    }

    this.activeView.set(view);
  }

  pagedItems<T>(key: string, items: readonly T[] | null | undefined): readonly T[] {
    const safeItems = items ?? [];
    const page = this.clampedGridPage(key, safeItems.length);
    const start = (page - 1) * this.gridPageSize;

    return safeItems.slice(start, start + this.gridPageSize);
  }

  pageInfo(key: string, totalItems: number | null | undefined): PageInfo {
    const total = Math.max(0, totalItems ?? 0);
    const page = this.clampedGridPage(key, total);
    const totalPages = this.totalGridPages(total);
    const start = total === 0 ? 0 : (page - 1) * this.gridPageSize + 1;
    const end = total === 0 ? 0 : Math.min(total, page * this.gridPageSize);

    return {
      page,
      totalPages,
      start,
      end,
      total,
      hasPrevious: page > 1,
      hasNext: page < totalPages,
    };
  }

  previousPage(key: string, totalItems: number | null | undefined): void {
    const page = this.clampedGridPage(key, Math.max(0, totalItems ?? 0));
    this.setGridPage(key, page - 1, totalItems);
  }

  nextPage(key: string, totalItems: number | null | undefined): void {
    const page = this.clampedGridPage(key, Math.max(0, totalItems ?? 0));
    this.setGridPage(key, page + 1, totalItems);
  }

  setDashboardActivityFilter(filter: ActivityFilter): void {
    this.dashboardActivityFilter.set(filter);
    this.setGridPage('dashboard-activity', 1, this.dashboardActivityTransactions().length);
  }

  setCashierActivityFilter(filter: ActivityFilter): void {
    this.cashierActivityFilter.set(filter);
    this.setGridPage('cashier-activity', 1, this.cashierActivityTransactions().length);
  }

  boothsForClient(clientId: string): readonly BoothSummary[] {
    return this.overview()?.booths.filter((booth) => booth.clientAccountId === clientId) ?? [];
  }

  async createClient(): Promise<void> {
    await this.run(
      async () => {
        await firstValueFrom(
          this.http.post(
            `${AdminWorkspace.apiBaseUrl}/api/admin/clients`,
            { name: this.clientName() },
            { withCredentials: true },
          ),
        );
        await this.loadOverview();
        this.succeed('Client account created.');
      },
      { errorMessage: 'Client creation failed.' },
    );
  }

  openClientModal(): void {
    this.clientModalOpen.set(true);
  }

  closeClientModal(): void {
    this.clientModalOpen.set(false);
  }

  openSubscriptionModal(clientId?: string): void {
    const resolvedClientId = clientId ?? this.selectedClientId();
    const latestSubscription = resolvedClientId
      ? this.latestSubscriptionFor(resolvedClientId)
      : null;

    this.subscriptionClientId.set(resolvedClientId);
    this.subscriptionPlanId.set(latestSubscription?.subscriptionPlanId ?? this.selectedPlanId());
    this.subscriptionStatus.set(latestSubscription?.status ?? 'ACTIVE');
    this.subscriptionAllowance.set(latestSubscription?.activeBoothAllowance ?? 2);
    this.subscriptionModalOpen.set(true);
  }

  closeSubscriptionModal(): void {
    this.subscriptionModalOpen.set(false);
  }

  openUserModal(): void {
    this.newUserName.set('');
    this.newUserEmail.set('');
    this.newUserRole.set('CLIENT_ADMIN');
    this.cashierPermissions.set({
      approveCash: true,
      returnBoothToWelcome: true,
      cancelTransaction: true,
    });
    this.userModalOpen.set(true);
  }

  closeUserModal(): void {
    this.userModalOpen.set(false);
  }

  openLocationModal(location?: LocationSummary): void {
    this.selectedLocationDetailId.set(location?.id ?? null);
    this.locationDetailName.set(location?.name ?? '');
    this.locationModalOpen.set(true);
  }

  closeLocationModal(): void {
    this.locationModalOpen.set(false);
  }

  startNewPackage(): void {
    this.selectedPackageDetailId.set(null);
    this.packageName.set('Per Session');
    this.packageDescription.set('Standard booth session');
    this.packageOfferType.set('PER_SESSION');
    this.packagePriceCents.set(25000);
    this.packagePrintEntitlement.set('2 pcs 6x2 or 1 pc 6x4');
    this.packageDurationHours.set(1);
    this.packageSessionAllowance.set(3);
    this.packageExtraPrintPriceCents.set(5000);
    this.packageLumaBoothMode.set('PRINT');
    this.packageActive.set(true);
    this.setView('package-detail');
  }

  viewPackage(offer: OfferSummary): void {
    this.selectedPackageDetailId.set(offer.id);
    this.packageName.set(offer.name);
    this.packageDescription.set(offer.description ?? '');
    this.packageOfferType.set(offer.offerType);
    this.packagePriceCents.set(offer.priceCents);
    this.packagePrintEntitlement.set(offer.includedPrintEntitlement);
    this.packageDurationHours.set(offer.durationHours ?? 1);
    this.packageSessionAllowance.set(offer.sessionAllowance ?? 3);
    this.packageExtraPrintPriceCents.set(offer.extraPrintPriceCents ?? 0);
    this.packageLumaBoothMode.set(
      offer.lumaboothSessionMode === 'SESSION_STANDARD' ? 'PRINT' : offer.lumaboothSessionMode,
    );
    this.packageActive.set(offer.active);
    this.setView('package-detail');
  }

  setPackagePriceFromPesos(value: string | number): void {
    this.packagePriceCents.set(this.moneyInputToCents(value));
  }

  setPackageExtraPrintPriceFromPesos(value: string | number): void {
    this.packageExtraPrintPriceCents.set(this.moneyInputToCents(value));
  }

  openPrintEntitlementsModal(): void {
    this.selectedPrintEntitlementDetailId.set(null);
    this.printEntitlementName.set('');
    this.printEntitlementModalOpen.set(true);
  }

  startNewPrintEntitlement(): void {
    this.selectedPrintEntitlementDetailId.set(null);
    this.printEntitlementName.set('');
    this.printEntitlementDetailModalOpen.set(true);
  }

  viewPrintEntitlement(entitlement: PrintEntitlementSummary): void {
    this.selectedPrintEntitlementDetailId.set(entitlement.id);
    this.printEntitlementName.set(entitlement.name);
    this.printEntitlementDetailModalOpen.set(true);
  }

  closePrintEntitlementModal(): void {
    this.printEntitlementModalOpen.set(false);
    this.closePrintEntitlementDetailModal();
  }

  closePrintEntitlementDetailModal(): void {
    this.printEntitlementDetailModalOpen.set(false);
    this.selectedPrintEntitlementDetailId.set(null);
    this.printEntitlementName.set('');
  }

  toggleCashierPermission(key: CashierPermissionKey): void {
    this.cashierPermissions.update((permissions) => ({
      ...permissions,
      [key]: !permissions[key],
    }));
  }

  toggleUserDetailPermission(key: CashierPermissionKey): void {
    this.userDetailPermissions.update((permissions) => ({
      ...permissions,
      [key]: !permissions[key],
    }));
  }

  setUserDetailRole(role: string): void {
    const previousRole = this.userDetailRole();
    this.userDetailRole.set(role);

    if (role === 'CASHIER' && previousRole !== 'CASHIER') {
      const selected = this.selectedUser();
      this.userDetailPermissions.set(
        selected?.role === 'CASHIER'
          ? this.permissionStateFor(selected)
          : {
              approveCash: true,
              returnBoothToWelcome: true,
              cancelTransaction: true,
            },
      );
    }
  }

  openBoothModal(locationId?: string): void {
    this.boothLocationId.set(locationId ?? this.selectedLocationId());
    this.boothCashierUserId.set(null);
    this.boothName.set('Booth A');
    this.boothCode.set('SMA-001');
    this.boothSecret.set(null);
    this.boothModalOpen.set(true);
  }

  closeBoothModal(): void {
    this.boothModalOpen.set(false);
  }

  async onboardClient(): Promise<void> {
    await this.run(
      async () => {
        await firstValueFrom(
          this.http.post(
            `${AdminWorkspace.apiBaseUrl}/api/admin/clients/onboard`,
            {
              clientName: this.clientName(),
              ownerName: this.ownerName(),
              ownerEmail: this.ownerEmail(),
            },
            { withCredentials: true },
          ),
        );
        await this.loadOverview();
        this.clientModalOpen.set(false);
        this.setView('clients');
        this.succeed('Client and owner credentials created.');
      },
      { errorMessage: 'Client onboarding failed.' },
    );
  }

  async updateClientStatus(client: ClientSummary, status: string): Promise<void> {
    const action =
      status === 'ACTIVE' ? 'activation' : status === 'SUSPENDED' ? 'suspension' : 'archive';
    const successMessage =
      status === 'ACTIVE'
        ? 'Client activated.'
        : status === 'SUSPENDED'
          ? 'Client suspended.'
          : 'Client archived.';

    await this.run(
      async () => {
        await firstValueFrom(
          this.http.put(
            `${AdminWorkspace.apiBaseUrl}/api/admin/clients/${client.id}`,
            { name: client.name, status },
            { withCredentials: true },
          ),
        );
        await this.loadOverview();
        this.succeed(successMessage);
      },
      { errorMessage: `Client ${action} failed.` },
    );
  }

  async createPlan(): Promise<void> {
    await this.run(
      async () => {
        await firstValueFrom(
          this.http.post(
            `${AdminWorkspace.apiBaseUrl}/api/admin/subscription-plans`,
            { name: this.planName(), pricePerBoothCents: this.planPrice(), currency: 'PHP' },
            { withCredentials: true },
          ),
        );
        await this.loadOverview();
        this.succeed('Subscription created.');
      },
      { errorMessage: 'Subscription creation failed.' },
    );
  }

  startNewSubscriptionDefinition(): void {
    this.selectedSubscriptionDetailId.set(null);
    this.planName.set('Per Booth MVP');
    this.planPrice.set(200000);
    this.subscriptionActive.set(true);
    this.setView('subscription-detail');
  }

  setPlanPriceFromPesos(value: number | string): void {
    this.planPrice.set(Math.max(0, Math.round(Number(value || 0) * 100)));
  }

  async saveSubscriptionDefinition(): Promise<void> {
    const selectedSubscription = this.selectedSubscriptionDefinition();

    await this.run(
      async () => {
        if (selectedSubscription) {
          await firstValueFrom(
            this.http.put(
              `${AdminWorkspace.apiBaseUrl}/api/admin/subscription-plans/${selectedSubscription.id}`,
              {
                name: this.planName(),
                pricePerBoothCents: this.planPrice(),
                currency: selectedSubscription.currency,
                active: this.subscriptionActive(),
              },
              { withCredentials: true },
            ),
          );
          this.succeed('Subscription updated.');
        } else {
          await firstValueFrom(
            this.http.post(
              `${AdminWorkspace.apiBaseUrl}/api/admin/subscription-plans`,
              { name: this.planName(), pricePerBoothCents: this.planPrice(), currency: 'PHP' },
              { withCredentials: true },
            ),
          );
          this.succeed('Subscription created.');
        }

        await this.loadOverview();
        this.setView('subscriptions');
      },
      {
        errorMessage: selectedSubscription
          ? 'Subscription save failed.'
          : 'Subscription creation failed.',
      },
    );
  }

  async assignSubscription(): Promise<boolean> {
    const clientAccountId = this.subscriptionClientId() ?? this.selectedClientId();
    const subscriptionPlanId = this.subscriptionPlanId() ?? this.selectedPlanId();
    const existingSubscription = clientAccountId
      ? this.latestSubscriptionFor(clientAccountId)
      : null;

    if (!clientAccountId || !subscriptionPlanId) {
      this.fail('Create a client and subscription first.');
      return false;
    }

    return await this.run(
      async () => {
        const payload = {
          subscriptionPlanId,
          status: this.subscriptionStatus(),
          activeBoothAllowance: this.subscriptionAllowance(),
          notes: 'MVP subscription',
        };

        if (existingSubscription) {
          await firstValueFrom(
            this.http.put(
              `${AdminWorkspace.apiBaseUrl}/api/admin/subscriptions/${existingSubscription.id}`,
              {
                ...payload,
                endsOn: null,
              },
              { withCredentials: true },
            ),
          );
        } else {
          await firstValueFrom(
            this.http.post(
              `${AdminWorkspace.apiBaseUrl}/api/admin/subscriptions`,
              {
                clientAccountId,
                ...payload,
              },
              { withCredentials: true },
            ),
          );
        }

        await this.loadOverview();
        this.subscriptionModalOpen.set(false);
        this.succeed(
          existingSubscription ? 'Client subscription updated.' : 'Client subscription assigned.',
        );
      },
      {
        errorMessage: existingSubscription
          ? 'Client subscription update failed.'
          : 'Client subscription assignment failed.',
      },
    );
  }

  async updateSubscription(
    subscription: SubscriptionSummary,
    status: string,
    allowance = subscription.activeBoothAllowance,
  ): Promise<void> {
    await this.run(
      async () => {
        await firstValueFrom(
          this.http.put(
            `${AdminWorkspace.apiBaseUrl}/api/admin/subscriptions/${subscription.id}`,
            {
              subscriptionPlanId: subscription.subscriptionPlanId,
              status,
              activeBoothAllowance: allowance,
              endsOn: null,
              notes: 'Updated from Admin Web',
            },
            { withCredentials: true },
          ),
        );
        await this.loadOverview();
        this.succeed('Subscription updated.');
      },
      { errorMessage: 'Subscription update failed.' },
    );
  }

  async createManagedUser(): Promise<void> {
    await this.createUser(
      this.newUserRole(),
      null,
      this.newUserName(),
      this.newUserEmail(),
      `User created. Default password: ${this.defaultInitialPassword}.`,
      this.cashierPermissions(),
    );
  }

  async updateUserStatus(user: UserSummary, status: string): Promise<void> {
    if (user.id === this.session()?.userId && status === 'INACTIVE') {
      this.fail('You cannot deactivate your own account.');
      return;
    }

    const successMessage = status === 'ACTIVE' ? 'User activated.' : 'User deactivated.';
    const errorMessage =
      status === 'ACTIVE' ? 'User activation failed.' : 'User deactivation failed.';

    await this.run(
      async () => {
        await firstValueFrom(
          this.http.put(
            `${AdminWorkspace.apiBaseUrl}/api/admin/users/${user.id}`,
            {
              assignedBoothId: this.isPosAssignableRole(user.role) ? user.assignedBoothId : null,
              name: user.name,
              email: user.email,
              role: user.role,
              status,
              ...this.cashierPermissionPayload(user.role, this.permissionStateFor(user)),
            },
            { withCredentials: true },
          ),
        );
        await this.loadOverview();
        this.succeed(successMessage);
      },
      { errorMessage },
    );
  }

  async saveUserDetail(): Promise<void> {
    const user = this.selectedUser();

    if (!user) {
      this.fail('Select a user first.');
      return;
    }

    const role = this.userDetailRole();

    await this.run(
      async () => {
        const savedUser = await firstValueFrom(
          this.http.put<UserSummary>(
            `${AdminWorkspace.apiBaseUrl}/api/admin/users/${user.id}`,
            {
              assignedBoothId: this.isPosAssignableRole(role) ? user.assignedBoothId : null,
              name: this.userDetailName(),
              email: user.email,
              role,
              status: user.status,
              ...this.cashierPermissionPayload(role, this.userDetailPermissions()),
            },
            { withCredentials: true },
          ),
        );
        await this.loadOverview();
        this.syncUserDetail(savedUser);
        this.succeed('User details updated.');
      },
      { errorMessage: 'User save failed.' },
    );
  }

  async transferClientOwner(clientId: string): Promise<void> {
    const newOwnerUserId = this.ownerTransferUserId();

    if (!newOwnerUserId) {
      this.fail('Select a user to transfer ownership to.');
      return;
    }

    await this.run(
      async () => {
        await firstValueFrom(
          this.http.post(
            `${AdminWorkspace.apiBaseUrl}/api/admin/clients/${clientId}/transfer-owner`,
            { newOwnerUserId },
            { withCredentials: true },
          ),
        );
        await this.loadOverview();
        this.ownerTransferUserId.set(null);
        this.succeed('Client owner transferred.');
      },
      { errorMessage: 'Client owner transfer failed.' },
    );
  }

  async createLocation(): Promise<void> {
    if (!this.selectedClientId()) {
      this.fail('Create a client first.');
      return;
    }

    const name = this.locationDetailName().trim();
    if (!name) {
      this.fail('Enter a location name.');
      return;
    }

    await this.run(
      async () => {
        await firstValueFrom(
          this.http.post(
            `${AdminWorkspace.apiBaseUrl}/api/admin/locations`,
            {
              clientAccountId: this.selectedClientId(),
              name,
              address: null,
            },
            { withCredentials: true },
          ),
        );
        await this.loadOverview();
        this.closeLocationModal();
        this.succeed('Location created.');
      },
      { errorMessage: 'Location creation failed.' },
    );
  }

  async saveLocationDetail(): Promise<void> {
    const location = this.selectedLocationDetail();
    const name = this.locationDetailName().trim();

    if (!location) {
      await this.createLocation();
      return;
    }

    if (!name) {
      this.fail('Enter a location name.');
      return;
    }

    await this.run(
      async () => {
        await firstValueFrom(
          this.http.put(
            `${AdminWorkspace.apiBaseUrl}/api/admin/locations/${location.id}`,
            { name, address: location.address, status: location.status },
            { withCredentials: true },
          ),
        );
        await this.loadOverview();
        this.closeLocationModal();
        this.succeed('Location updated.');
      },
      { errorMessage: 'Location save failed.' },
    );
  }

  async updateLocationStatus(location: LocationSummary, status: string): Promise<boolean> {
    const successMessage = status === 'ACTIVE' ? 'Location activated.' : 'Location deactivated.';
    const errorMessage =
      status === 'ACTIVE' ? 'Location activation failed.' : 'Location deactivation failed.';

    return await this.run(
      async () => {
        await firstValueFrom(
          this.http.put(
            `${AdminWorkspace.apiBaseUrl}/api/admin/locations/${location.id}`,
            { name: location.name, address: location.address, status },
            { withCredentials: true },
          ),
        );
        await this.loadOverview();
        this.succeed(successMessage);
      },
      { errorMessage },
    );
  }

  async deactivateLocation(location: LocationSummary): Promise<void> {
    if (await this.updateLocationStatus(location, 'INACTIVE')) {
      this.closeLocationModal();
    }
  }

  async activateLocation(location: LocationSummary): Promise<void> {
    if (await this.updateLocationStatus(location, 'ACTIVE')) {
      this.closeLocationModal();
    }
  }

  async createBooth(): Promise<void> {
    const locationId = this.boothLocationId() ?? this.selectedLocationId();

    if (!this.selectedClientId() || !locationId) {
      this.fail('Create a client and location first.');
      return;
    }

    await this.run(
      async () => {
        const response = await firstValueFrom(
          this.http.post<{ booth: BoothSummary; kioskToken: string; agentCredential: string }>(
            `${AdminWorkspace.apiBaseUrl}/api/admin/booths`,
            {
              clientAccountId: this.selectedClientId(),
              locationId,
              name: this.boothName(),
              code: this.boothCode(),
              cashierUserId: this.boothCashierUserId(),
            },
            { withCredentials: true },
          ),
        );

        this.boothSecret.set({
          boothId: response.booth.id,
          boothName: response.booth.name,
          boothCode: response.booth.code,
          kioskToken: response.kioskToken,
          agentCredential: response.agentCredential,
        });
        await this.loadOverview();
        this.closeBoothModal();
        this.syncBoothDetail(response.booth.id);
        this.boothDetailTab.set('details');
        this.setView('booth-detail');
        this.succeed('Booth created and credentials issued.');
      },
      { errorMessage: 'Booth creation failed.' },
    );
  }

  async issueBoothCredentials(): Promise<void> {
    const booth = this.selectedBoothDetail();

    if (!booth) {
      this.fail('Select a booth first.');
      return;
    }

    await this.run(
      async () => {
        const response = await firstValueFrom(
          this.http.post<{
            boothId: string;
            boothCode: string;
            kioskToken: string;
            agentCredential: string;
          }>(
            `${AdminWorkspace.apiBaseUrl}/api/admin/booths/${booth.id}/credentials`,
            {},
            { withCredentials: true },
          ),
        );

        this.boothSecret.set({
          boothId: response.boothId,
          boothName: booth.name,
          boothCode: response.boothCode,
          kioskToken: response.kioskToken,
          agentCredential: response.agentCredential,
        });
        this.succeed('Booth credentials issued. Update the Windows Agent with the new credential.');
      },
      { errorMessage: 'Booth credential issue failed.' },
    );
  }

  async updateBoothStatus(booth: BoothSummary, status: string): Promise<void> {
    const successMessage = status === 'ACTIVE' ? 'Booth activated.' : 'Booth deactivated.';
    const errorMessage =
      status === 'ACTIVE' ? 'Booth activation failed.' : 'Booth deactivation failed.';

    await this.run(
      async () => {
        await firstValueFrom(
          this.http.put(
            `${AdminWorkspace.apiBaseUrl}/api/admin/booths/${booth.id}`,
            {
              locationId: booth.locationId,
              name: booth.name,
              code: booth.code,
              status,
              cashierUserId: this.assignedPosStaffFor(booth.id)?.id ?? null,
            },
            { withCredentials: true },
          ),
        );
        await this.loadOverview();
        this.succeed(successMessage);
      },
      { errorMessage },
    );
  }

  async saveBoothDetails(): Promise<void> {
    const booth = this.selectedBoothDetail();

    if (!booth || !this.boothDetailLocationId()) {
      this.fail('Select a booth and location first.');
      return;
    }

    await this.run(
      async () => {
        const updated = await firstValueFrom(
          this.http.put<BoothSummary>(
            `${AdminWorkspace.apiBaseUrl}/api/admin/booths/${booth.id}`,
            {
              locationId: this.boothDetailLocationId(),
              name: this.boothDetailName(),
              code: this.boothDetailCode(),
              status: this.boothDetailStatus(),
              cashierUserId: this.boothDetailCashierUserId(),
            },
            { withCredentials: true },
          ),
        );

        const offerId = this.boothDetailOfferId();
        if (offerId && offerId !== this.selectedOfferFor(booth.id)?.id) {
          await firstValueFrom(
            this.http.post(
              `${AdminWorkspace.apiBaseUrl}/api/admin/booths/${booth.id}/activate-offer`,
              { boothOfferId: offerId },
              { withCredentials: true },
            ),
          );
        }

        if (!this.cashAssignmentFor(booth.id)?.runtimeEnabled) {
          await firstValueFrom(
            this.http.post(
              `${AdminWorkspace.apiBaseUrl}/api/admin/booths/${booth.id}/payment-options`,
              { paymentMethod: 'CASH', runtimeEnabled: true },
              { withCredentials: true },
            ),
          );
        }

        await this.loadOverview();
        this.syncBoothDetail(updated.id);
        const selectedOffer = this.selectedOfferFor(updated.id);
        this.succeed(
          selectedOffer?.offerType === 'PER_SESSION'
            ? 'Booth details saved.'
            : 'Booth details saved. Cashier activation is required before customers can start sessions.',
        );
      },
      { errorMessage: 'Booth save failed.' },
    );
  }

  async saveBoothSession(): Promise<void> {
    const booth = this.selectedBoothDetail();

    if (!booth) {
      this.fail('Select a booth first.');
      return;
    }

    await this.run(
      async () => {
        await firstValueFrom(
          this.http.put(
            `${AdminWorkspace.apiBaseUrl}/api/admin/booths/${booth.id}/appearance`,
            {
              themePreset: this.boothAppearanceThemePreset(),
              sessionLabel: this.boothAppearanceSessionLabel(),
              defaultWelcomeHeadline: this.boothAppearanceHeadline(),
              defaultWelcomeSubtitle: this.boothAppearanceSubtitle(),
              completionThankYouMessage: this.boothAppearanceCompletionMessage(),
              backgroundImageDataUrl: this.boothAppearanceBackgroundImageDataUrl() || null,
            },
            { withCredentials: true },
          ),
        );
        await this.loadOverview();
        this.syncBoothDetail(booth.id);
        this.succeed('Session setup saved.');
      },
      { errorMessage: 'Session setup save failed.' },
    );
  }

  async savePackage(): Promise<void> {
    if (!this.selectedClientId()) {
      this.fail('Create a client first.');
      return;
    }

    const name = this.packageName().trim();
    if (!name) {
      this.fail('Package name is required.');
      return;
    }

    const offerType = this.packageOfferType();
    const allowsExtraPrintAddOn =
      offerType === 'PER_SESSION' && this.packageExtraPrintPriceCents() > 0;
    const payload = {
      clientAccountId: this.selectedClientId(),
      name,
      description: this.packageDescription().trim() || null,
      offerType,
      priceCents: this.packagePriceCents(),
      currency: 'PHP',
      includedPrintEntitlement: this.packagePrintEntitlement(),
      durationHours: offerType === 'TIME_UNLIMITED' ? this.packageDurationHours() : null,
      sessionAllowance: offerType === 'SESSION_COUNT' ? this.packageSessionAllowance() : null,
      allowsExtraPrintAddOn,
      extraPrintPriceCents: allowsExtraPrintAddOn ? this.packageExtraPrintPriceCents() : null,
      lumaboothSessionMode: this.packageLumaBoothMode(),
    };
    const selectedPackageId = this.selectedPackageDetailId();

    await this.run(
      async () => {
        if (selectedPackageId) {
          await firstValueFrom(
            this.http.put(
              `${AdminWorkspace.apiBaseUrl}/api/admin/offers/${selectedPackageId}`,
              { ...payload, active: this.packageActive() },
              { withCredentials: true },
            ),
          );
        } else {
          await firstValueFrom(
            this.http.post(`${AdminWorkspace.apiBaseUrl}/api/admin/offers`, payload, {
              withCredentials: true,
            }),
          );
        }
        await this.loadOverview();
        this.setView('packages');
        this.succeed(selectedPackageId ? 'Package updated.' : 'Package created.');
      },
      { errorMessage: selectedPackageId ? 'Package save failed.' : 'Package creation failed.' },
    );
  }

  async updatePackageStatus(offer: OfferSummary, active: boolean): Promise<void> {
    await this.run(
      async () => {
        const updated = await firstValueFrom(
          this.http.put<OfferSummary>(
            `${AdminWorkspace.apiBaseUrl}/api/admin/offers/${offer.id}`,
            { ...offer, active },
            { withCredentials: true },
          ),
        );
        await this.loadOverview();
        this.viewPackage(updated);
        this.succeed(active ? 'Package activated.' : 'Package deactivated.');
      },
      { errorMessage: active ? 'Package activation failed.' : 'Package deactivation failed.' },
    );
  }

  async savePrintEntitlement(): Promise<void> {
    if (!this.selectedClientId()) {
      this.fail('Create a client first.');
      return;
    }

    const name = this.printEntitlementName().trim();
    if (!name) {
      this.fail('Print entitlement name is required.');
      return;
    }

    const selected = this.selectedPrintEntitlement();
    const shouldUpdate = this.isPersistedPrintEntitlement(selected);

    await this.run(
      async () => {
        const saved =
          shouldUpdate && selected
            ? await firstValueFrom(
                this.http.put<PrintEntitlementSummary>(
                  `${AdminWorkspace.apiBaseUrl}/api/admin/print-entitlements/${selected.id}`,
                  { name },
                  { withCredentials: true },
                ),
              )
            : await firstValueFrom(
                this.http.post<PrintEntitlementSummary>(
                  `${AdminWorkspace.apiBaseUrl}/api/admin/print-entitlements`,
                  { clientAccountId: this.selectedClientId(), name },
                  { withCredentials: true },
                ),
              );

        await this.loadOverview();
        this.packagePrintEntitlement.set(saved.name);
        this.closePrintEntitlementDetailModal();
        this.succeed(shouldUpdate ? 'Print entitlement updated.' : 'Print entitlement created.');
      },
      {
        errorMessage: shouldUpdate
          ? 'Print entitlement save failed.'
          : 'Print entitlement creation failed.',
      },
    );
  }

  async deletePrintEntitlement(entitlement: PrintEntitlementSummary): Promise<void> {
    if (!this.isPersistedPrintEntitlement(entitlement)) {
      this.fail('Default print entitlements cannot be deleted.');
      return;
    }

    if (this.isPrintEntitlementInUse(entitlement)) {
      this.fail('Print entitlement is in use by one or more packages.');
      return;
    }

    await this.run(
      async () => {
        await firstValueFrom(
          this.http.delete(
            `${AdminWorkspace.apiBaseUrl}/api/admin/print-entitlements/${entitlement.id}`,
            {
              withCredentials: true,
            },
          ),
        );
        await this.loadOverview();

        if (this.selectedPrintEntitlementDetailId() === entitlement.id) {
          this.closePrintEntitlementDetailModal();
        }

        this.succeed('Print entitlement deleted.');
      },
      { errorMessage: 'Print entitlement delete failed.' },
    );
  }

  async activateOffer(
    boothId = this.selectedBoothId(),
    offerId = this.selectedOfferId(),
  ): Promise<void> {
    if (!boothId || !offerId) {
      this.fail('Create a booth and offer first.');
      return;
    }

    await this.run(
      async () => {
        await firstValueFrom(
          this.http.post(
            `${AdminWorkspace.apiBaseUrl}/api/admin/booths/${boothId}/activate-offer`,
            { boothOfferId: offerId },
            { withCredentials: true },
          ),
        );
        await this.loadOverview();
        this.succeed('Offer activated for booth.');
      },
      { errorMessage: 'Offer activation failed.' },
    );
  }

  async assignCash(boothId: string): Promise<void> {
    await this.run(
      async () => {
        await firstValueFrom(
          this.http.post(
            `${AdminWorkspace.apiBaseUrl}/api/admin/booths/${boothId}/payment-options`,
            { paymentMethod: 'CASH', runtimeEnabled: true },
            { withCredentials: true },
          ),
        );
        await this.loadOverview();
        this.succeed('Cash enabled for booth.');
      },
      { errorMessage: 'Cash assignment failed.' },
    );
  }

  async setCashPaymentEnabled(boothId: string, enabled: boolean): Promise<void> {
    if (enabled) {
      await this.assignCash(boothId);
      return;
    }

    const assignment = this.cashAssignmentFor(boothId);

    if (!assignment) {
      this.succeed('Cash already disabled for booth.');
      return;
    }

    await this.disablePayment(assignment);
  }

  async setPaymentResourceEnabled(paymentMethod: string, enabled: boolean): Promise<void> {
    if (paymentMethod === 'CASH' && !enabled) {
      this.fail('Cash is always enabled for client tenants.');
      return;
    }

    await this.run(
      async () => {
        await firstValueFrom(
          this.http.put(
            `${AdminWorkspace.apiBaseUrl}/api/admin/payment-resources/${paymentMethod}`,
            { enabled },
            { withCredentials: true },
          ),
        );
        await this.loadOverview();
        this.succeed(enabled ? 'Payment resource enabled.' : 'Payment resource disabled.');
      },
      { errorMessage: 'Payment resource update failed.' },
    );
  }

  async disablePayment(assignment: PaymentAssignmentSummary): Promise<void> {
    await this.run(
      async () => {
        await firstValueFrom(
          this.http.delete(
            `${AdminWorkspace.apiBaseUrl}/api/admin/booths/${assignment.boothId}/payment-options/${assignment.paymentMethod}`,
            { withCredentials: true },
          ),
        );
        await this.loadOverview();
        this.succeed('Payment assignment disabled.');
      },
      { errorMessage: 'Payment assignment disable failed.' },
    );
  }

  async approveCash(transactionId: string): Promise<void> {
    await this.run(
      async () => {
        await firstValueFrom(
          this.http.post(
            `${AdminWorkspace.apiBaseUrl}/api/cashier/transactions/${transactionId}/approve-cash`,
            {},
            { withCredentials: true },
          ),
        );
        await this.loadOverview();
        this.succeed('Cash approved.');
      },
      { errorMessage: 'Cash approval failed.' },
    );
  }

  async cancelTransaction(transactionId: string): Promise<void> {
    await this.run(
      async () => {
        await firstValueFrom(
          this.http.post(
            `${AdminWorkspace.apiBaseUrl}/api/cashier/transactions/${transactionId}/cancel`,
            {},
            { withCredentials: true },
          ),
        );
        await this.loadOverview();
        this.succeed('Transaction cancelled.');
      },
      { errorMessage: 'Transaction cancellation failed.' },
    );
  }

  async returnBoothToWelcome(boothId: string): Promise<void> {
    await this.run(
      async () => {
        await firstValueFrom(
          this.http.post(
            `${AdminWorkspace.apiBaseUrl}/api/cashier/booths/${boothId}/return-to-welcome`,
            {},
            { withCredentials: true },
          ),
        );
        await this.loadOverview();
        this.succeed('Booth returned to welcome.');
      },
      { errorMessage: 'Return to welcome failed.' },
    );
  }

  async createExtraPrintAddOn(parentTransactionId: string): Promise<boolean> {
    return await this.run(
      async () => {
        await firstValueFrom(
          this.http.post(
            `${AdminWorkspace.apiBaseUrl}/api/cashier/transactions/${parentTransactionId}/extra-prints`,
            { copyCount: this.extraPrintCopies() },
            { withCredentials: true },
          ),
        );
        await this.loadOverview();
        this.succeed('Extra print add-on created. Collect cash, then approve.');
      },
      { errorMessage: 'Extra print add-on creation failed.' },
    );
  }

  async createPlanActivation(boothId: string): Promise<void> {
    await this.run(
      async () => {
        await firstValueFrom(
          this.http.post(
            `${AdminWorkspace.apiBaseUrl}/api/cashier/booths/${boothId}/plan-activation`,
            {},
            { withCredentials: true },
          ),
        );
        await this.loadOverview();
        this.succeed('Package activation created. Collect cash, then approve.');
      },
      { errorMessage: 'Package activation creation failed.' },
    );
  }

  clientNameFor(clientId: string | null): string {
    return this.overview()?.clients.find((client) => client.id === clientId)?.name ?? 'Platform';
  }

  ownerForClient(clientId: string): UserSummary | null {
    return (
      this.overview()?.users.find(
        (user) => user.clientAccountId === clientId && user.role === 'CLIENT_OWNER',
      ) ?? null
    );
  }

  ownerTransferCandidates(clientId: string): readonly UserSummary[] {
    const currentOwnerId = this.ownerForClient(clientId)?.id ?? null;

    return (
      this.overview()?.users.filter(
        (user) =>
          user.clientAccountId === clientId &&
          user.status === 'ACTIVE' &&
          user.id !== currentOwnerId &&
          this.isPosAssignableRole(user.role),
      ) ?? []
    );
  }

  ownerTransferTargetFor(clientId: string): UserSummary | null {
    const selectedUserId = this.ownerTransferUserId();

    if (!selectedUserId) {
      return null;
    }

    return (
      this.ownerTransferCandidates(clientId).find((candidate) => candidate.id === selectedUserId) ??
      null
    );
  }

  latestSubscriptionFor(clientId: string): SubscriptionSummary | null {
    const subscriptions =
      this.overview()?.subscriptions.filter(
        (subscription) => subscription.clientAccountId === clientId,
      ) ?? [];

    return subscriptions.at(-1) ?? null;
  }

  planNameFor(planId: string | null): string {
    if (!planId) {
      return 'No subscription';
    }

    return (
      this.overview()?.subscriptionPlans.find((plan) => plan.id === planId)?.name ??
      'Unknown subscription'
    );
  }

  planPriceFor(planId: string | null): number {
    if (!planId) {
      return 0;
    }

    return (
      this.overview()?.subscriptionPlans.find((plan) => plan.id === planId)?.pricePerBoothCents ?? 0
    );
  }

  subscriptionMonthlyTotalCents(subscription: SubscriptionSummary | null): number {
    if (!subscription) {
      return 0;
    }

    return this.planPriceFor(subscription.subscriptionPlanId) * subscription.activeBoothAllowance;
  }

  assignedClientCountForSubscription(subscriptionPlanId: string): number {
    return new Set(
      (this.overview()?.subscriptions ?? [])
        .filter((subscription) => subscription.subscriptionPlanId === subscriptionPlanId)
        .map((subscription) => subscription.clientAccountId),
    ).size;
  }

  assignedAllowanceForSubscription(subscriptionPlanId: string): number {
    return (this.overview()?.subscriptions ?? [])
      .filter((subscription) => subscription.subscriptionPlanId === subscriptionPlanId)
      .reduce((total, subscription) => total + subscription.activeBoothAllowance, 0);
  }

  subscriptionDefinitionMrrCents(subscriptionPlanId: string): number {
    return (this.overview()?.subscriptions ?? [])
      .filter(
        (subscription) =>
          subscription.subscriptionPlanId === subscriptionPlanId &&
          ['TRIAL', 'ACTIVE'].includes(subscription.status),
      )
      .reduce(
        (total, subscription) =>
          total +
          this.planPriceFor(subscription.subscriptionPlanId) * subscription.activeBoothAllowance,
        0,
      );
  }

  activeBoothCountForClient(clientId: string): number {
    return (
      this.overview()?.booths.filter(
        (booth) => booth.clientAccountId === clientId && booth.status === 'ACTIVE',
      ).length ?? 0
    );
  }

  boothCountForLocation(locationId: string): number {
    return this.overview()?.booths.filter((booth) => booth.locationId === locationId).length ?? 0;
  }

  locationSalesCents(locationId: string): number {
    return (
      this.overview()?.reports.locationSales.find((location) => location.locationId === locationId)
        ?.grossSalesCents ?? 0
    );
  }

  offerSessionCount(offerId: string): number {
    return (
      this.overview()?.reports.offerSales.find((offer) => offer.offerId === offerId)
        ?.completedSessions ?? 0
    );
  }

  activeBoothCountUsingOffers(): number {
    const activatedBoothIds = new Set(
      (this.overview()?.activations ?? [])
        .filter((activation) => activation.status === 'ACTIVE')
        .map((activation) => activation.boothId),
    );
    return activatedBoothIds.size;
  }

  viewClient(client: ClientSummary): void {
    this.selectedClientDetailId.set(client.id);
    this.setView('client-detail');
  }

  viewUser(user: UserSummary): void {
    this.syncUserDetail(user);
    this.setView('user-detail');
  }

  viewSubscription(subscription: SubscriptionPlanSummary): void {
    this.selectedSubscriptionDetailId.set(subscription.id);
    this.planName.set(subscription.name);
    this.planPrice.set(subscription.pricePerBoothCents);
    this.subscriptionActive.set(subscription.active);
    this.setView('subscription-detail');
  }

  viewBooth(booth: BoothSummary): void {
    this.syncBoothDetail(booth.id);
    this.boothDetailTab.set('details');
    this.setView('booth-detail');
  }

  syncBoothDetail(boothId: string): void {
    const booth = this.overview()?.booths.find((item) => item.id === boothId);

    if (!booth) {
      return;
    }

    const appearance =
      this.overview()?.appearanceConfigs?.find((item) => item.boothId === booth.id) ?? null;
    const selectedOffer = this.selectedOfferFor(booth.id);
    const assignedPosStaff = this.assignedPosStaffFor(booth.id);

    this.selectedBoothDetailId.set(booth.id);
    this.boothDetailName.set(booth.name);
    this.boothDetailCode.set(booth.code);
    this.boothDetailLocationId.set(booth.locationId);
    this.boothDetailCashierUserId.set(assignedPosStaff?.id ?? null);
    this.boothDetailStatus.set(booth.status);
    this.boothDetailOfferId.set(selectedOffer?.id ?? this.activeOffers()[0]?.id ?? null);
    const themePreset = this.normalizeBoothThemePreset(appearance?.themePreset);
    const themeDefaults = this.boothDefaultsFor(themePreset);

    this.boothAppearanceSessionLabel.set(appearance?.sessionLabel || themeDefaults.sessionLabel);
    this.boothAppearanceHeadline.set(
      appearance?.defaultWelcomeHeadline || themeDefaults.welcomeHeadline,
    );
    this.boothAppearanceSubtitle.set(
      appearance?.defaultWelcomeSubtitle || themeDefaults.welcomeSubtitle,
    );
    this.boothAppearanceCompletionMessage.set(
      appearance?.completionThankYouMessage || themeDefaults.completionThankYouMessage,
    );
    this.boothAppearanceThemePreset.set(themePreset);
    this.boothAppearanceBackgroundImageDataUrl.set(appearance?.backgroundImageDataUrl ?? '');
  }

  setBoothDetailTab(tab: BoothDetailTab): void {
    this.boothDetailTab.set(tab);
  }

  setBoothPreviewScreen(screen: BoothPreviewScreenKey): void {
    this.boothPreviewScreenKey.set(screen);
  }

  setBoothBackgroundImageFromFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];

    if (!file) {
      return;
    }

    if (!['image/png', 'image/jpeg', 'image/webp'].includes(file.type)) {
      this.fail('Upload a PNG, JPG, or WebP image for the booth background.');
      input.value = '';
      return;
    }

    if (file.size > 2 * 1024 * 1024) {
      this.fail('Background image must be 2 MB or smaller.');
      input.value = '';
      return;
    }

    const reader = new FileReader();
    reader.onload = () => {
      this.boothAppearanceBackgroundImageDataUrl.set(String(reader.result ?? ''));
    };
    reader.onerror = () => this.fail('Unable to read the background image.');
    reader.readAsDataURL(file);
  }

  clearBoothBackgroundImage(): void {
    this.boothAppearanceBackgroundImageDataUrl.set('');
  }

  resetBoothSessionToThemeDefaults(): void {
    const defaults = this.boothDefaultsFor(this.boothAppearanceThemePreset());
    this.boothAppearanceSessionLabel.set(defaults.sessionLabel);
    this.boothAppearanceHeadline.set(defaults.welcomeHeadline);
    this.boothAppearanceSubtitle.set(defaults.welcomeSubtitle);
    this.boothAppearanceCompletionMessage.set(defaults.completionThankYouMessage);
  }

  private normalizeBoothThemePreset(value: string | null | undefined): string {
    switch ((value ?? '').toUpperCase()) {
      case 'POP':
      case 'MODERN_POP':
        return 'POP';
      case 'CLEAN_MODERN':
      case 'MODERN_CLEAN':
      case 'CLASSIC_LIGHT':
        return 'CLEAN_MODERN';
      default:
        return 'VINTAGE';
    }
  }

  private themeSchemeFor(value: string): {
    readonly primaryColor: string;
    readonly accentColor: string;
  } {
    switch (this.normalizeBoothThemePreset(value)) {
      case 'POP':
        return { primaryColor: '#0bbbe6', accentColor: '#ff0090' };
      case 'CLEAN_MODERN':
        return { primaryColor: '#111827', accentColor: '#2563eb' };
      default:
        return { primaryColor: '#4f2d1d', accentColor: '#f5d27e' };
    }
  }

  private boothDefaultsFor(value: string): {
    readonly sessionLabel: string;
    readonly welcomeHeadline: string;
    readonly welcomeSubtitle: string;
    readonly completionThankYouMessage: string;
  } {
    return AdminWorkspace.boothThemeDefaults[
      this.normalizeBoothThemePreset(value) as keyof typeof AdminWorkspace.boothThemeDefaults
    ];
  }

  locationNameFor(locationId: string): string {
    return (
      this.overview()?.locations.find((location) => location.id === locationId)?.name ??
      'Unassigned'
    );
  }

  boothNameFor(boothId: string | null): string {
    return this.overview()?.booths.find((booth) => booth.id === boothId)?.name ?? 'Unassigned';
  }

  userNameFor(userId: string | null | undefined): string {
    return this.overview()?.users.find((user) => user.id === userId)?.name ?? 'Unknown user';
  }

  assignedPosStaffFor(boothId: string): UserSummary | null {
    return this.posAssignableUsers().find((user) => user.assignedBoothId === boothId) ?? null;
  }

  canEditSelectedUserRole(user: UserSummary): boolean {
    return user.id !== this.session()?.userId && user.role !== 'CLIENT_OWNER';
  }

  canDeactivateUser(user: UserSummary): boolean {
    return user.id !== this.session()?.userId;
  }

  activeOfferFor(boothId: string): OfferSummary | null {
    const activation = this.overview()?.activations.find(
      (item) => item.boothId === boothId && item.status === 'ACTIVE',
    );
    return this.overview()?.offers.find((offer) => offer.id === activation?.boothOfferId) ?? null;
  }

  pendingActivationFor(boothId: string): OfferActivationSummary | null {
    return (
      this.overview()?.activations.find(
        (item) => item.boothId === boothId && item.status === 'PENDING_PAYMENT',
      ) ?? null
    );
  }

  selectedActivationFor(boothId: string): OfferActivationSummary | null {
    return (
      this.pendingActivationFor(boothId) ??
      this.overview()?.activations.find(
        (item) => item.boothId === boothId && item.status === 'ACTIVE',
      ) ??
      null
    );
  }

  boothPackageStatusFor(booth: BoothSummary): BoothPackageStatusDisplay | null {
    const activation = this.dashboardActivationFor(booth.id);
    const offer = this.offerForActivation(activation);

    if (!activation || !offer) {
      return null;
    }

    if (activation.status === 'PENDING_PAYMENT') {
      return {
        packageName: offer.name,
        detail: 'Awaiting cashier activation',
      };
    }

    if (offer.offerType === 'SESSION_COUNT') {
      const sessionAllowance = activation.sessionAllowance ?? offer.sessionAllowance ?? 0;
      const latestCoveredSession = this.latestCoveredSessionForActivation(activation.id);
      const usedCount =
        latestCoveredSession?.coveredSessionSequence ?? activation.sessionsUsed ?? 0;

      return {
        packageName: offer.name,
        detail: `${usedCount} of ${sessionAllowance} used`,
      };
    }

    if (offer.offerType === 'TIME_UNLIMITED') {
      return {
        packageName: offer.name,
        detail: this.timedPackageStatusFor(activation, offer),
      };
    }

    return {
      packageName: offer.name,
      detail: 'Pay per session',
    };
  }

  offerForActivation(activation: OfferActivationSummary | null): OfferSummary | null {
    return this.overview()?.offers.find((offer) => offer.id === activation?.boothOfferId) ?? null;
  }

  selectedOfferFor(boothId: string): OfferSummary | null {
    return this.offerForActivation(this.selectedActivationFor(boothId));
  }

  packageStatusLabelFor(boothId: string): string {
    const activation = this.selectedActivationFor(boothId);
    const offer = this.offerForActivation(activation);

    if (!activation || !offer) {
      return 'None';
    }

    return activation.status === 'PENDING_PAYMENT'
      ? `${offer.name} (awaiting activation)`
      : offer.name;
  }

  activationDetailFor(activation: OfferActivationSummary, offer: OfferSummary): string {
    if (offer.offerType === 'TIME_UNLIMITED') {
      return `${offer.durationHours ?? 0} hour timed package`;
    }

    if (offer.offerType === 'SESSION_COUNT') {
      return `${activation.sessionsUsed}/${activation.sessionAllowance ?? offer.sessionAllowance ?? 0} sessions used`;
    }

    return offer.includedPrintEntitlement;
  }

  cashAssignmentFor(boothId: string): PaymentAssignmentSummary | null {
    return (
      this.overview()?.paymentAssignments.find(
        (assignment) => assignment.boothId === boothId && assignment.paymentMethod === 'CASH',
      ) ?? null
    );
  }

  paymentLabelFor(boothId: string): string {
    const assignment = this.cashAssignmentFor(boothId);

    if (!assignment) {
      return 'None';
    }

    return assignment.runtimeEnabled ? 'Cash' : 'Cash disabled';
  }

  formatMoney(cents: number): string {
    return `PHP ${(cents / 100).toLocaleString('en-PH', { maximumFractionDigits: 0 })}`;
  }

  paymentResourceStatusLabel(status: string): string {
    return status
      .split('_')
      .map((part) => part.charAt(0) + part.slice(1).toLowerCase())
      .join(' ');
  }

  statusClassFor(value: string | null | undefined): string {
    return String(value ?? '')
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, '-');
  }

  transactionActivityFor(transaction: TransactionSummary): TransactionActivityDisplay {
    const boothName = this.boothNameFor(transaction.boothId);
    const offerName = transaction.offerName ?? 'Package';
    const offerType = this.readableType(transaction.offerType ?? transaction.transactionType);
    const status = this.readableType(transaction.status);
    const entitlement = transaction.includedPrintEntitlement
      ? ` / ${transaction.includedPrintEntitlement}`
      : '';
    const auditText = `${transaction.transactionNumber} / ${status}`;

    switch (transaction.transactionType) {
      case 'PLAN_ACTIVATION':
        return {
          title: 'Package activation',
          detail: this.withCancellationDetail(
            `${boothName} / ${offerName} / ${offerType}`,
            transaction,
          ),
          auditText,
          value: this.formatMoney(transaction.amountCents),
        };
      case 'COVERED_PLAN_SESSION':
        return {
          title: 'Covered session',
          detail: this.withCancellationDetail(
            `${boothName} / ${offerName} / ${this.coveredSessionDetail(transaction)}`,
            transaction,
          ),
          auditText,
          value: 'Included',
        };
      case 'EXTRA_PRINT_ADD_ON':
        return {
          title: 'Extra print add-on',
          detail: this.withCancellationDetail(
            `${boothName} / ${transaction.extraPrintCount} ${
              transaction.extraPrintCount === 1 ? 'copy' : 'copies'
            }${entitlement}`,
            transaction,
          ),
          auditText,
          value: this.formatMoney(transaction.amountCents),
        };
      case 'SESSION_PURCHASE':
      default:
        return {
          title: 'Per-session sale',
          detail: this.withCancellationDetail(
            `${boothName} / ${offerName}${entitlement}`,
            transaction,
          ),
          auditText,
          value: this.formatMoney(transaction.amountCents),
        };
    }
  }

  cancellationDetailFor(transaction: TransactionSummary): string {
    if (transaction.status !== 'CANCELLED') {
      return '';
    }

    const actor = this.cancellationActorLabel(
      transaction.cancelledByActorType,
      transaction.cancelledByUserId,
    );
    const source = this.cancellationSourceLabel(transaction.cancellationSource);

    return `${actor} / ${source}`;
  }

  auditDetailFor(audit: AuditLogSummary): string {
    const metadata = this.parseAuditMetadata(audit.metadata);

    if (
      audit.action === 'transaction.cancelled' ||
      audit.action === 'transaction.kiosk_cancelled'
    ) {
      const transactionNumber = this.metadataText(metadata, 'TransactionNumber');
      const actor = this.cancellationActorLabel(
        this.metadataText(metadata, 'CancelledByActorType') ??
          (audit.action === 'transaction.kiosk_cancelled' ? 'BOOTH_USER' : null),
        this.metadataText(metadata, 'CancelledByUserId') ?? audit.userId,
      );
      const source = this.cancellationSourceLabel(
        this.metadataText(metadata, 'CancellationSource'),
      );
      const previousStatus = this.metadataText(metadata, 'PreviousStatus');
      const previous = previousStatus ? `from ${this.readableType(previousStatus)}` : null;
      const reason = this.metadataText(metadata, 'Reason');

      return [transactionNumber, actor, source, previous, reason].filter(Boolean).join(' / ');
    }

    return this.compactAuditMetadata(metadata);
  }

  isTerminalTransaction(status: string): boolean {
    return status === 'COMPLETED' || status === 'EXPIRED' || status === 'CANCELLED';
  }

  copyOptions(): readonly number[] {
    return [1, 2, 3, 4, 5];
  }

  roleLabel(role = this.session()?.role ?? ''): string {
    return role
      .split('_')
      .map((part) => part.charAt(0) + part.slice(1).toLowerCase())
      .join(' ');
  }

  activeViewTitle(): string {
    switch (this.activeView()) {
      case 'dashboard':
        return this.isApplicationOwner() ? 'PhotoBIZ Platform Dashboard' : 'Operations Dashboard';
      case 'subscriptions':
        return 'Subscriptions';
      case 'subscription-detail':
        return this.selectedSubscriptionDefinition() ? 'Subscription Detail' : 'Add Subscription';
      case 'clients':
        return 'Client Accounts';
      case 'client-detail':
        return 'Client Account Detail';
      case 'users':
        return 'Users';
      case 'user-detail':
        return this.selectedUser()?.name ?? 'User Detail';
      case 'locations':
        return 'Location & Booth Inventory';
      case 'booths':
        return 'Booths';
      case 'booth-detail':
        return this.selectedBoothDetail()?.name ?? 'Manage Booth';
      case 'packages':
        return 'Packages';
      case 'package-detail':
        return this.selectedPackage() ? 'Package Detail' : 'New Package';
      case 'transactions':
        return 'Sales & Audit';
      case 'pos':
        return 'Cashier POS';
      case 'reports':
        return 'Reports';
      case 'settings':
        return 'Payment Resources';
      case 'account':
        return 'Account';
      case 'audit':
        return 'Audit Log';
      default:
        return 'PhotoBIZ';
    }
  }

  canAccessView(view: ViewKey): boolean {
    const role = this.session()?.role;

    if (view === 'account') {
      return Boolean(role);
    }

    if (role === 'APPLICATION_OWNER') {
      return (
        view === 'dashboard' ||
        view === 'subscriptions' ||
        view === 'subscription-detail' ||
        view === 'clients' ||
        view === 'client-detail' ||
        view === 'audit'
      );
    }

    if (role === 'CASHIER') {
      return view === 'dashboard' || view === 'pos' || view === 'reports' || view === 'audit';
    }

    if (role === 'CLIENT_OWNER' || role === 'CLIENT_ADMIN') {
      return (
        view === 'dashboard' ||
        view === 'users' ||
        view === 'user-detail' ||
        view === 'locations' ||
        view === 'booths' ||
        view === 'booth-detail' ||
        view === 'packages' ||
        view === 'package-detail' ||
        view === 'transactions' ||
        view === 'pos' ||
        view === 'reports' ||
        view === 'settings' ||
        view === 'audit'
      );
    }

    return view === 'dashboard';
  }

  formatDate(value: string): string {
    return new Intl.DateTimeFormat('en-PH', {
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    }).format(new Date(value));
  }

  formatPhilippinesDateTime(value: string): string {
    return `${new Intl.DateTimeFormat('en-PH', {
      timeZone: 'Asia/Manila',
      month: 'short',
      day: 'numeric',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    }).format(new Date(value))} PH time`;
  }

  private moneyInputToCents(value: string | number): number {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? Math.max(0, Math.round(parsed * 100)) : 0;
  }

  private setGridPage(key: string, page: number, totalItems: number | null | undefined): void {
    const targetPage = Math.min(
      Math.max(1, page),
      this.totalGridPages(Math.max(0, totalItems ?? 0)),
    );

    this.gridPages.update((pages) => ({ ...pages, [key]: targetPage }));
  }

  private clampedGridPage(key: string, totalItems: number): number {
    const currentPage = this.gridPages()[key] ?? 1;
    return Math.min(Math.max(1, currentPage), this.totalGridPages(totalItems));
  }

  private totalGridPages(totalItems: number): number {
    return Math.max(1, Math.ceil(totalItems / this.gridPageSize));
  }

  private filterActivityTransactions(
    transactions: readonly TransactionSummary[],
    filter: ActivityFilter,
  ): readonly TransactionSummary[] {
    return transactions.filter((transaction) => {
      if (filter === 'SALES') {
        return this.isSalesActivity(transaction);
      }

      if (filter === 'SESSIONS') {
        return this.isSessionActivity(transaction);
      }

      return this.isSalesActivity(transaction) || this.isSessionActivity(transaction);
    });
  }

  private isSalesActivity(transaction: TransactionSummary): boolean {
    return (
      transaction.transactionType === 'SESSION_PURCHASE' ||
      transaction.transactionType === 'PLAN_ACTIVATION' ||
      transaction.transactionType === 'EXTRA_PRINT_ADD_ON'
    );
  }

  private isSessionActivity(transaction: TransactionSummary): boolean {
    return (
      transaction.transactionType === 'SESSION_PURCHASE' ||
      transaction.transactionType === 'COVERED_PLAN_SESSION'
    );
  }

  private coveredSessionDetail(transaction: TransactionSummary): string {
    const activation =
      this.selectedActivationFor(transaction.boothId) ??
      this.overview()?.activations.find(
        (item) => item.boothId === transaction.boothId && item.status === 'COMPLETED',
      ) ??
      null;
    if (transaction.offerType === 'SESSION_COUNT') {
      const sessionAllowance =
        activation?.sessionAllowance ?? transaction.sessionAllowance ?? undefined;
      if (transaction.coveredSessionSequence && sessionAllowance) {
        return `${transaction.coveredSessionSequence} of ${sessionAllowance} used`;
      }

      return sessionAllowance ? `${sessionAllowance} session package` : 'Session count package';
    }

    if (transaction.offerType === 'TIME_UNLIMITED') {
      return 'Unlimited package';
    }

    return this.readableType(transaction.offerType ?? transaction.transactionType);
  }

  private withCancellationDetail(detail: string, transaction: TransactionSummary): string {
    const cancellationDetail = this.cancellationDetailFor(transaction);
    return cancellationDetail ? `${detail} / ${cancellationDetail}` : detail;
  }

  private cancellationActorLabel(
    actorType: string | null | undefined,
    cancelledByUserId: string | null | undefined,
  ): string {
    switch (actorType) {
      case 'BOOTH_USER':
        return 'Cancelled by booth user';
      case 'CASHIER': {
        if (!cancelledByUserId) {
          return 'Cancelled by cashier';
        }

        const userName = this.userNameFor(cancelledByUserId);
        return userName === 'Unknown user'
          ? 'Cancelled by cashier'
          : `Cancelled by cashier ${userName}`;
      }
      case 'SYSTEM':
        return 'Cancelled by system';
      default:
        return 'Cancelled / Actor not tracked';
    }
  }

  private cancellationSourceLabel(source: string | null | undefined): string {
    switch (source) {
      case 'BOOTH_UI_PAYMENT_OPTIONS_BACK':
        return 'Payment options';
      case 'BOOTH_UI_PAYMENT_OPTIONS_IDLE_TIMEOUT':
        return 'Payment options idle timeout';
      case 'BOOTH_UI_WAITING_FOR_PAYMENT_BACK':
        return 'Waiting for payment';
      case 'CASHIER_POS_CANCEL_TRANSACTION':
        return 'Cashier POS';
      case 'CASHIER_POS_RETURN_TO_WELCOME':
        return 'Return to welcome';
      case 'SYSTEM_EXTRA_PRINT_TIMEOUT':
        return 'Extra print timeout';
      default:
        return 'Source not tracked';
    }
  }

  private parseAuditMetadata(metadata: string): Record<string, unknown> {
    try {
      const parsed = JSON.parse(metadata) as unknown;
      return parsed && typeof parsed === 'object' && !Array.isArray(parsed)
        ? (parsed as Record<string, unknown>)
        : {};
    } catch {
      return {};
    }
  }

  private metadataText(metadata: Record<string, unknown>, key: string): string | null {
    const value = metadata[key] ?? metadata[key.charAt(0).toLowerCase() + key.slice(1)];
    return typeof value === 'string' && value.trim() ? value.trim() : null;
  }

  private compactAuditMetadata(metadata: Record<string, unknown>): string {
    return Object.entries(metadata)
      .filter(([, value]) => value !== null && value !== undefined && value !== '')
      .map(([key, value]) => `${this.readableType(key)}: ${String(value)}`)
      .join(' / ');
  }

  private readableType(value: string): string {
    return value
      .split('_')
      .filter(Boolean)
      .map((part) => part.charAt(0) + part.slice(1).toLowerCase())
      .join(' ');
  }

  private dashboardActivationFor(boothId: string): OfferActivationSummary | null {
    return this.selectedActivationFor(boothId);
  }

  private latestCoveredSessionForActivation(activationId: string): TransactionSummary | null {
    return (
      this.overview()
        ?.transactions.filter(
          (transaction) =>
            transaction.boothOfferActivationId === activationId &&
            transaction.transactionType === 'COVERED_PLAN_SESSION' &&
            transaction.status === 'COMPLETED',
        )
        .sort(
          (left, right) =>
            new Date(right.completedAt ?? right.createdAt).getTime() -
            new Date(left.completedAt ?? left.createdAt).getTime(),
        )[0] ?? null
    );
  }

  private timedPackageStatusFor(activation: OfferActivationSummary, offer: OfferSummary): string {
    if (!activation.endsAt) {
      return `${offer.durationHours ?? 0} hour timed package`;
    }

    const endsAt = new Date(activation.endsAt);
    const remainingMinutes = Math.max(0, Math.ceil((endsAt.getTime() - Date.now()) / 60_000));
    const totalMinutes =
      offer.durationHours !== null
        ? offer.durationHours * 60
        : activation.startsAt
          ? Math.max(
              0,
              Math.round((endsAt.getTime() - new Date(activation.startsAt).getTime()) / 60_000),
            )
          : 0;

    return `${remainingMinutes} mins left of ${totalMinutes} mins / expires ${this.formatPhilippinesDateTime(
      activation.endsAt,
    )}`;
  }

  private syncUserDetail(user: UserSummary): void {
    this.selectedUserDetailId.set(user.id);
    this.userDetailName.set(user.name);
    this.userDetailRole.set(user.role);
    this.userDetailPermissions.set(this.permissionStateFor(user));
  }

  private permissionStateFor(user: UserSummary): Record<CashierPermissionKey, boolean> {
    return {
      approveCash: user.canApproveCash !== false,
      returnBoothToWelcome: user.canReturnBoothToWelcome !== false,
      cancelTransaction: user.canCancelTransaction !== false,
    };
  }

  private cashierPermissionPayload(
    role: string,
    permissions: Record<CashierPermissionKey, boolean>,
  ): {
    canApproveCash: boolean;
    canReturnBoothToWelcome: boolean;
    canCancelTransaction: boolean;
  } {
    return {
      canApproveCash: role === 'CASHIER' ? permissions.approveCash : this.isPosAssignableRole(role),
      canReturnBoothToWelcome:
        role === 'CASHIER' ? permissions.returnBoothToWelcome : this.isPosAssignableRole(role),
      canCancelTransaction:
        role === 'CASHIER' ? permissions.cancelTransaction : this.isPosAssignableRole(role),
    };
  }

  private isPosAssignableRole(role: string): boolean {
    return role === 'CLIENT_OWNER' || role === 'CLIENT_ADMIN' || role === 'CASHIER';
  }

  private resetChangePasswordForm(): void {
    this.changePasswordCurrent.set('');
    this.changePasswordNew.set('');
    this.changePasswordConfirm.set('');
  }

  private async createUser(
    role: string,
    assignedBoothId: string | null,
    name: string,
    email: string,
    successMessage: string,
    permissions: Record<CashierPermissionKey, boolean> = {
      approveCash: true,
      returnBoothToWelcome: true,
      cancelTransaction: true,
    },
  ): Promise<boolean> {
    if (!this.selectedClientId()) {
      this.fail('Create a client first.');
      return false;
    }

    return await this.run(
      async () => {
        await firstValueFrom(
          this.http.post(
            `${AdminWorkspace.apiBaseUrl}/api/admin/users`,
            {
              clientAccountId: this.selectedClientId(),
              assignedBoothId,
              name,
              email,
              role,
              ...this.cashierPermissionPayload(role, permissions),
            },
            { withCredentials: true },
          ),
        );
        await this.loadOverview();
        this.succeed(successMessage);
        this.userModalOpen.set(false);
      },
      { errorMessage: 'User creation failed.' },
    );
  }

  private async restoreSession(): Promise<void> {
    await this.run(
      async () => {
        const session = await firstValueFrom(
          this.http.get<Session>(`${AdminWorkspace.apiBaseUrl}/api/auth/session`, {
            withCredentials: true,
          }),
        );
        this.session.set(session);
        if (session.mustChangePassword) {
          this.overview.set(null);
          return;
        }

        await this.loadOverview();
      },
      { errorMessage: 'Session restore failed.', notifyError: false },
    );

    if (!this.session()) {
      this.session.set(null);
    }
  }

  private async loadOverview(): Promise<void> {
    const overview = await firstValueFrom(
      this.http.get<Overview>(`${AdminWorkspace.apiBaseUrl}/api/admin/overview`, {
        withCredentials: true,
      }),
    );
    this.overview.set(overview);
    this.session.set(overview.session);

    if (!this.canAccessView(this.activeView())) {
      this.setView('dashboard');
    }
  }

  dismissToast(id: number): void {
    this.removeToast(id);
  }

  private isTransactionBeforeLumaboothSession(transaction: TransactionSummary): boolean {
    return (
      this.isSessionTransaction(transaction) &&
      (transaction.status === 'CREATED' ||
        transaction.status === 'PENDING_CASH' ||
        transaction.status === 'PAID')
    );
  }

  private isTransactionInLumaboothSession(transaction: TransactionSummary): boolean {
    return transaction.status === 'STARTING_SESSION' || transaction.status === 'IN_SESSION';
  }

  private resolveExtraPrintReferenceTransaction(
    orderedTransactions: readonly TransactionSummary[],
  ): TransactionSummary | null {
    for (const transaction of orderedTransactions) {
      if (this.isTransactionInLumaboothSession(transaction)) {
        return null;
      }

      if (this.isTransactionBeforeLumaboothSession(transaction)) {
        continue;
      }

      if (!this.isSessionTransaction(transaction)) {
        if (!this.isTerminalTransaction(transaction.status)) {
          return null;
        }

        continue;
      }

      return transaction;
    }

    return null;
  }

  private isSessionTransaction(transaction: TransactionSummary): boolean {
    return (
      transaction.transactionType === 'SESSION_PURCHASE' ||
      transaction.transactionType === 'COVERED_PLAN_SESSION'
    );
  }

  private async run(operation: () => Promise<void>, options: RunOptions = {}): Promise<boolean> {
    const showBusy = options.showBusy ?? true;
    if (showBusy) {
      this.beginBusy();
    }
    this.error.set('');

    try {
      await operation();
      return true;
    } catch (error) {
      const message = this.getRequestErrorMessage(error, options.errorMessage ?? 'Request failed.');
      this.error.set(message);
      if (options.notifyError !== false) {
        this.showToast('error', message);
      }
      return false;
    } finally {
      if (showBusy) {
        this.endBusy();
      }
    }
  }

  private fail(message: string): void {
    this.error.set(message);
    this.showToast('error', message);
  }

  private succeed(message: string): void {
    this.error.set('');
    this.clearToasts('error');
    this.showToast('success', message);
  }

  private beginBusy(): void {
    this.busyRequests.update((count) => count + 1);
  }

  private endBusy(): void {
    this.busyRequests.update((count) => Math.max(0, count - 1));
  }

  private showToast(kind: ToastKind, message: string): void {
    const normalizedMessage = message.trim();
    if (!normalizedMessage) {
      return;
    }

    const timeout = kind === 'error' ? 7000 : 4500;
    this.snackBar.open(normalizedMessage, 'Dismiss', {
      duration: timeout,
      panelClass: [`snackbar-${kind}`],
      verticalPosition: 'top',
      horizontalPosition: 'right',
    });
  }

  private removeToast(id: number): void {
    const timer = this.toastTimers.get(id);
    if (timer !== undefined) {
      window.clearTimeout(timer);
      this.toastTimers.delete(id);
    }

    this.toasts.update((toasts) => toasts.filter((toast) => toast.id !== id));
  }

  private clearToasts(kind: ToastKind): void {
    this.toasts.update((toasts) => toasts.filter((toast) => toast.kind !== kind));
    this.snackBar.dismiss();
  }

  private getRequestErrorMessage(error: unknown, fallback: string): string {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 0) {
        return `${fallback} The API is unreachable.`;
      }

      const apiMessage = this.getApiErrorMessage(error.error);
      if (apiMessage && error.status !== 401 && error.status !== 403) {
        return `${fallback} ${apiMessage}`;
      }

      return fallback;
    }

    if (error instanceof Error && error.message) {
      return `${fallback} ${error.message}`;
    }

    return fallback;
  }

  private getApiErrorMessage(errorBody: unknown): string {
    if (typeof errorBody === 'string') {
      return errorBody;
    }

    if (!errorBody || typeof errorBody !== 'object') {
      return '';
    }

    const body = errorBody as {
      detail?: unknown;
      errors?: Record<string, unknown>;
      title?: unknown;
    };
    if (typeof body.detail === 'string') {
      return body.detail;
    }

    if (body.errors) {
      for (const value of Object.values(body.errors)) {
        if (Array.isArray(value) && typeof value[0] === 'string') {
          return value[0];
        }

        if (typeof value === 'string') {
          return value;
        }
      }
    }

    return typeof body.title === 'string' ? body.title : '';
  }
}
