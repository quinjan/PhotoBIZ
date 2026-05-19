import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterOutlet } from '@angular/router';
import { BoothStageComponent, BoothStageConfig } from '@photobiz/booth-stage';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { firstValueFrom, interval } from 'rxjs';

type Session = {
  readonly userId: string;
  readonly name: string;
  readonly email: string;
  readonly role: string;
  readonly clientAccountId: string | null;
  readonly assignedBoothId: string | null;
  readonly canApproveCash: boolean;
  readonly canReturnBoothToWelcome: boolean;
  readonly canCancelTransaction: boolean;
};

type ClientSummary = { readonly id: string; readonly name: string; readonly status: string };
type SubscriptionSummary = {
  readonly id: string;
  readonly clientAccountId: string;
  readonly subscriptionPlanId: string;
  readonly status: string;
  readonly activeBoothAllowance: number;
};
type SubscriptionPlanSummary = {
  readonly id: string;
  readonly name: string;
  readonly pricePerBoothCents: number;
  readonly currency: string;
  readonly active: boolean;
};
type UserSummary = {
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
type LocationSummary = {
  readonly id: string;
  readonly clientAccountId: string;
  readonly name: string;
  readonly address: string | null;
  readonly status: string;
};
type BoothSummary = {
  readonly id: string;
  readonly clientAccountId: string;
  readonly locationId: string;
  readonly name: string;
  readonly code: string;
  readonly status: string;
  readonly currentState: string;
  readonly lastHeartbeatAt: string | null;
};
type OfferSummary = {
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
type PrintEntitlementSummary = {
  readonly id: string;
  readonly clientAccountId: string;
  readonly name: string;
  readonly status: string;
};
type OfferActivationSummary = {
  readonly id: string;
  readonly boothId: string;
  readonly boothOfferId: string;
  readonly status: string;
  readonly startsAt: string | null;
  readonly endsAt: string | null;
  readonly sessionAllowance: number | null;
  readonly sessionsUsed: number;
};
type PaymentAssignmentSummary = {
  readonly id: string;
  readonly boothId: string;
  readonly paymentMethod: string;
  readonly runtimeEnabled: boolean;
  readonly status: string;
};
type BoothAppearanceSummary = {
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
};
type TransactionSummary = {
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
};
type ActivityFilter = 'ALL' | 'SALES' | 'SESSIONS';
type TransactionActivityDisplay = {
  readonly title: string;
  readonly detail: string;
  readonly auditText: string;
  readonly value: string;
};
type BoothPackageStatusDisplay = {
  readonly packageName: string;
  readonly detail: string;
};
type ReportSummary = {
  readonly platform: {
    readonly activeClients: number;
    readonly activeBooths: number;
    readonly offlineBooths: number;
    readonly trialSubscriptions: number;
    readonly activeSubscriptions: number;
    readonly pastDueSubscriptions: number;
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
type AuditLogSummary = {
  readonly id: string;
  readonly clientAccountId: string | null;
  readonly userId: string | null;
  readonly action: string;
  readonly entityType: string;
  readonly entityId: string | null;
  readonly metadata: string;
  readonly createdAt: string;
};

type CashierPermissionKey = 'approveCash' | 'returnBoothToWelcome' | 'cancelTransaction';
type BoothDetailTab = 'details' | 'session';
type PageInfo = {
  readonly page: number;
  readonly totalPages: number;
  readonly start: number;
  readonly end: number;
  readonly total: number;
  readonly hasPrevious: boolean;
  readonly hasNext: boolean;
};

type Overview = {
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
  readonly paymentAssignments: readonly PaymentAssignmentSummary[];
  readonly appearanceConfigs: readonly BoothAppearanceSummary[];
  readonly transactions: readonly TransactionSummary[];
  readonly reports: ReportSummary;
  readonly auditLogs: readonly AuditLogSummary[];
};

type BoothSecret = {
  readonly boothId: string;
  readonly boothName: string;
  readonly boothCode: string;
  readonly kioskToken: string;
  readonly agentCredential: string;
};

type ViewKey =
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
  | 'pos'
  | 'reports'
  | 'audit';

@Component({
  selector: 'app-root',
  imports: [CommonModule, FormsModule, RouterOutlet, BoothStageComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  private static readonly apiBaseUrl = 'http://localhost:5082';
  private readonly http = inject(HttpClient);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly session = signal<Session | null>(null);
  protected readonly overview = signal<Overview | null>(null);
  protected readonly activeView = signal<ViewKey>('dashboard');
  protected readonly message = signal('Sign in to manage PhotoBIZ operations.');
  protected readonly error = signal('');
  protected readonly loading = signal(false);
  protected readonly boothSecret = signal<BoothSecret | null>(null);
  protected readonly gridPageSize = 5;
  private readonly gridPages = signal<Record<string, number>>({});
  protected readonly dashboardActivityFilter = signal<ActivityFilter>('ALL');
  protected readonly cashierActivityFilter = signal<ActivityFilter>('ALL');
  protected readonly activityFilters: readonly {
    readonly value: ActivityFilter;
    readonly label: string;
  }[] = [
    { value: 'ALL', label: 'All' },
    { value: 'SALES', label: 'Sales' },
    { value: 'SESSIONS', label: 'Sessions' },
  ];

  protected readonly loginEmail = signal('owner@photobiz.local');
  protected readonly loginPassword = signal('PhotoBIZ!123');
  protected readonly clientName = signal('The Memory Box');
  protected readonly planName = signal('Starter Booth');
  protected readonly planPrice = signal(150000);
  protected readonly subscriptionActive = signal(true);
  protected readonly subscriptionAllowance = signal(2);
  protected readonly locationModalOpen = signal(false);
  protected readonly selectedLocationDetailId = signal<string | null>(null);
  protected readonly locationDetailName = signal('');
  protected readonly boothName = signal('Booth A');
  protected readonly boothCode = signal('SMA-001');
  protected readonly ownerName = signal('Client Owner');
  protected readonly ownerEmail = signal('owner@memorybox.local');
  protected readonly ownerPassword = signal('PhotoBIZ!123');
  protected readonly cashierName = signal('Cashier');
  protected readonly cashierEmail = signal('cashier@memorybox.local');
  protected readonly cashierPassword = signal('PhotoBIZ!123');
  protected readonly selectedPackageDetailId = signal<string | null>(null);
  protected readonly packageName = signal('Per Session');
  protected readonly packageDescription = signal('Standard booth session');
  protected readonly packageOfferType = signal('PER_SESSION');
  protected readonly packagePriceCents = signal(25000);
  protected readonly packagePrintEntitlement = signal('2 pcs 6x2 or 1 pc 6x4');
  protected readonly packageDurationHours = signal(1);
  protected readonly packageSessionAllowance = signal(3);
  protected readonly packageExtraPrintPriceCents = signal(5000);
  protected readonly packageLumaBoothMode = signal('PRINT');
  protected readonly packageActive = signal(true);
  protected readonly printEntitlementModalOpen = signal(false);
  protected readonly selectedPrintEntitlementDetailId = signal<string | null>(null);
  protected readonly printEntitlementName = signal('');
  protected readonly extraPrintCopies = signal(1);
  protected readonly clientSearch = signal('');
  protected readonly clientModalOpen = signal(false);
  protected readonly selectedClientDetailId = signal<string | null>(null);
  protected readonly subscriptionModalOpen = signal(false);
  protected readonly selectedSubscriptionDetailId = signal<string | null>(null);
  protected readonly subscriptionClientId = signal<string | null>(null);
  protected readonly subscriptionPlanId = signal<string | null>(null);
  protected readonly subscriptionStatus = signal('ACTIVE');
  protected readonly userModalOpen = signal(false);
  protected readonly selectedUserDetailId = signal<string | null>(null);
  protected readonly userDetailName = signal('');
  protected readonly userDetailRole = signal('CLIENT_ADMIN');
  protected readonly userDetailPermissions = signal<Record<CashierPermissionKey, boolean>>({
    approveCash: true,
    returnBoothToWelcome: true,
    cancelTransaction: true,
  });
  protected readonly newUserName = signal('');
  protected readonly newUserEmail = signal('');
  protected readonly newUserRole = signal('CLIENT_ADMIN');
  protected readonly cashierPermissions = signal<Record<CashierPermissionKey, boolean>>({
    approveCash: true,
    returnBoothToWelcome: true,
    cancelTransaction: true,
  });
  protected readonly cashierPermissionRows: readonly {
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
  protected readonly boothModalOpen = signal(false);
  protected readonly boothLocationId = signal<string | null>(null);
  protected readonly boothCashierUserId = signal<string | null>(null);
  protected readonly selectedBoothDetailId = signal<string | null>(null);
  protected readonly boothDetailName = signal('');
  protected readonly boothDetailCode = signal('');
  protected readonly boothDetailLocationId = signal<string | null>(null);
  protected readonly boothDetailCashierUserId = signal<string | null>(null);
  protected readonly boothDetailStatus = signal('ACTIVE');
  protected readonly boothDetailOfferId = signal<string | null>(null);
  protected readonly boothDetailTab = signal<BoothDetailTab>('details');
  protected readonly boothAppearanceSessionLabel = signal('Self photo booth');
  protected readonly boothAppearanceHeadline = signal('Step Into The Memory Box');
  protected readonly boothAppearanceSubtitle = signal(
    "Review today's booth offer, pay at the counter, then strike your best pose.",
  );
  protected readonly boothAppearanceThemePreset = signal('VINTAGE');
  protected readonly boothAppearanceBackgroundImageDataUrl = signal('');
  protected readonly boothThemePresets: readonly { readonly value: string; readonly label: string }[] = [
    { value: 'VINTAGE', label: 'Vintage' },
    { value: 'CLEAN_MODERN', label: 'Clean Modern' },
    { value: 'POP', label: 'Pop' },
  ];

  protected readonly selectedClientId = computed(() => this.overview()?.clients[0]?.id ?? null);
  protected readonly selectedPlanId = computed(
    () => this.overview()?.subscriptionPlans[0]?.id ?? null,
  );
  protected readonly selectedLocationId = computed(() => this.overview()?.locations[0]?.id ?? null);
  protected readonly selectedLocationDetail = computed(() => {
    const selectedId = this.selectedLocationDetailId();
    return this.overview()?.locations.find((location) => location.id === selectedId) ?? null;
  });
  protected readonly selectedBoothId = computed(() => this.overview()?.booths[0]?.id ?? null);
  protected readonly selectedOfferId = computed(
    () =>
      this.overview()?.offers.find((offer) => offer.active)?.id ??
      this.overview()?.offers[0]?.id ??
      null,
  );
  protected readonly activeBooths = computed(
    () => this.overview()?.booths.filter((booth) => booth.status === 'ACTIVE') ?? [],
  );
  protected readonly offlineBooths = computed(() =>
    this.activeBooths().filter((booth) => booth.currentState === 'OFFLINE'),
  );
  protected readonly pendingTransactions = computed(
    () =>
      this.overview()?.transactions.filter(
        (transaction) => transaction.status === 'PENDING_CASH',
      ) ?? [],
  );
  protected readonly completedTransactions = computed(
    () =>
      this.overview()?.transactions.filter((transaction) => transaction.status === 'COMPLETED') ??
      [],
  );
  protected readonly grossSalesCents = computed(() =>
    this.completedTransactions().reduce((total, transaction) => total + transaction.amountCents, 0),
  );
  protected readonly assignedBooth = computed(() => {
    const assignedBoothId = this.session()?.assignedBoothId;
    const booths = this.overview()?.booths ?? [];
    return booths.find((booth) => booth.id === assignedBoothId) ?? booths[0] ?? null;
  });
  protected readonly dashboardActivityTransactions = computed(() =>
    this.filterActivityTransactions(
      this.overview()?.transactions ?? [],
      this.dashboardActivityFilter(),
    ),
  );
  protected readonly cashierActivityTransactions = computed(() => {
    const boothId = this.assignedBooth()?.id;
    const transactions =
      this.overview()?.transactions.filter((transaction) => transaction.boothId === boothId) ?? [];

    return this.filterActivityTransactions(transactions, this.cashierActivityFilter());
  });
  protected readonly cashierTransaction = computed(() => {
    const boothId = this.assignedBooth()?.id;
    return (
      this.overview()?.transactions.find(
        (transaction) =>
          boothId &&
          transaction.boothId === boothId &&
          !this.isTerminalTransaction(transaction.status),
      ) ?? null
    );
  });
  protected readonly pendingPlanActivation = computed(() => {
    const boothId = this.assignedBooth()?.id;
    if (!boothId || this.cashierTransaction()) {
      return null;
    }

    return this.pendingActivationFor(boothId);
  });
  protected readonly pendingPlanActivationOffer = computed(() => {
    const activation = this.pendingPlanActivation();
    return activation ? this.offerForActivation(activation) : null;
  });
  protected readonly extraPrintCandidate = computed(() => {
    const boothId = this.assignedBooth()?.id;
    if (this.pendingPlanActivation()) {
      return null;
    }

    return (
      this.overview()?.transactions.find(
        (transaction) =>
          boothId && transaction.boothId === boothId && transaction.canCreateExtraPrintAddOn,
      ) ?? null
    );
  });
  protected readonly extraPrintTotalCents = computed(() => {
    const candidate = this.extraPrintCandidate();
    return (candidate?.extraPrintUnitPriceCents ?? 0) * this.extraPrintCopies();
  });
  protected readonly isApplicationOwner = computed(
    () => this.session()?.role === 'APPLICATION_OWNER',
  );
  protected readonly platformClients = computed(() => {
    const search = this.clientSearch().trim().toLowerCase();
    const clients = this.overview()?.clients ?? [];

    if (!search) {
      return clients;
    }

    return clients.filter((client) => {
      const owner = this.ownerForClient(client.id);
      const subscription = this.latestSubscriptionFor(client.id);
      return [client.name, client.status, owner?.name, owner?.email, subscription?.status]
        .filter(Boolean)
        .some((value) => String(value).toLowerCase().includes(search));
    });
  });
  protected readonly subscriptionAuditLogs = computed(
    () =>
      this.overview()?.auditLogs.filter(
        (audit) =>
          audit.entityType === 'ClientSubscription' ||
          audit.action.startsWith('client_subscription.'),
      ) ?? [],
  );
  protected readonly selectedClient = computed(() => {
    const clients = this.overview()?.clients ?? [];
    const selectedId = this.selectedClientDetailId() ?? clients[0]?.id ?? null;
    return clients.find((client) => client.id === selectedId) ?? null;
  });
  protected readonly currentClient = computed<ClientSummary | null>(() => {
    const clientAccountId = this.session()?.clientAccountId;
    const clients = this.overview()?.clients ?? [];
    return clients.find((client) => client.id === clientAccountId) ?? clients[0] ?? null;
  });
  protected readonly clientOwners = computed(
    () => this.overview()?.users.filter((user) => user.role === 'CLIENT_OWNER') ?? [],
  );
  protected readonly clientAdmins = computed(
    () => this.overview()?.users.filter((user) => user.role === 'CLIENT_ADMIN') ?? [],
  );
  protected readonly cashiers = computed(
    () => this.overview()?.users.filter((user) => user.role === 'CASHIER') ?? [],
  );
  protected readonly availableCashiers = computed(() =>
    this.cashiers().filter((user) => !user.assignedBoothId),
  );
  protected readonly boothDetailCashierOptions = computed(() => {
    const selectedBoothId = this.selectedBoothDetailId();
    return this.cashiers().filter(
      (user) => !user.assignedBoothId || user.assignedBoothId === selectedBoothId,
    );
  });
  protected readonly inactiveUsers = computed(
    () => this.overview()?.users.filter((user) => user.status !== 'ACTIVE') ?? [],
  );
  protected readonly selectedUser = computed(() => {
    const users = this.overview()?.users ?? [];
    const selectedId = this.selectedUserDetailId();
    return users.find((user) => user.id === selectedId) ?? null;
  });
  protected readonly canApproveCashAction = computed(() => {
    const session = this.session();
    return session?.role !== 'CASHIER' || session.canApproveCash !== false;
  });
  protected readonly canReturnBoothToWelcomeAction = computed(() => {
    const session = this.session();
    return session?.role !== 'CASHIER' || session.canReturnBoothToWelcome !== false;
  });
  protected readonly canCancelTransactionAction = computed(() => {
    const session = this.session();
    return session?.role !== 'CASHIER' || session.canCancelTransaction !== false;
  });
  protected readonly activeOffers = computed(
    () => this.overview()?.offers.filter((offer) => offer.active) ?? [],
  );
  protected readonly selectedBoothDetail = computed(() => {
    const selectedId = this.selectedBoothDetailId();
    return this.overview()?.booths.find((booth) => booth.id === selectedId) ?? null;
  });
  protected readonly selectedBoothCashier = computed(() => {
    const boothId = this.selectedBoothDetailId();
    return this.cashiers().find((cashier) => cashier.assignedBoothId === boothId) ?? null;
  });
  protected readonly selectedBoothAppearance = computed(() => {
    const boothId = this.selectedBoothDetailId();
    return (
      this.overview()?.appearanceConfigs?.find((appearance) => appearance.boothId === boothId) ??
      null
    );
  });
  protected readonly selectedBoothSecret = computed(() => {
    const booth = this.selectedBoothDetail();
    const secret = this.boothSecret();
    return booth && secret?.boothId === booth.id ? secret : null;
  });
  protected readonly boothPreviewConfig = computed<BoothStageConfig | null>(() => {
    const booth = this.selectedBoothDetail();
    const client = this.currentClient();

    if (!booth || !client) {
      return null;
    }

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
      },
      booth: { id: booth.id, state: booth.currentState },
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
            activationStatus:
              this.selectedActivationFor(booth.id)?.status ?? 'ACTIVE',
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
    };
  });
  protected readonly selectedPackage = computed(() => {
    const selectedId = this.selectedPackageDetailId();
    return this.overview()?.offers.find((offer) => offer.id === selectedId) ?? null;
  });
  protected readonly printEntitlements = computed(() => {
    const entitlements = this.overview()?.printEntitlements ?? [];

    if (entitlements.length > 0) {
      return entitlements;
    }

    return [
      {
        id: 'default-combo',
        clientAccountId: this.selectedClientId() ?? '',
        name: '2 pcs 6x2 or 1 pc 6x4',
        status: 'ACTIVE',
      },
      {
        id: 'default-2x6',
        clientAccountId: this.selectedClientId() ?? '',
        name: '2 pcs 6x2',
        status: 'ACTIVE',
      },
      {
        id: 'default-1x4',
        clientAccountId: this.selectedClientId() ?? '',
        name: '1 pc 6x4',
        status: 'ACTIVE',
      },
    ];
  });
  protected readonly packagePrintEntitlementOptions = computed(() => {
    const names = this.printEntitlements()
      .filter((entitlement) => entitlement.status === 'ACTIVE')
      .map((entitlement) => entitlement.name);
    const selectedName = this.packagePrintEntitlement();

    if (selectedName && !names.includes(selectedName)) {
      names.unshift(selectedName);
    }

    return names;
  });
  protected readonly selectedPrintEntitlement = computed(() => {
    const selectedId = this.selectedPrintEntitlementDetailId();
    return this.printEntitlements().find((entitlement) => entitlement.id === selectedId) ?? null;
  });
  protected isPersistedPrintEntitlement(entitlement: PrintEntitlementSummary | null): boolean {
    return Boolean(entitlement && !entitlement.id.startsWith('default-'));
  }
  protected readonly selectedSubscriptionDefinition = computed(() => {
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
  protected readonly visibleNavItems = computed(() =>
    this.navItems.filter((item) => this.canAccessView(item.key)),
  );

  constructor() {
    this.restoreSession();

    interval(5000)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        if (this.session()) {
          this.loadOverview();
        }
      });
  }

  protected async login(): Promise<void> {
    await this.run(async () => {
      const session = await firstValueFrom(
        this.http.post<Session>(
          `${App.apiBaseUrl}/api/auth/login`,
          { email: this.loginEmail(), password: this.loginPassword() },
          { withCredentials: true },
        ),
      );

      this.session.set(session);
      await this.loadOverview();
      this.message.set('Signed in.');
    });
  }

  protected async logout(): Promise<void> {
    await firstValueFrom(
      this.http.post(`${App.apiBaseUrl}/api/auth/logout`, {}, { withCredentials: true }),
    );
    this.session.set(null);
    this.overview.set(null);
    this.message.set('Signed out.');
  }

  protected setView(view: ViewKey): void {
    if (!this.canAccessView(view)) {
      return;
    }

    this.activeView.set(view);
  }

  protected pagedItems<T>(key: string, items: readonly T[] | null | undefined): readonly T[] {
    const safeItems = items ?? [];
    const page = this.clampedGridPage(key, safeItems.length);
    const start = (page - 1) * this.gridPageSize;

    return safeItems.slice(start, start + this.gridPageSize);
  }

  protected pageInfo(key: string, totalItems: number | null | undefined): PageInfo {
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

  protected previousPage(key: string, totalItems: number | null | undefined): void {
    const page = this.clampedGridPage(key, Math.max(0, totalItems ?? 0));
    this.setGridPage(key, page - 1, totalItems);
  }

  protected nextPage(key: string, totalItems: number | null | undefined): void {
    const page = this.clampedGridPage(key, Math.max(0, totalItems ?? 0));
    this.setGridPage(key, page + 1, totalItems);
  }

  protected setDashboardActivityFilter(filter: ActivityFilter): void {
    this.dashboardActivityFilter.set(filter);
    this.setGridPage('dashboard-activity', 1, this.dashboardActivityTransactions().length);
  }

  protected setCashierActivityFilter(filter: ActivityFilter): void {
    this.cashierActivityFilter.set(filter);
    this.setGridPage('cashier-activity', 1, this.cashierActivityTransactions().length);
  }

  protected boothsForClient(clientId: string): readonly BoothSummary[] {
    return this.overview()?.booths.filter((booth) => booth.clientAccountId === clientId) ?? [];
  }

  protected async createClient(): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(
        this.http.post(
          `${App.apiBaseUrl}/api/admin/clients`,
          { name: this.clientName() },
          { withCredentials: true },
        ),
      );
      await this.loadOverview();
      this.message.set('Client account created.');
    });
  }

  protected openClientModal(): void {
    this.clientModalOpen.set(true);
  }

  protected closeClientModal(): void {
    this.clientModalOpen.set(false);
  }

  protected openSubscriptionModal(clientId?: string): void {
    this.subscriptionClientId.set(clientId ?? this.selectedClientId());
    this.subscriptionPlanId.set(this.selectedPlanId());
    this.subscriptionStatus.set('ACTIVE');
    this.subscriptionModalOpen.set(true);
  }

  protected closeSubscriptionModal(): void {
    this.subscriptionModalOpen.set(false);
  }

  protected openUserModal(): void {
    this.newUserName.set('');
    this.newUserEmail.set('');
    this.newUserRole.set(this.session()?.role === 'CLIENT_ADMIN' ? 'CLIENT_ADMIN' : 'CLIENT_ADMIN');
    this.cashierPermissions.set({
      approveCash: true,
      returnBoothToWelcome: true,
      cancelTransaction: true,
    });
    this.userModalOpen.set(true);
  }

  protected closeUserModal(): void {
    this.userModalOpen.set(false);
  }

  protected openLocationModal(location?: LocationSummary): void {
    this.selectedLocationDetailId.set(location?.id ?? null);
    this.locationDetailName.set(location?.name ?? '');
    this.locationModalOpen.set(true);
  }

  protected closeLocationModal(): void {
    this.locationModalOpen.set(false);
  }

  protected startNewPackage(): void {
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
    this.activeView.set('package-detail');
  }

  protected viewPackage(offer: OfferSummary): void {
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
    this.activeView.set('package-detail');
  }

  protected setPackagePriceFromPesos(value: string | number): void {
    this.packagePriceCents.set(this.moneyInputToCents(value));
  }

  protected setPackageExtraPrintPriceFromPesos(value: string | number): void {
    this.packageExtraPrintPriceCents.set(this.moneyInputToCents(value));
  }

  protected startNewPrintEntitlement(): void {
    this.selectedPrintEntitlementDetailId.set(null);
    this.printEntitlementName.set('');
    this.printEntitlementModalOpen.set(true);
  }

  protected viewPrintEntitlement(entitlement: PrintEntitlementSummary): void {
    this.selectedPrintEntitlementDetailId.set(entitlement.id);
    this.printEntitlementName.set(entitlement.name);
    this.printEntitlementModalOpen.set(true);
  }

  protected closePrintEntitlementModal(): void {
    this.printEntitlementModalOpen.set(false);
  }

  protected toggleCashierPermission(key: CashierPermissionKey): void {
    this.cashierPermissions.update((permissions) => ({
      ...permissions,
      [key]: !permissions[key],
    }));
  }

  protected toggleUserDetailPermission(key: CashierPermissionKey): void {
    this.userDetailPermissions.update((permissions) => ({
      ...permissions,
      [key]: !permissions[key],
    }));
  }

  protected setUserDetailRole(role: string): void {
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

  protected openBoothModal(locationId?: string): void {
    this.boothLocationId.set(locationId ?? this.selectedLocationId());
    this.boothCashierUserId.set(null);
    this.boothName.set('Booth A');
    this.boothCode.set('SMA-001');
    this.boothSecret.set(null);
    this.boothModalOpen.set(true);
  }

  protected closeBoothModal(): void {
    this.boothModalOpen.set(false);
  }

  protected async onboardClient(): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(
        this.http.post(
          `${App.apiBaseUrl}/api/admin/clients/onboard`,
          {
            clientName: this.clientName(),
            ownerName: this.ownerName(),
            ownerEmail: this.ownerEmail(),
            ownerPassword: this.ownerPassword(),
          },
          { withCredentials: true },
        ),
      );
      await this.loadOverview();
      this.clientModalOpen.set(false);
      this.activeView.set('clients');
      this.message.set('Client and owner credentials created.');
    });
  }

  protected async updateClientStatus(client: ClientSummary, status: string): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(
        this.http.put(
          `${App.apiBaseUrl}/api/admin/clients/${client.id}`,
          { name: client.name, status },
          { withCredentials: true },
        ),
      );
      await this.loadOverview();
      this.message.set('Client status updated.');
    });
  }

  protected async createPlan(): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(
        this.http.post(
          `${App.apiBaseUrl}/api/admin/subscription-plans`,
          { name: this.planName(), pricePerBoothCents: this.planPrice(), currency: 'PHP' },
          { withCredentials: true },
        ),
      );
      await this.loadOverview();
      this.message.set('Subscription created.');
    });
  }

  protected startNewSubscriptionDefinition(): void {
    this.selectedSubscriptionDetailId.set(null);
    this.planName.set('Per Booth MVP');
    this.planPrice.set(200000);
    this.subscriptionActive.set(true);
    this.activeView.set('subscription-detail');
  }

  protected setPlanPriceFromPesos(value: number | string): void {
    this.planPrice.set(Math.max(0, Math.round(Number(value || 0) * 100)));
  }

  protected async saveSubscriptionDefinition(): Promise<void> {
    const selectedSubscription = this.selectedSubscriptionDefinition();

    await this.run(async () => {
      if (selectedSubscription) {
        await firstValueFrom(
          this.http.put(
            `${App.apiBaseUrl}/api/admin/subscription-plans/${selectedSubscription.id}`,
            {
              name: this.planName(),
              pricePerBoothCents: this.planPrice(),
              currency: selectedSubscription.currency,
              active: this.subscriptionActive(),
            },
            { withCredentials: true },
          ),
        );
        this.message.set('Subscription updated.');
      } else {
        await firstValueFrom(
          this.http.post(
            `${App.apiBaseUrl}/api/admin/subscription-plans`,
            { name: this.planName(), pricePerBoothCents: this.planPrice(), currency: 'PHP' },
            { withCredentials: true },
          ),
        );
        this.message.set('Subscription created.');
      }

      await this.loadOverview();
      this.activeView.set('subscriptions');
    });
  }

  protected async assignSubscription(): Promise<void> {
    const clientAccountId = this.subscriptionClientId() ?? this.selectedClientId();
    const subscriptionPlanId = this.subscriptionPlanId() ?? this.selectedPlanId();

    if (!clientAccountId || !subscriptionPlanId) {
      this.error.set('Create a client and subscription first.');
      return;
    }

    await this.run(async () => {
      await firstValueFrom(
        this.http.post(
          `${App.apiBaseUrl}/api/admin/subscriptions`,
          {
            clientAccountId,
            subscriptionPlanId,
            status: this.subscriptionStatus(),
            activeBoothAllowance: this.subscriptionAllowance(),
            notes: 'MVP subscription',
          },
          { withCredentials: true },
        ),
      );
      await this.loadOverview();
      this.subscriptionModalOpen.set(false);
      this.message.set('Client subscription assigned.');
    });
  }

  protected async updateSubscription(
    subscription: SubscriptionSummary,
    status: string,
    allowance = subscription.activeBoothAllowance,
  ): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(
        this.http.put(
          `${App.apiBaseUrl}/api/admin/subscriptions/${subscription.id}`,
          {
            status,
            activeBoothAllowance: allowance,
            endsOn: null,
            notes: 'Updated from Admin Web',
          },
          { withCredentials: true },
        ),
      );
      await this.loadOverview();
      this.message.set('Subscription updated.');
    });
  }

  protected async createOwner(): Promise<void> {
    await this.createUser(
      'CLIENT_OWNER',
      null,
      this.ownerName(),
      this.ownerEmail(),
      this.ownerPassword(),
      'Client owner created.',
    );
  }

  protected async createManagedUser(): Promise<void> {
    await this.createUser(
      this.newUserRole(),
      null,
      this.newUserName(),
      this.newUserEmail(),
      this.cashierPassword(),
      `User created. Temporary password: ${this.cashierPassword()}.`,
      this.cashierPermissions(),
    );
    this.userModalOpen.set(false);
  }

  protected async updateUserStatus(user: UserSummary, status: string): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(
        this.http.put(
          `${App.apiBaseUrl}/api/admin/users/${user.id}`,
          {
            assignedBoothId: user.role === 'CASHIER' ? user.assignedBoothId : null,
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
      this.message.set('User status updated.');
    });
  }

  protected async saveUserDetail(): Promise<void> {
    const user = this.selectedUser();

    if (!user) {
      this.error.set('Select a user first.');
      return;
    }

    const role = this.userDetailRole();

    await this.run(async () => {
      const savedUser = await firstValueFrom(
        this.http.put<UserSummary>(
          `${App.apiBaseUrl}/api/admin/users/${user.id}`,
          {
            assignedBoothId: role === 'CASHIER' ? user.assignedBoothId : null,
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
      this.message.set('User details updated.');
    });
  }

  protected async createLocation(): Promise<void> {
    if (!this.selectedClientId()) {
      this.error.set('Create a client first.');
      return;
    }

    const name = this.locationDetailName().trim();
    if (!name) {
      this.error.set('Enter a location name.');
      return;
    }

    await this.run(async () => {
      await firstValueFrom(
        this.http.post(
          `${App.apiBaseUrl}/api/admin/locations`,
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
      this.message.set('Location created.');
    });
  }

  protected async saveLocationDetail(): Promise<void> {
    const location = this.selectedLocationDetail();
    const name = this.locationDetailName().trim();

    if (!location) {
      await this.createLocation();
      return;
    }

    if (!name) {
      this.error.set('Enter a location name.');
      return;
    }

    await this.run(async () => {
      await firstValueFrom(
        this.http.put(
          `${App.apiBaseUrl}/api/admin/locations/${location.id}`,
          { name, address: location.address, status: location.status },
          { withCredentials: true },
        ),
      );
      await this.loadOverview();
      this.closeLocationModal();
      this.message.set('Location updated.');
    });
  }

  protected async updateLocationStatus(location: LocationSummary, status: string): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(
        this.http.put(
          `${App.apiBaseUrl}/api/admin/locations/${location.id}`,
          { name: location.name, address: location.address, status },
          { withCredentials: true },
        ),
      );
      await this.loadOverview();
      this.message.set('Location status updated.');
    });
  }

  protected async deleteLocation(location: LocationSummary): Promise<void> {
    await this.updateLocationStatus(location, 'INACTIVE');
    this.closeLocationModal();
    this.message.set('Location deleted.');
  }

  protected async restoreLocation(location: LocationSummary): Promise<void> {
    await this.updateLocationStatus(location, 'ACTIVE');
    this.closeLocationModal();
    this.message.set('Location restored.');
  }

  protected async createBooth(): Promise<void> {
    const locationId = this.boothLocationId() ?? this.selectedLocationId();

    if (!this.selectedClientId() || !locationId) {
      this.error.set('Create a client and location first.');
      return;
    }

    await this.run(async () => {
      const response = await firstValueFrom(
        this.http.post<{ booth: BoothSummary; kioskToken: string; agentCredential: string }>(
          `${App.apiBaseUrl}/api/admin/booths`,
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
      this.message.set('Booth created and credentials issued.');
    });
  }

  protected async issueBoothCredentials(): Promise<void> {
    const booth = this.selectedBoothDetail();

    if (!booth) {
      this.error.set('Select a booth first.');
      return;
    }

    await this.run(async () => {
      const response = await firstValueFrom(
        this.http.post<{
          boothId: string;
          boothCode: string;
          kioskToken: string;
          agentCredential: string;
        }>(`${App.apiBaseUrl}/api/admin/booths/${booth.id}/credentials`, {}, { withCredentials: true }),
      );

      this.boothSecret.set({
        boothId: response.boothId,
        boothName: booth.name,
        boothCode: response.boothCode,
        kioskToken: response.kioskToken,
        agentCredential: response.agentCredential,
      });
      this.message.set('Booth credentials issued. Update the Windows Agent with the new credential.');
    });
  }

  protected async updateBoothStatus(booth: BoothSummary, status: string): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(
        this.http.put(
          `${App.apiBaseUrl}/api/admin/booths/${booth.id}`,
          {
            locationId: booth.locationId,
            name: booth.name,
            code: booth.code,
            status,
            cashierUserId: this.assignedCashierFor(booth.id)?.id ?? null,
          },
          { withCredentials: true },
        ),
      );
      await this.loadOverview();
      this.message.set('Booth status updated.');
    });
  }

  protected async saveBoothRecord(): Promise<void> {
    const booth = this.selectedBoothDetail();

    if (!booth || !this.boothDetailLocationId()) {
      this.error.set('Select a booth and location first.');
      return;
    }

    await this.run(async () => {
      const updated = await firstValueFrom(
        this.http.put<BoothSummary>(
          `${App.apiBaseUrl}/api/admin/booths/${booth.id}`,
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
      await this.loadOverview();
      this.syncBoothDetail(updated.id);
      this.message.set('Booth record saved.');
    });
  }

  protected async saveBoothSession(): Promise<void> {
    const booth = this.selectedBoothDetail();

    if (!booth) {
      this.error.set('Select a booth first.');
      return;
    }

    await this.run(async () => {
      await firstValueFrom(
        this.http.put(
          `${App.apiBaseUrl}/api/admin/booths/${booth.id}/appearance`,
          {
            themePreset: this.boothAppearanceThemePreset(),
            sessionLabel: this.boothAppearanceSessionLabel(),
            defaultWelcomeHeadline: this.boothAppearanceHeadline(),
            defaultWelcomeSubtitle: this.boothAppearanceSubtitle(),
            backgroundImageDataUrl: this.boothAppearanceBackgroundImageDataUrl() || null,
          },
          { withCredentials: true },
        ),
      );

      const offerId = this.boothDetailOfferId();
      if (offerId && offerId !== this.selectedOfferFor(booth.id)?.id) {
        await firstValueFrom(
          this.http.post(
            `${App.apiBaseUrl}/api/admin/booths/${booth.id}/activate-offer`,
            { boothOfferId: offerId },
            { withCredentials: true },
          ),
        );
      }

      if (!this.cashAssignmentFor(booth.id)?.runtimeEnabled) {
        await firstValueFrom(
          this.http.post(
            `${App.apiBaseUrl}/api/admin/booths/${booth.id}/payment-options`,
            { paymentMethod: 'CASH', runtimeEnabled: true },
            { withCredentials: true },
          ),
        );
      }

      await this.loadOverview();
      this.syncBoothDetail(booth.id);
      const selectedOffer = this.selectedOfferFor(booth.id);
      this.message.set(
        selectedOffer?.offerType === 'PER_SESSION'
          ? 'Booth session saved.'
          : 'Package saved. Cashier activation is required before customers can start sessions.',
      );
    });
  }

  protected async savePackage(): Promise<void> {
    if (!this.selectedClientId()) {
      this.error.set('Create a client first.');
      return;
    }

    const name = this.packageName().trim();
    if (!name) {
      this.error.set('Package name is required.');
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

    await this.run(async () => {
      if (selectedPackageId) {
        await firstValueFrom(
          this.http.put(
            `${App.apiBaseUrl}/api/admin/offers/${selectedPackageId}`,
            { ...payload, active: this.packageActive() },
            { withCredentials: true },
          ),
        );
      } else {
        await firstValueFrom(
          this.http.post(`${App.apiBaseUrl}/api/admin/offers`, payload, {
            withCredentials: true,
          }),
        );
      }
      await this.loadOverview();
      this.activeView.set('packages');
      this.message.set(selectedPackageId ? 'Package updated.' : 'Package created.');
    });
  }

  protected async updatePackageStatus(offer: OfferSummary, active: boolean): Promise<void> {
    await this.run(async () => {
      const updated = await firstValueFrom(
        this.http.put<OfferSummary>(
          `${App.apiBaseUrl}/api/admin/offers/${offer.id}`,
          { ...offer, active },
          { withCredentials: true },
        ),
      );
      await this.loadOverview();
      this.viewPackage(updated);
      this.message.set(active ? 'Package activated.' : 'Package deactivated.');
    });
  }

  protected async savePrintEntitlement(): Promise<void> {
    if (!this.selectedClientId()) {
      this.error.set('Create a client first.');
      return;
    }

    const name = this.printEntitlementName().trim();
    if (!name) {
      this.error.set('Print entitlement name is required.');
      return;
    }

    const selected = this.selectedPrintEntitlement();
    const shouldUpdate = this.isPersistedPrintEntitlement(selected);

    await this.run(async () => {
      const saved = shouldUpdate && selected
        ? await firstValueFrom(
            this.http.put<PrintEntitlementSummary>(
              `${App.apiBaseUrl}/api/admin/print-entitlements/${selected.id}`,
              { name, status: selected.status },
              { withCredentials: true },
            ),
          )
        : await firstValueFrom(
            this.http.post<PrintEntitlementSummary>(
              `${App.apiBaseUrl}/api/admin/print-entitlements`,
              { clientAccountId: this.selectedClientId(), name },
              { withCredentials: true },
            ),
          );

      await this.loadOverview();
      this.selectedPrintEntitlementDetailId.set(saved.id);
      this.printEntitlementName.set(saved.name);
      this.packagePrintEntitlement.set(saved.name);
      this.printEntitlementModalOpen.set(false);
      this.message.set(shouldUpdate ? 'Print entitlement updated.' : 'Print entitlement created.');
    });
  }

  protected async updatePrintEntitlementStatus(
    entitlement: PrintEntitlementSummary,
    status: string,
  ): Promise<void> {
    if (!this.isPersistedPrintEntitlement(entitlement)) {
      this.error.set('Save this print entitlement before changing its status.');
      return;
    }

    await this.run(async () => {
      const updated = await firstValueFrom(
        this.http.put<PrintEntitlementSummary>(
          `${App.apiBaseUrl}/api/admin/print-entitlements/${entitlement.id}`,
          { name: entitlement.name, status },
          { withCredentials: true },
        ),
      );
      await this.loadOverview();
      this.viewPrintEntitlement(updated);
      this.message.set(
        status === 'ACTIVE' ? 'Print entitlement activated.' : 'Print entitlement deactivated.',
      );
    });
  }

  protected async activateOffer(
    boothId = this.selectedBoothId(),
    offerId = this.selectedOfferId(),
  ): Promise<void> {
    if (!boothId || !offerId) {
      this.error.set('Create a booth and offer first.');
      return;
    }

    await this.run(async () => {
      await firstValueFrom(
        this.http.post(
          `${App.apiBaseUrl}/api/admin/booths/${boothId}/activate-offer`,
          { boothOfferId: offerId },
          { withCredentials: true },
        ),
      );
      await this.loadOverview();
      this.message.set('Offer activated for booth.');
    });
  }

  protected async assignCash(boothId: string): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(
        this.http.post(
          `${App.apiBaseUrl}/api/admin/booths/${boothId}/payment-options`,
          { paymentMethod: 'CASH', runtimeEnabled: true },
          { withCredentials: true },
        ),
      );
      await this.loadOverview();
      this.message.set('Cash enabled for booth.');
    });
  }

  protected async disablePayment(assignment: PaymentAssignmentSummary): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(
        this.http.delete(
          `${App.apiBaseUrl}/api/admin/booths/${assignment.boothId}/payment-options/${assignment.paymentMethod}`,
          { withCredentials: true },
        ),
      );
      await this.loadOverview();
      this.message.set('Payment assignment disabled.');
    });
  }

  protected async approveCash(transactionId: string): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(
        this.http.post(
          `${App.apiBaseUrl}/api/cashier/transactions/${transactionId}/approve-cash`,
          {},
          { withCredentials: true },
        ),
      );
      await this.loadOverview();
      this.message.set('Cash approved.');
    });
  }

  protected async cancelTransaction(transactionId: string): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(
        this.http.post(
          `${App.apiBaseUrl}/api/cashier/transactions/${transactionId}/cancel`,
          {},
          { withCredentials: true },
        ),
      );
      await this.loadOverview();
      this.message.set('Transaction cancelled.');
    });
  }

  protected async returnBoothToWelcome(boothId: string): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(
        this.http.post(
          `${App.apiBaseUrl}/api/cashier/booths/${boothId}/return-to-welcome`,
          {},
          { withCredentials: true },
        ),
      );
      await this.loadOverview();
      this.message.set('Booth returned to welcome.');
    });
  }

  protected async createExtraPrintAddOn(parentTransactionId: string): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(
        this.http.post(
          `${App.apiBaseUrl}/api/cashier/transactions/${parentTransactionId}/extra-prints`,
          { copyCount: this.extraPrintCopies() },
          { withCredentials: true },
        ),
      );
      await this.loadOverview();
      this.message.set('Extra print add-on created. Collect cash, then approve.');
    });
  }

  protected async createPlanActivation(boothId: string): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(
        this.http.post(
          `${App.apiBaseUrl}/api/cashier/booths/${boothId}/plan-activation`,
          {},
          { withCredentials: true },
        ),
      );
      await this.loadOverview();
      this.message.set('Package activation created. Collect cash, then approve.');
    });
  }

  protected clientNameFor(clientId: string | null): string {
    return this.overview()?.clients.find((client) => client.id === clientId)?.name ?? 'Platform';
  }

  protected ownerForClient(clientId: string): UserSummary | null {
    return (
      this.overview()?.users.find(
        (user) => user.clientAccountId === clientId && user.role === 'CLIENT_OWNER',
      ) ?? null
    );
  }

  protected latestSubscriptionFor(clientId: string): SubscriptionSummary | null {
    const subscriptions =
      this.overview()?.subscriptions.filter(
        (subscription) => subscription.clientAccountId === clientId,
      ) ?? [];

    return subscriptions.at(-1) ?? null;
  }

  protected planNameFor(planId: string | null): string {
    if (!planId) {
      return 'No subscription';
    }

    return (
      this.overview()?.subscriptionPlans.find((plan) => plan.id === planId)?.name ??
      'Unknown subscription'
    );
  }

  protected planPriceFor(planId: string | null): number {
    if (!planId) {
      return 0;
    }

    return (
      this.overview()?.subscriptionPlans.find((plan) => plan.id === planId)?.pricePerBoothCents ?? 0
    );
  }

  protected subscriptionMonthlyTotalCents(subscription: SubscriptionSummary | null): number {
    if (!subscription) {
      return 0;
    }

    return this.planPriceFor(subscription.subscriptionPlanId) * subscription.activeBoothAllowance;
  }

  protected assignedClientCountForSubscription(subscriptionPlanId: string): number {
    return new Set(
      (this.overview()?.subscriptions ?? [])
        .filter((subscription) => subscription.subscriptionPlanId === subscriptionPlanId)
        .map((subscription) => subscription.clientAccountId),
    ).size;
  }

  protected assignedAllowanceForSubscription(subscriptionPlanId: string): number {
    return (this.overview()?.subscriptions ?? [])
      .filter((subscription) => subscription.subscriptionPlanId === subscriptionPlanId)
      .reduce((total, subscription) => total + subscription.activeBoothAllowance, 0);
  }

  protected subscriptionDefinitionMrrCents(subscriptionPlanId: string): number {
    return (this.overview()?.subscriptions ?? [])
      .filter(
        (subscription) =>
          subscription.subscriptionPlanId === subscriptionPlanId &&
          ['TRIAL', 'ACTIVE', 'PAST_DUE'].includes(subscription.status),
      )
      .reduce(
        (total, subscription) =>
          total +
          this.planPriceFor(subscription.subscriptionPlanId) * subscription.activeBoothAllowance,
        0,
      );
  }

  protected activeBoothCountForClient(clientId: string): number {
    return (
      this.overview()?.booths.filter(
        (booth) => booth.clientAccountId === clientId && booth.status === 'ACTIVE',
      ).length ?? 0
    );
  }

  protected boothCountForLocation(locationId: string): number {
    return this.overview()?.booths.filter((booth) => booth.locationId === locationId).length ?? 0;
  }

  protected locationSalesCents(locationId: string): number {
    return (
      this.overview()?.reports.locationSales.find((location) => location.locationId === locationId)
        ?.grossSalesCents ?? 0
    );
  }

  protected offerSessionCount(offerId: string): number {
    return (
      this.overview()?.reports.offerSales.find((offer) => offer.offerId === offerId)
        ?.completedSessions ?? 0
    );
  }

  protected activeBoothCountUsingOffers(): number {
    const activatedBoothIds = new Set(
      (this.overview()?.activations ?? [])
        .filter((activation) => activation.status === 'ACTIVE')
        .map((activation) => activation.boothId),
    );
    return activatedBoothIds.size;
  }

  protected viewClient(client: ClientSummary): void {
    this.selectedClientDetailId.set(client.id);
    this.activeView.set('client-detail');
  }

  protected viewUser(user: UserSummary): void {
    this.syncUserDetail(user);
    this.activeView.set('user-detail');
  }

  protected viewSubscription(subscription: SubscriptionPlanSummary): void {
    this.selectedSubscriptionDetailId.set(subscription.id);
    this.planName.set(subscription.name);
    this.planPrice.set(subscription.pricePerBoothCents);
    this.subscriptionActive.set(subscription.active);
    this.activeView.set('subscription-detail');
  }

  protected viewBooth(booth: BoothSummary): void {
    this.syncBoothDetail(booth.id);
    this.boothDetailTab.set('details');
    this.activeView.set('booth-detail');
  }

  protected syncBoothDetail(boothId: string): void {
    const booth = this.overview()?.booths.find((item) => item.id === boothId);

    if (!booth) {
      return;
    }

    const appearance =
      this.overview()?.appearanceConfigs?.find((item) => item.boothId === booth.id) ?? null;
    const selectedOffer = this.selectedOfferFor(booth.id);
    const assignedCashier = this.assignedCashierFor(booth.id);

    this.selectedBoothDetailId.set(booth.id);
    this.boothDetailName.set(booth.name);
    this.boothDetailCode.set(booth.code);
    this.boothDetailLocationId.set(booth.locationId);
    this.boothDetailCashierUserId.set(assignedCashier?.id ?? null);
    this.boothDetailStatus.set(booth.status);
    this.boothDetailOfferId.set(selectedOffer?.id ?? this.activeOffers()[0]?.id ?? null);
    this.boothAppearanceSessionLabel.set(appearance?.sessionLabel || 'Self photo booth');
    this.boothAppearanceHeadline.set(
      appearance?.defaultWelcomeHeadline || 'Step Into The Memory Box',
    );
    this.boothAppearanceSubtitle.set(
      appearance?.defaultWelcomeSubtitle ||
        "Review today's booth offer, pay at the counter, then strike your best pose.",
    );
    this.boothAppearanceThemePreset.set(this.normalizeBoothThemePreset(appearance?.themePreset));
    this.boothAppearanceBackgroundImageDataUrl.set(appearance?.backgroundImageDataUrl ?? '');
  }

  protected setBoothDetailTab(tab: BoothDetailTab): void {
    this.boothDetailTab.set(tab);
  }

  protected setBoothBackgroundImageFromFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];

    if (!file) {
      return;
    }

    if (!['image/png', 'image/jpeg', 'image/webp'].includes(file.type)) {
      this.error.set('Upload a PNG, JPG, or WebP image for the booth background.');
      input.value = '';
      return;
    }

    if (file.size > 2 * 1024 * 1024) {
      this.error.set('Background image must be 2 MB or smaller.');
      input.value = '';
      return;
    }

    const reader = new FileReader();
    reader.onload = () => {
      this.boothAppearanceBackgroundImageDataUrl.set(String(reader.result ?? ''));
    };
    reader.onerror = () => this.error.set('Unable to read the background image.');
    reader.readAsDataURL(file);
  }

  protected clearBoothBackgroundImage(): void {
    this.boothAppearanceBackgroundImageDataUrl.set('');
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

  private themeSchemeFor(value: string): { readonly primaryColor: string; readonly accentColor: string } {
    switch (this.normalizeBoothThemePreset(value)) {
      case 'POP':
        return { primaryColor: '#0bbbe6', accentColor: '#ff0090' };
      case 'CLEAN_MODERN':
        return { primaryColor: '#111827', accentColor: '#2563eb' };
      default:
        return { primaryColor: '#4f2d1d', accentColor: '#f5d27e' };
    }
  }

  protected locationNameFor(locationId: string): string {
    return (
      this.overview()?.locations.find((location) => location.id === locationId)?.name ??
      'Unassigned'
    );
  }

  protected boothNameFor(boothId: string | null): string {
    return this.overview()?.booths.find((booth) => booth.id === boothId)?.name ?? 'Unassigned';
  }

  protected assignedCashierFor(boothId: string): UserSummary | null {
    return this.cashiers().find((cashier) => cashier.assignedBoothId === boothId) ?? null;
  }

  protected activeOfferFor(boothId: string): OfferSummary | null {
    const activation = this.overview()?.activations.find(
      (item) => item.boothId === boothId && item.status === 'ACTIVE',
    );
    return this.overview()?.offers.find((offer) => offer.id === activation?.boothOfferId) ?? null;
  }

  protected pendingActivationFor(boothId: string): OfferActivationSummary | null {
    return (
      this.overview()?.activations.find(
        (item) => item.boothId === boothId && item.status === 'PENDING_PAYMENT',
      ) ?? null
    );
  }

  protected selectedActivationFor(boothId: string): OfferActivationSummary | null {
    return (
      this.pendingActivationFor(boothId) ??
      this.overview()?.activations.find(
        (item) => item.boothId === boothId && item.status === 'ACTIVE',
      ) ??
      null
    );
  }

  protected boothPackageStatusFor(booth: BoothSummary): BoothPackageStatusDisplay | null {
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

  protected offerForActivation(activation: OfferActivationSummary | null): OfferSummary | null {
    return this.overview()?.offers.find((offer) => offer.id === activation?.boothOfferId) ?? null;
  }

  protected selectedOfferFor(boothId: string): OfferSummary | null {
    return this.offerForActivation(this.selectedActivationFor(boothId));
  }

  protected packageStatusLabelFor(boothId: string): string {
    const activation = this.selectedActivationFor(boothId);
    const offer = this.offerForActivation(activation);

    if (!activation || !offer) {
      return 'None';
    }

    return activation.status === 'PENDING_PAYMENT'
      ? `${offer.name} (awaiting activation)`
      : offer.name;
  }

  protected activationDetailFor(activation: OfferActivationSummary, offer: OfferSummary): string {
    if (offer.offerType === 'TIME_UNLIMITED') {
      return `${offer.durationHours ?? 0} hour timed package`;
    }

    if (offer.offerType === 'SESSION_COUNT') {
      return `${activation.sessionsUsed}/${activation.sessionAllowance ?? offer.sessionAllowance ?? 0} sessions used`;
    }

    return offer.includedPrintEntitlement;
  }

  protected cashAssignmentFor(boothId: string): PaymentAssignmentSummary | null {
    return (
      this.overview()?.paymentAssignments.find(
        (assignment) => assignment.boothId === boothId && assignment.paymentMethod === 'CASH',
      ) ?? null
    );
  }

  protected paymentLabelFor(boothId: string): string {
    const assignment = this.cashAssignmentFor(boothId);

    if (!assignment) {
      return 'None';
    }

    return assignment.runtimeEnabled ? 'Cash' : 'Cash disabled';
  }

  protected formatMoney(cents: number): string {
    return `PHP ${(cents / 100).toLocaleString('en-PH', { maximumFractionDigits: 0 })}`;
  }

  protected transactionActivityFor(transaction: TransactionSummary): TransactionActivityDisplay {
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
          detail: `${boothName} / ${offerName} / ${offerType}`,
          auditText,
          value: this.formatMoney(transaction.amountCents),
        };
      case 'COVERED_PLAN_SESSION':
        return {
          title: 'Covered session',
          detail: `${boothName} / ${offerName} / ${this.coveredSessionDetail(transaction)}`,
          auditText,
          value: 'Included',
        };
      case 'EXTRA_PRINT_ADD_ON':
        return {
          title: 'Extra print add-on',
          detail: `${boothName} / ${transaction.extraPrintCount} ${
            transaction.extraPrintCount === 1 ? 'copy' : 'copies'
          }${entitlement}`,
          auditText,
          value: this.formatMoney(transaction.amountCents),
        };
      case 'SESSION_PURCHASE':
      default:
        return {
          title: 'Per-session sale',
          detail: `${boothName} / ${offerName}${entitlement}`,
          auditText,
          value: this.formatMoney(transaction.amountCents),
        };
    }
  }

  protected isTerminalTransaction(status: string): boolean {
    return status === 'COMPLETED' || status === 'EXPIRED' || status === 'CANCELLED';
  }

  protected copyOptions(): readonly number[] {
    return [1, 2, 3, 4, 5];
  }

  protected roleLabel(role = this.session()?.role ?? ''): string {
    return role
      .split('_')
      .map((part) => part.charAt(0) + part.slice(1).toLowerCase())
      .join(' ');
  }

  protected activeViewTitle(): string {
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
      case 'audit':
        return 'Audit Log';
      default:
        return 'Admin Web';
    }
  }

  protected canAccessView(view: ViewKey): boolean {
    const role = this.session()?.role;

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
        view === 'reports' ||
        view === 'settings' ||
        view === 'audit'
      );
    }

    return view === 'dashboard';
  }

  protected formatDate(value: string): string {
    return new Intl.DateTimeFormat('en-PH', {
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    }).format(new Date(value));
  }

  protected formatPhilippinesDateTime(value: string): string {
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

      return sessionAllowance
        ? `${sessionAllowance} session package`
        : 'Session count package';
    }

    if (transaction.offerType === 'TIME_UNLIMITED') {
      return 'Unlimited package';
    }

    return this.readableType(transaction.offerType ?? transaction.transactionType);
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
      this.overview()?.transactions
        .filter(
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

  private timedPackageStatusFor(
    activation: OfferActivationSummary,
    offer: OfferSummary,
  ): string {
    if (!activation.endsAt) {
      return `${offer.durationHours ?? 0} hour timed package`;
    }

    const endsAt = new Date(activation.endsAt);
    const remainingMinutes = Math.max(
      0,
      Math.ceil((endsAt.getTime() - Date.now()) / 60_000),
    );
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
      canApproveCash: role === 'CASHIER' && permissions.approveCash,
      canReturnBoothToWelcome: role === 'CASHIER' && permissions.returnBoothToWelcome,
      canCancelTransaction: role === 'CASHIER' && permissions.cancelTransaction,
    };
  }

  private async createUser(
    role: string,
    assignedBoothId: string | null,
    name: string,
    email: string,
    password: string,
    successMessage: string,
    permissions: Record<CashierPermissionKey, boolean> = {
      approveCash: true,
      returnBoothToWelcome: true,
      cancelTransaction: true,
    },
  ): Promise<void> {
    if (!this.selectedClientId()) {
      this.error.set('Create a client first.');
      return;
    }

    await this.run(async () => {
      await firstValueFrom(
        this.http.post(
          `${App.apiBaseUrl}/api/admin/users`,
          {
            clientAccountId: this.selectedClientId(),
            assignedBoothId,
            name,
            email,
            password,
            role,
            ...this.cashierPermissionPayload(role, permissions),
          },
          { withCredentials: true },
        ),
      );
      await this.loadOverview();
      this.message.set(successMessage);
    });
  }

  private async restoreSession(): Promise<void> {
    try {
      const session = await firstValueFrom(
        this.http.get<Session>(`${App.apiBaseUrl}/api/auth/session`, { withCredentials: true }),
      );
      this.session.set(session);
      await this.loadOverview();
    } catch {
      this.session.set(null);
    }
  }

  private async loadOverview(): Promise<void> {
    const overview = await firstValueFrom(
      this.http.get<Overview>(`${App.apiBaseUrl}/api/admin/overview`, { withCredentials: true }),
    );
    this.overview.set(overview);
    this.session.set(overview.session);

    if (!this.canAccessView(this.activeView())) {
      this.activeView.set('dashboard');
    }
  }

  private async run(operation: () => Promise<void>): Promise<void> {
    this.loading.set(true);
    this.error.set('');

    try {
      await operation();
    } catch (error) {
      this.error.set(error instanceof Error ? error.message : 'Request failed.');
    } finally {
      this.loading.set(false);
    }
  }
}

