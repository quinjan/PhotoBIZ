import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterOutlet } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { firstValueFrom, interval } from 'rxjs';

type Session = {
  readonly userId: string;
  readonly name: string;
  readonly email: string;
  readonly role: string;
  readonly clientAccountId: string | null;
  readonly assignedBoothId: string | null;
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
type PaymentAssignmentSummary = {
  readonly id: string;
  readonly boothId: string;
  readonly paymentMethod: string;
  readonly runtimeEnabled: boolean;
  readonly status: string;
};
type TransactionSummary = {
  readonly id: string;
  readonly boothId: string;
  readonly transactionNumber: string;
  readonly transactionType: string;
  readonly status: string;
  readonly paymentMethod: string;
  readonly amountCents: number;
  readonly parentTransactionId: string | null;
  readonly extraPrintCount: number;
  readonly canCreateExtraPrintAddOn: boolean;
  readonly extraPrintUnitPriceCents: number | null;
  readonly createdAt: string;
  readonly paidAt: string | null;
  readonly completedAt: string | null;
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

type Overview = {
  readonly session: Session;
  readonly clients: readonly ClientSummary[];
  readonly subscriptionPlans: readonly SubscriptionPlanSummary[];
  readonly subscriptions: readonly SubscriptionSummary[];
  readonly users: readonly UserSummary[];
  readonly locations: readonly LocationSummary[];
  readonly booths: readonly BoothSummary[];
  readonly offers: readonly OfferSummary[];
  readonly activations: readonly {
    id: string;
    boothId: string;
    boothOfferId: string;
    status: string;
  }[];
  readonly paymentAssignments: readonly PaymentAssignmentSummary[];
  readonly transactions: readonly TransactionSummary[];
  readonly reports: ReportSummary;
  readonly auditLogs: readonly AuditLogSummary[];
};

type BoothSecret = {
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
  | 'setup'
  | 'booths'
  | 'pos'
  | 'reports'
  | 'audit';

@Component({
  selector: 'app-root',
  imports: [CommonModule, FormsModule, RouterOutlet],
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

  protected readonly loginEmail = signal('owner@photobiz.local');
  protected readonly loginPassword = signal('PhotoBIZ!123');
  protected readonly clientName = signal('The Memory Box');
  protected readonly planName = signal('Starter Booth');
  protected readonly planPrice = signal(150000);
  protected readonly subscriptionActive = signal(true);
  protected readonly subscriptionAllowance = signal(2);
  protected readonly locationName = signal('SM Manila');
  protected readonly boothName = signal('Booth A');
  protected readonly boothCode = signal('SMA-001');
  protected readonly ownerName = signal('Client Owner');
  protected readonly ownerEmail = signal('owner@memorybox.local');
  protected readonly ownerPassword = signal('PhotoBIZ!123');
  protected readonly cashierName = signal('Cashier');
  protected readonly cashierEmail = signal('cashier@memorybox.local');
  protected readonly cashierPassword = signal('PhotoBIZ!123');
  protected readonly offerName = signal('Per Session');
  protected readonly offerPrice = signal(25000);
  protected readonly offerMode = signal('SESSION_STANDARD');
  protected readonly extraPrintCopies = signal(1);
  protected readonly clientSearch = signal('');
  protected readonly clientModalOpen = signal(false);
  protected readonly selectedClientDetailId = signal<string | null>(null);
  protected readonly subscriptionModalOpen = signal(false);
  protected readonly selectedSubscriptionDetailId = signal<string | null>(null);
  protected readonly subscriptionClientId = signal<string | null>(null);
  protected readonly subscriptionPlanId = signal<string | null>(null);
  protected readonly subscriptionStatus = signal('ACTIVE');

  protected readonly selectedClientId = computed(() => this.overview()?.clients[0]?.id ?? null);
  protected readonly selectedPlanId = computed(
    () => this.overview()?.subscriptionPlans[0]?.id ?? null,
  );
  protected readonly selectedLocationId = computed(() => this.overview()?.locations[0]?.id ?? null);
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
  protected readonly extraPrintCandidate = computed(() => {
    const boothId = this.assignedBooth()?.id;
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
    { key: 'setup', label: 'Setup' },
    { key: 'clients', label: 'Clients' },
    { key: 'booths', label: 'Booths' },
    { key: 'pos', label: 'Cashier POS' },
    { key: 'reports', label: 'Reports' },
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

  protected async createCashier(): Promise<void> {
    if (!this.selectedBoothId()) {
      this.error.set('Create a booth before assigning a cashier.');
      return;
    }

    await this.createUser(
      'CASHIER',
      this.selectedBoothId(),
      this.cashierName(),
      this.cashierEmail(),
      this.cashierPassword(),
      'Cashier created.',
    );
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
          },
          { withCredentials: true },
        ),
      );
      await this.loadOverview();
      this.message.set('User status updated.');
    });
  }

  protected async createLocation(): Promise<void> {
    if (!this.selectedClientId()) {
      this.error.set('Create a client first.');
      return;
    }

    await this.run(async () => {
      await firstValueFrom(
        this.http.post(
          `${App.apiBaseUrl}/api/admin/locations`,
          {
            clientAccountId: this.selectedClientId(),
            name: this.locationName(),
            address: 'Customer-facing mall location',
          },
          { withCredentials: true },
        ),
      );
      await this.loadOverview();
      this.message.set('Location created.');
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

  protected async createBooth(): Promise<void> {
    if (!this.selectedClientId() || !this.selectedLocationId()) {
      this.error.set('Create a client and location first.');
      return;
    }

    await this.run(async () => {
      const response = await firstValueFrom(
        this.http.post<{ booth: BoothSummary; kioskToken: string; agentCredential: string }>(
          `${App.apiBaseUrl}/api/admin/booths`,
          {
            clientAccountId: this.selectedClientId(),
            locationId: this.selectedLocationId(),
            name: this.boothName(),
            code: this.boothCode(),
          },
          { withCredentials: true },
        ),
      );

      this.boothSecret.set({
        boothName: response.booth.name,
        boothCode: response.booth.code,
        kioskToken: response.kioskToken,
        agentCredential: response.agentCredential,
      });
      await this.loadOverview();
      this.message.set('Booth created and credentials issued.');
    });
  }

  protected async updateBoothStatus(booth: BoothSummary, status: string): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(
        this.http.put(
          `${App.apiBaseUrl}/api/admin/booths/${booth.id}`,
          { locationId: booth.locationId, name: booth.name, code: booth.code, status },
          { withCredentials: true },
        ),
      );
      await this.loadOverview();
      this.message.set('Booth status updated.');
    });
  }

  protected async createOffer(): Promise<void> {
    if (!this.selectedClientId()) {
      this.error.set('Create a client first.');
      return;
    }

    await this.run(async () => {
      await firstValueFrom(
        this.http.post(
          `${App.apiBaseUrl}/api/admin/offers`,
          {
            clientAccountId: this.selectedClientId(),
            name: this.offerName(),
            description: 'Standard booth session',
            offerType: 'PER_SESSION',
            priceCents: this.offerPrice(),
            currency: 'PHP',
            includedPrintEntitlement: '2 pcs 6x2 or 1 pc 6x4',
            durationHours: null,
            sessionAllowance: null,
            allowsExtraPrintAddOn: true,
            extraPrintPriceCents: 5000,
            lumaboothSessionMode: this.offerMode(),
          },
          { withCredentials: true },
        ),
      );
      await this.loadOverview();
      this.message.set('Offer created.');
    });
  }

  protected async updateOfferActive(offer: OfferSummary, active: boolean): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(
        this.http.put(
          `${App.apiBaseUrl}/api/admin/offers/${offer.id}`,
          { ...offer, active },
          { withCredentials: true },
        ),
      );
      await this.loadOverview();
      this.message.set(active ? 'Offer reactivated.' : 'Offer deactivated.');
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

  protected viewClient(client: ClientSummary): void {
    this.selectedClientDetailId.set(client.id);
    this.activeView.set('client-detail');
  }

  protected viewSubscription(subscription: SubscriptionPlanSummary): void {
    this.selectedSubscriptionDetailId.set(subscription.id);
    this.planName.set(subscription.name);
    this.planPrice.set(subscription.pricePerBoothCents);
    this.subscriptionActive.set(subscription.active);
    this.activeView.set('subscription-detail');
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

  protected activeOfferFor(boothId: string): OfferSummary | null {
    const activation = this.overview()?.activations.find(
      (item) => item.boothId === boothId && item.status === 'ACTIVE',
    );
    return this.overview()?.offers.find((offer) => offer.id === activation?.boothOfferId) ?? null;
  }

  protected cashAssignmentFor(boothId: string): PaymentAssignmentSummary | null {
    return (
      this.overview()?.paymentAssignments.find(
        (assignment) => assignment.boothId === boothId && assignment.paymentMethod === 'CASH',
      ) ?? null
    );
  }

  protected formatMoney(cents: number): string {
    return `PHP ${(cents / 100).toLocaleString('en-PH', { maximumFractionDigits: 0 })}`;
  }

  protected isTerminalTransaction(status: string): boolean {
    return status === 'COMPLETED' || status === 'EXPIRED' || status === 'CANCELLED';
  }

  protected copyOptions(): readonly number[] {
    return [1, 2, 3, 4, 5];
  }

  protected activeViewTitle(): string {
    switch (this.activeView()) {
      case 'dashboard':
        return this.isApplicationOwner() ? 'PhotoBIZ Platform Dashboard' : 'Admin Web';
      case 'subscriptions':
        return 'Subscriptions';
      case 'subscription-detail':
        return this.selectedSubscriptionDefinition() ? 'Subscription Detail' : 'Add Subscription';
      case 'clients':
        return 'Client Accounts';
      case 'client-detail':
        return 'Client Account Detail';
      case 'pos':
        return 'Cashier POS';
      case 'reports':
        return 'Reports';
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

    return true;
  }

  protected formatDate(value: string): string {
    return new Intl.DateTimeFormat('en-PH', {
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    }).format(new Date(value));
  }

  private async createUser(
    role: string,
    assignedBoothId: string | null,
    name: string,
    email: string,
    password: string,
    successMessage: string,
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
