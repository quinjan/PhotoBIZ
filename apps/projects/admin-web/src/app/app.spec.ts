import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { vi } from 'vitest';
import { App } from './app';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideHttpClient()],
    }).compileComponents();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;

    expect(app).toBeTruthy();
  });

  it('should paginate grid collections independently', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance as unknown as {
      pagedItems: <T>(key: string, items: readonly T[]) => readonly T[];
      pageInfo: (
        key: string,
        totalItems: number,
      ) => {
        page: number;
        totalPages: number;
        start: number;
        end: number;
        total: number;
        hasPrevious: boolean;
        hasNext: boolean;
      };
      nextPage: (key: string, totalItems: number) => void;
      previousPage: (key: string, totalItems: number) => void;
    };
    const items = [1, 2, 3, 4, 5, 6];

    expect(app.pagedItems('dashboard-transactions', items)).toEqual([1, 2, 3, 4, 5]);
    expect(app.pageInfo('dashboard-transactions', items.length)).toEqual({
      page: 1,
      totalPages: 2,
      start: 1,
      end: 5,
      total: 6,
      hasPrevious: false,
      hasNext: true,
    });

    app.nextPage('dashboard-transactions', items.length);

    expect(app.pagedItems('dashboard-transactions', items)).toEqual([6]);
    expect(app.pageInfo('dashboard-transactions', items.length).page).toBe(2);
    expect(app.pageInfo('dashboard-booths', items.length).page).toBe(1);

    app.previousPage('dashboard-transactions', items.length);

    expect(app.pageInfo('dashboard-transactions', items.length).page).toBe(1);
  });

  it('should render the sign in screen', async () => {
    const fixture = TestBed.createComponent(App);

    await fixture.whenStable();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('PhotoBIZ');
    expect(compiled.querySelector('h1')?.textContent).toContain('Sign in');
  });

  it('should limit application owner navigation to platform management views', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();

    const app = fixture.componentInstance as unknown as {
      session: { set: (value: unknown) => void };
      overview: { set: (value: unknown) => void };
      setView: (value: string) => void;
      viewBooth: (value: unknown) => void;
      setBoothDetailTab: (value: string) => void;
    };
    const session = {
      userId: 'owner-id',
      name: 'Platform Owner',
      email: 'platform@photobiz.local',
      role: 'APPLICATION_OWNER',
      clientAccountId: null,
      assignedBoothId: null,
    };

    app.session.set(session);
    app.overview.set({
      session,
      clients: [],
      subscriptionPlans: [],
      subscriptions: [],
      users: [],
      locations: [],
      booths: [],
      offers: [],
      activations: [],
      paymentAssignments: [],
      transactions: [],
      reports: emptyReports(),
      auditLogs: [],
    });
    fixture.detectChanges();
    await fixture.whenStable();

    const compiled = fixture.nativeElement as HTMLElement;

    const navLabels = Array.from(compiled.querySelectorAll('.nav-item')).map((item) =>
      item.textContent?.trim(),
    );

    expect(navLabels).toEqual(['-Dashboard', '-Subscriptions', '-Clients', '-Audit Log']);
  });

  it('should expose client owner operations navigation', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();

    const app = fixture.componentInstance as unknown as {
      session: { set: (value: unknown) => void };
      overview: { set: (value: unknown) => void };
      setView: (value: string) => void;
      viewBooth: (value: unknown) => void;
      setBoothDetailTab: (value: string) => void;
    };
    const session = {
      userId: 'client-owner-id',
      name: 'Client Owner',
      email: 'owner@memorybox.local',
      role: 'CLIENT_OWNER',
      clientAccountId: 'client-id',
      assignedBoothId: null,
    };

    app.session.set(session);
    app.overview.set({
      session,
      clients: [{ id: 'client-id', name: 'The Memory Box', status: 'ACTIVE' }],
      subscriptionPlans: [],
      subscriptions: [],
      users: [],
      locations: [],
      booths: [],
      offers: [],
      activations: [],
      paymentAssignments: [],
      transactions: [],
      reports: emptyReports(),
      auditLogs: [],
    });
    fixture.detectChanges();
    await fixture.whenStable();

    const compiled = fixture.nativeElement as HTMLElement;
    const navLabels = Array.from(compiled.querySelectorAll('.nav-item')).map((item) =>
      item.textContent?.trim(),
    );

    expect(navLabels).toEqual([
      '-Dashboard',
      '-Users',
      '-Locations',
      '-Booths',
      '-Packages',
      '-Transactions',
      '-Reports',
      '-Settings',
      '-Audit Log',
    ]);
    expect(compiled.textContent).toContain('The Memory Box / Client Owner');
  });

  it('should show add user action on the users view for client owners', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();

    const app = fixture.componentInstance as unknown as {
      session: { set: (value: unknown) => void };
      overview: { set: (value: unknown) => void };
      setView: (value: string) => void;
    };
    const session = {
      userId: 'client-owner-id',
      name: 'Client Owner',
      email: 'owner@memorybox.local',
      role: 'CLIENT_OWNER',
      clientAccountId: 'client-id',
      assignedBoothId: null,
    };

    app.session.set(session);
    app.overview.set({
      session,
      clients: [{ id: 'client-id', name: 'The Memory Box', status: 'ACTIVE' }],
      subscriptionPlans: [],
      subscriptions: [],
      users: [],
      locations: [],
      booths: [],
      offers: [],
      activations: [],
      paymentAssignments: [],
      transactions: [],
      reports: emptyReports(),
      auditLogs: [],
    });
    app.setView('users');
    fixture.detectChanges();
    await fixture.whenStable();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('+ Add User');
    expect(compiled.textContent).not.toContain('+ Add Cashier');
  });

  it('should manage users from a detail view instead of inline status actions', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();

    const app = fixture.componentInstance as unknown as {
      session: { set: (value: unknown) => void };
      overview: { set: (value: unknown) => void };
      setView: (value: string) => void;
      viewUser: (value: unknown) => void;
    };
    const session = {
      userId: 'client-owner-id',
      name: 'Client Owner',
      email: 'owner@memorybox.local',
      role: 'CLIENT_OWNER',
      clientAccountId: 'client-id',
      assignedBoothId: null,
    };
    const cashier = {
      id: 'cashier-id',
      clientAccountId: 'client-id',
      name: 'Cashier One',
      email: 'cashier@memorybox.local',
      role: 'CASHIER',
      status: 'ACTIVE',
      assignedBoothId: 'booth-id',
      canApproveCash: true,
      canReturnBoothToWelcome: false,
      canCancelTransaction: true,
    };

    app.session.set(session);
    app.overview.set({
      session,
      clients: [{ id: 'client-id', name: 'The Memory Box', status: 'ACTIVE' }],
      subscriptionPlans: [],
      subscriptions: [],
      users: [cashier],
      locations: [],
      booths: [
        {
          id: 'booth-id',
          clientAccountId: 'client-id',
          locationId: 'location-id',
          name: 'Booth A',
          code: 'SMA-001',
          status: 'ACTIVE',
          currentState: 'OFFLINE',
          lastHeartbeatAt: null,
        },
      ],
      offers: [],
      activations: [],
      paymentAssignments: [],
      transactions: [],
      reports: emptyReports(),
      auditLogs: [],
    });
    app.setView('users');
    fixture.detectChanges();
    await fixture.whenStable();

    let compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Manage');
    expect(compiled.textContent).not.toContain('Deactivate');

    app.viewUser(cashier);
    fixture.detectChanges();
    await fixture.whenStable();

    compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('User Details');
    expect(compiled.textContent).toContain('Deactivate');
    expect(compiled.textContent).toContain('Approve cash');
    expect(compiled.querySelectorAll('[role="switch"]').length).toBe(3);
    expect(compiled.querySelectorAll('[role="switch"]')[1].getAttribute('aria-checked')).toBe(
      'false',
    );
  });

  it('should manage locations through add and manage modals', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();

    const app = fixture.componentInstance as unknown as {
      session: { set: (value: unknown) => void };
      overview: { set: (value: unknown) => void };
      setView: (value: string) => void;
    };
    const session = {
      userId: 'client-owner-id',
      name: 'Client Owner',
      email: 'owner@memorybox.local',
      role: 'CLIENT_OWNER',
      clientAccountId: 'client-id',
      assignedBoothId: null,
    };

    app.session.set(session);
    app.overview.set({
      session,
      clients: [{ id: 'client-id', name: 'The Memory Box', status: 'ACTIVE' }],
      subscriptionPlans: [],
      subscriptions: [],
      users: [],
      locations: [
        {
          id: 'location-id',
          clientAccountId: 'client-id',
          name: 'SM Manila',
          address: null,
          status: 'ACTIVE',
        },
      ],
      booths: [],
      offers: [],
      activations: [],
      paymentAssignments: [],
      transactions: [],
      reports: emptyReports(),
      auditLogs: [],
    });
    app.setView('locations');
    fixture.detectChanges();
    await fixture.whenStable();

    let compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('+ Add Location');
    expect(compiled.textContent).toContain('Manage');
    expect(compiled.textContent).not.toContain('+ Register Booth');
    expect(compiled.textContent).not.toContain('Quick Add');

    const addLocationButton = Array.from(compiled.querySelectorAll('button')).find(
      (button) => button.textContent?.trim() === '+ Add Location',
    ) as HTMLButtonElement;
    addLocationButton.click();
    fixture.detectChanges();
    await fixture.whenStable();

    compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Add Location');
    expect(compiled.textContent).toContain('LOCATION NAME');

    const closeButton = Array.from(compiled.querySelectorAll('button')).find(
      (button) => button.textContent?.trim() === 'Close',
    ) as HTMLButtonElement;
    closeButton.click();
    fixture.detectChanges();
    await fixture.whenStable();

    compiled = fixture.nativeElement as HTMLElement;
    const manageButton = Array.from(compiled.querySelectorAll('button')).find(
      (button) => button.textContent?.trim() === 'Manage',
    ) as HTMLButtonElement;
    manageButton.click();
    fixture.detectChanges();
    await fixture.whenStable();

    compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Manage Location');
    expect(compiled.textContent).toContain('Delete Location');
    expect((compiled.querySelector('input[name="locationDetailName"]') as HTMLInputElement).value).toBe(
      'SM Manila',
    );
  });

  it('should render booth offline state from the overview API', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();

    const app = fixture.componentInstance as unknown as {
      session: { set: (value: unknown) => void };
      overview: { set: (value: unknown) => void };
      setView: (value: string) => void;
      viewBooth: (value: unknown) => void;
      setBoothDetailTab: (value: string) => void;
    };
    const session = {
      userId: 'user-id',
      name: 'Client Owner',
      email: 'owner@memorybox.local',
      role: 'CLIENT_OWNER',
      clientAccountId: 'client-id',
      assignedBoothId: null,
    };

    app.session.set(session);
    app.overview.set({
      session,
      clients: [{ id: 'client-id', name: 'The Memory Box', status: 'ACTIVE' }],
      subscriptionPlans: [],
      subscriptions: [],
      users: [
        {
          id: 'cashier-id',
          clientAccountId: 'client-id',
          name: 'Cashier',
          email: 'cashier@memorybox.local',
          role: 'CASHIER',
          status: 'ACTIVE',
          assignedBoothId: 'booth-id',
          canApproveCash: true,
          canReturnBoothToWelcome: true,
          canCancelTransaction: true,
        },
      ],
      locations: [
        {
          id: 'location-id',
          clientAccountId: 'client-id',
          name: 'SM Manila',
          address: null,
          status: 'ACTIVE',
        },
      ],
      booths: [
        {
          id: 'booth-id',
          clientAccountId: 'client-id',
          locationId: 'location-id',
          name: 'Booth A',
          code: 'SMA-001',
          status: 'ACTIVE',
          currentState: 'OFFLINE',
          lastHeartbeatAt: null,
        },
      ],
      offers: [
        {
          id: 'offer-id',
          clientAccountId: 'client-id',
          name: 'Per Session',
          description: 'Standard booth session',
          offerType: 'PER_SESSION',
          priceCents: 25000,
          currency: 'PHP',
          includedPrintEntitlement: '2 pcs 6x2',
          durationHours: null,
          sessionAllowance: null,
          allowsExtraPrintAddOn: true,
          extraPrintPriceCents: 5000,
          lumaboothSessionMode: 'PRINT',
          active: true,
        },
      ],
      printEntitlements: [],
      activations: [
        {
          id: 'activation-id',
          boothId: 'booth-id',
          boothOfferId: 'offer-id',
          status: 'ACTIVE',
          startsAt: null,
          endsAt: null,
          sessionAllowance: null,
          sessionsUsed: 0,
        },
      ],
      paymentAssignments: [
        {
          id: 'cash-id',
          boothId: 'booth-id',
          paymentMethod: 'CASH',
          runtimeEnabled: true,
          status: 'ASSIGNED',
        },
      ],
      appearanceConfigs: [
        {
          id: 'appearance-id',
          boothId: 'booth-id',
          themePreset: 'VINTAGE_FILM',
          primaryColor: '#2f6868',
          accentColor: '#f5d27e',
          backgroundImageUrl: null,
          backgroundImageDataUrl: null,
          sessionLabel: 'Vintage Pop-Up',
          defaultWelcomeHeadline: 'Step Into The Memory Box',
          defaultWelcomeSubtitle: 'Pay at the counter, then strike your best pose.',
        },
      ],
      transactions: [],
      reports: emptyReports(),
      auditLogs: [],
    });
    app.setView('booths');
    fixture.detectChanges();
    await fixture.whenStable();

    let compiled = fixture.nativeElement as HTMLElement;

    expect(compiled.textContent).toContain('Booth A');
    expect(compiled.textContent).toContain('Agent State');
    expect(compiled.textContent).toContain('Per Session');
    expect(compiled.textContent).toContain('Cash');
    expect(compiled.textContent).toContain('Manage');
    expect(compiled.textContent).toContain('OFFLINE');

    app.viewBooth({
      id: 'booth-id',
      clientAccountId: 'client-id',
      locationId: 'location-id',
      name: 'Booth A',
      code: 'SMA-001',
      status: 'ACTIVE',
      currentState: 'OFFLINE',
      lastHeartbeatAt: null,
    });
    app.setBoothDetailTab('session');
    fixture.detectChanges();
    await fixture.whenStable();

    compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Preview Booth');
    expect(compiled.textContent).toContain('Step Into The Memory Box');
  });

  it('should expose the cashier POS view', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();

    const app = fixture.componentInstance as unknown as {
      session: { set: (value: unknown) => void };
      overview: { set: (value: unknown) => void };
      setView: (value: string) => void;
    };
    const session = {
      userId: 'user-id',
      name: 'Cashier',
      email: 'cashier@memorybox.local',
      role: 'CASHIER',
      clientAccountId: 'client-id',
      assignedBoothId: 'booth-id',
    };

    app.session.set(session);
    app.overview.set({
      session,
      clients: [],
      subscriptionPlans: [],
      subscriptions: [],
      users: [],
      locations: [],
      booths: [
        {
          id: 'booth-id',
          clientAccountId: 'client-id',
          locationId: 'location-id',
          name: 'Booth A',
          code: 'SMA-001',
          status: 'ACTIVE',
          currentState: 'PAYMENT_PENDING',
          lastHeartbeatAt: null,
        },
      ],
      offers: [
        {
          id: 'offer-id',
          clientAccountId: 'client-id',
          name: 'Event Package',
          description: 'Five included sessions',
          offerType: 'SESSION_COUNT',
          priceCents: 150000,
          currency: 'PHP',
          includedPrintEntitlement: '2 pcs 6x2',
          durationHours: null,
          sessionAllowance: 5,
          allowsExtraPrintAddOn: false,
          extraPrintPriceCents: null,
          lumaboothSessionMode: 'PRINT',
          active: true,
        },
      ],
      activations: [
        {
          id: 'activation-id',
          boothId: 'booth-id',
          boothOfferId: 'offer-id',
          status: 'ACTIVE',
          startsAt: null,
          endsAt: null,
          sessionAllowance: 5,
          sessionsUsed: 2,
        },
      ],
      paymentAssignments: [],
      transactions: [
        {
          id: 'transaction-id',
          boothId: 'booth-id',
          boothOfferActivationId: null,
          transactionNumber: 'TXN-1',
          transactionType: 'SESSION_PURCHASE',
          status: 'PENDING_CASH',
          paymentMethod: 'CASH',
          amountCents: 25000,
          parentTransactionId: null,
          extraPrintCount: 0,
          canCreateExtraPrintAddOn: false,
          extraPrintUnitPriceCents: null,
          offerName: 'Per Session',
          offerType: 'PER_SESSION',
          includedPrintEntitlement: '2 pcs 6x2 or 1 pc 6x4',
          sessionAllowance: null,
          coveredSessionSequence: null,
          createdAt: new Date().toISOString(),
          paidAt: null,
          completedAt: null,
        },
      ],
      reports: emptyReports(),
      auditLogs: [],
    });
    app.setView('pos');
    fixture.detectChanges();
    await fixture.whenStable();

    const compiled = fixture.nativeElement as HTMLElement;

    expect(compiled.textContent).toContain('Cashier POS');
    expect(compiled.textContent).toContain('Current Payment Request');
    expect(compiled.textContent).toContain('Event Package / 2 of 5 used');
    expect(compiled.textContent).toContain('Assigned Booth Activity');
    expect(compiled.textContent).toContain('Approve Cash');
  });

  it('should show covered booth sessions as included activity instead of zero amount', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();

    const app = fixture.componentInstance as unknown as {
      session: { set: (value: unknown) => void };
      overview: { set: (value: unknown) => void };
      setView: (value: string) => void;
      setDashboardActivityFilter: (value: string) => void;
    };
    const session = {
      userId: 'client-owner-id',
      name: 'Client Owner',
      email: 'owner@memorybox.local',
      role: 'CLIENT_OWNER',
      clientAccountId: 'client-id',
      assignedBoothId: null,
    };

    app.session.set(session);
    app.overview.set({
      session,
      clients: [],
      subscriptionPlans: [],
      subscriptions: [],
      users: [],
      locations: [
        {
          id: 'location-id',
          clientAccountId: 'client-id',
          name: 'SM Southmall',
          status: 'ACTIVE',
        },
      ],
      booths: [
        {
          id: 'booth-id',
          clientAccountId: 'client-id',
          locationId: 'location-id',
          name: 'Booth A',
          code: 'SMA-001',
          status: 'ACTIVE',
          currentState: 'WELCOME',
          lastHeartbeatAt: null,
        },
      ],
      offers: [],
      activations: [],
      paymentAssignments: [],
      transactions: [
        {
          id: 'covered-session-id',
          boothId: 'booth-id',
          boothOfferActivationId: 'activation-id',
          transactionNumber: 'TXN-COVERED',
          transactionType: 'COVERED_PLAN_SESSION',
          status: 'COMPLETED',
          paymentMethod: 'CASH',
          amountCents: 0,
          parentTransactionId: null,
          extraPrintCount: 0,
          canCreateExtraPrintAddOn: false,
          extraPrintUnitPriceCents: null,
          offerName: 'Five Session Pass',
          offerType: 'SESSION_COUNT',
          includedPrintEntitlement: '2 pcs 6x2',
          sessionAllowance: 5,
          coveredSessionSequence: 1,
          createdAt: new Date().toISOString(),
          paidAt: new Date().toISOString(),
          completedAt: new Date().toISOString(),
        },
      ],
      reports: emptyReports(),
      auditLogs: [],
    });
    app.setView('dashboard');
    app.setDashboardActivityFilter('SESSIONS');
    fixture.detectChanges();
    await fixture.whenStable();

    const compiled = fixture.nativeElement as HTMLElement;
    const activityCard = Array.from(compiled.querySelectorAll('article.card')).find((card) =>
      card.textContent?.includes('Recent Booth Activity'),
    );

    expect(activityCard?.textContent).toContain('Recent Booth Activity');
    expect(activityCard?.textContent).toContain('Covered session');
    expect(activityCard?.textContent).toContain('1 of 5 used');
    expect(activityCard?.textContent).toContain('Included');
    expect(activityCard?.textContent).not.toContain('PHP 0');
  });

  it('should show package activation as a sale and hide it from session activity', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();

    const app = fixture.componentInstance as unknown as {
      session: { set: (value: unknown) => void };
      overview: { set: (value: unknown) => void };
      setView: (value: string) => void;
      setDashboardActivityFilter: (value: string) => void;
    };
    const session = {
      userId: 'client-owner-id',
      name: 'Client Owner',
      email: 'owner@memorybox.local',
      role: 'CLIENT_OWNER',
      clientAccountId: 'client-id',
      assignedBoothId: null,
    };

    app.session.set(session);
    app.overview.set({
      session,
      clients: [],
      subscriptionPlans: [],
      subscriptions: [],
      users: [],
      locations: [],
      booths: [
        {
          id: 'booth-id',
          clientAccountId: 'client-id',
          locationId: 'location-id',
          name: 'Booth A',
          code: 'SMA-001',
          status: 'ACTIVE',
          currentState: 'WELCOME',
          lastHeartbeatAt: null,
        },
      ],
      offers: [],
      activations: [],
      paymentAssignments: [],
      transactions: [
        {
          id: 'plan-activation-id',
          boothId: 'booth-id',
          boothOfferActivationId: 'activation-id',
          transactionNumber: 'TXN-PLAN',
          transactionType: 'PLAN_ACTIVATION',
          status: 'COMPLETED',
          paymentMethod: 'CASH',
          amountCents: 150000,
          parentTransactionId: null,
          extraPrintCount: 0,
          canCreateExtraPrintAddOn: false,
          extraPrintUnitPriceCents: null,
          offerName: 'One Hour Unlimited',
          offerType: 'TIME_UNLIMITED',
          includedPrintEntitlement: '2 pcs 6x2',
          sessionAllowance: null,
          coveredSessionSequence: null,
          createdAt: new Date().toISOString(),
          paidAt: new Date().toISOString(),
          completedAt: new Date().toISOString(),
        },
      ],
      reports: emptyReports(),
      auditLogs: [],
    });
    app.setView('dashboard');
    app.setDashboardActivityFilter('SALES');
    fixture.detectChanges();
    await fixture.whenStable();

    let compiled = fixture.nativeElement as HTMLElement;

    expect(compiled.textContent).toContain('Package activation');
    expect(compiled.textContent).toContain('PHP 1,500');

    app.setDashboardActivityFilter('SESSIONS');
    fixture.detectChanges();
    await fixture.whenStable();
    compiled = fixture.nativeElement as HTMLElement;

    expect(compiled.textContent).not.toContain('Package activation');
  });

  it('should show session-count package progress in booth status from latest completed session', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();

    const app = fixture.componentInstance as unknown as {
      session: { set: (value: unknown) => void };
      overview: { set: (value: unknown) => void };
      setView: (value: string) => void;
    };
    const session = {
      userId: 'client-owner-id',
      name: 'Client Owner',
      email: 'owner@memorybox.local',
      role: 'CLIENT_OWNER',
      clientAccountId: 'client-id',
      assignedBoothId: null,
    };

    app.session.set(session);
    app.overview.set({
      session,
      clients: [],
      subscriptionPlans: [],
      subscriptions: [],
      users: [],
      locations: [
        {
          id: 'location-id',
          clientAccountId: 'client-id',
          name: 'SM Southmall',
          status: 'ACTIVE',
        },
      ],
      booths: [
        {
          id: 'booth-id',
          clientAccountId: 'client-id',
          locationId: 'location-id',
          name: 'Booth A',
          code: 'SMA-001',
          status: 'ACTIVE',
          currentState: 'WELCOME',
          lastHeartbeatAt: null,
        },
      ],
      offers: [
        {
          id: 'offer-id',
          clientAccountId: 'client-id',
          name: 'Event Package',
          description: 'Five sessions',
          offerType: 'SESSION_COUNT',
          priceCents: 150000,
          currency: 'PHP',
          includedPrintEntitlement: '2 pcs 6x2',
          durationHours: null,
          sessionAllowance: 5,
          allowsExtraPrintAddOn: false,
          extraPrintPriceCents: null,
          lumaboothSessionMode: 'PRINT',
          active: true,
        },
      ],
      activations: [
        {
          id: 'activation-id',
          boothId: 'booth-id',
          boothOfferId: 'offer-id',
          status: 'ACTIVE',
          startsAt: null,
          endsAt: null,
          sessionAllowance: 5,
          sessionsUsed: 2,
        },
      ],
      paymentAssignments: [],
      transactions: [
        {
          id: 'covered-session-2',
          boothId: 'booth-id',
          boothOfferActivationId: 'activation-id',
          transactionNumber: 'TXN-COVERED-2',
          transactionType: 'COVERED_PLAN_SESSION',
          status: 'COMPLETED',
          paymentMethod: 'CASH',
          amountCents: 0,
          parentTransactionId: null,
          extraPrintCount: 0,
          canCreateExtraPrintAddOn: false,
          extraPrintUnitPriceCents: null,
          offerName: 'Event Package',
          offerType: 'SESSION_COUNT',
          includedPrintEntitlement: '2 pcs 6x2',
          sessionAllowance: 5,
          coveredSessionSequence: 2,
          createdAt: '2026-05-18T11:20:00Z',
          paidAt: '2026-05-18T11:20:00Z',
          completedAt: '2026-05-18T11:30:00Z',
        },
        {
          id: 'covered-session-1',
          boothId: 'booth-id',
          boothOfferActivationId: 'activation-id',
          transactionNumber: 'TXN-COVERED-1',
          transactionType: 'COVERED_PLAN_SESSION',
          status: 'COMPLETED',
          paymentMethod: 'CASH',
          amountCents: 0,
          parentTransactionId: null,
          extraPrintCount: 0,
          canCreateExtraPrintAddOn: false,
          extraPrintUnitPriceCents: null,
          offerName: 'Event Package',
          offerType: 'SESSION_COUNT',
          includedPrintEntitlement: '2 pcs 6x2',
          sessionAllowance: 5,
          coveredSessionSequence: 1,
          createdAt: '2026-05-18T10:20:00Z',
          paidAt: '2026-05-18T10:20:00Z',
          completedAt: '2026-05-18T10:30:00Z',
        },
      ],
      reports: emptyReports(),
      auditLogs: [],
    });
    app.setView('dashboard');
    fixture.detectChanges();
    await fixture.whenStable();

    const compiled = fixture.nativeElement as HTMLElement;
    const boothStatusCard = Array.from(compiled.querySelectorAll('article.card')).find((card) =>
      card.textContent?.includes('Booth Status'),
    );

    expect(boothStatusCard?.textContent).toContain('Event Package / 2 of 5 used');
  });

  it('should show timed package minutes and PH expiration in booth status', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    vi.spyOn(Date, 'now').mockReturnValue(new Date('2026-05-18T04:30:00Z').getTime());

    const app = fixture.componentInstance as unknown as {
      session: { set: (value: unknown) => void };
      overview: { set: (value: unknown) => void };
      setView: (value: string) => void;
    };
    const session = {
      userId: 'client-owner-id',
      name: 'Client Owner',
      email: 'owner@memorybox.local',
      role: 'CLIENT_OWNER',
      clientAccountId: 'client-id',
      assignedBoothId: null,
    };

    app.session.set(session);
    app.overview.set({
      session,
      clients: [],
      subscriptionPlans: [],
      subscriptions: [],
      users: [],
      locations: [
        {
          id: 'location-id',
          clientAccountId: 'client-id',
          name: 'SM Southmall',
          status: 'ACTIVE',
        },
      ],
      booths: [
        {
          id: 'booth-id',
          clientAccountId: 'client-id',
          locationId: 'location-id',
          name: 'Booth A',
          code: 'SMA-001',
          status: 'ACTIVE',
          currentState: 'WELCOME',
          lastHeartbeatAt: null,
        },
      ],
      offers: [
        {
          id: 'offer-id',
          clientAccountId: 'client-id',
          name: 'One Hour Pass',
          description: 'Timed event',
          offerType: 'TIME_UNLIMITED',
          priceCents: 150000,
          currency: 'PHP',
          includedPrintEntitlement: '2 pcs 6x2',
          durationHours: 1,
          sessionAllowance: null,
          allowsExtraPrintAddOn: false,
          extraPrintPriceCents: null,
          lumaboothSessionMode: 'PRINT',
          active: true,
        },
      ],
      activations: [
        {
          id: 'activation-id',
          boothId: 'booth-id',
          boothOfferId: 'offer-id',
          status: 'ACTIVE',
          startsAt: '2026-05-18T04:00:00Z',
          endsAt: '2026-05-18T05:00:00Z',
          sessionAllowance: null,
          sessionsUsed: 0,
        },
      ],
      paymentAssignments: [],
      transactions: [],
      reports: emptyReports(),
      auditLogs: [],
    });
    app.setView('dashboard');
    fixture.detectChanges();
    await fixture.whenStable();

    const compiled = fixture.nativeElement as HTMLElement;
    const boothStatusCard = Array.from(compiled.querySelectorAll('article.card')).find((card) =>
      card.textContent?.includes('Booth Status'),
    );

    expect(boothStatusCard?.textContent).toContain('One Hour Pass / 30 mins left of 60 mins');
    expect(boothStatusCard?.textContent).toContain('expires May 18, 2026');
    expect(boothStatusCard?.textContent).toContain('PH time');
    vi.restoreAllMocks();
  });

  it('should hide completed package context from live booth status', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();

    const app = fixture.componentInstance as unknown as {
      session: { set: (value: unknown) => void };
      overview: { set: (value: unknown) => void };
      setView: (value: string) => void;
    };
    const session = {
      userId: 'client-owner-id',
      name: 'Client Owner',
      email: 'owner@memorybox.local',
      role: 'CLIENT_OWNER',
      clientAccountId: 'client-id',
      assignedBoothId: null,
    };

    app.session.set(session);
    app.overview.set({
      session,
      clients: [],
      subscriptionPlans: [],
      subscriptions: [],
      users: [],
      locations: [
        {
          id: 'location-id',
          clientAccountId: 'client-id',
          name: 'SM Southmall',
          status: 'ACTIVE',
        },
      ],
      booths: [
        {
          id: 'booth-id',
          clientAccountId: 'client-id',
          locationId: 'location-id',
          name: 'Booth A',
          code: 'SMA-001',
          status: 'ACTIVE',
          currentState: 'WELCOME',
          lastHeartbeatAt: null,
        },
      ],
      offers: [
        {
          id: 'offer-id',
          clientAccountId: 'client-id',
          name: 'By Time',
          description: 'Timed event',
          offerType: 'TIME_UNLIMITED',
          priceCents: 150000,
          currency: 'PHP',
          includedPrintEntitlement: '2 pcs 6x2',
          durationHours: 1,
          sessionAllowance: null,
          allowsExtraPrintAddOn: false,
          extraPrintPriceCents: null,
          lumaboothSessionMode: 'PRINT',
          active: true,
        },
      ],
      activations: [
        {
          id: 'activation-id',
          boothId: 'booth-id',
          boothOfferId: 'offer-id',
          status: 'COMPLETED',
          startsAt: '2026-05-18T04:00:00Z',
          endsAt: '2026-05-18T05:00:00Z',
          sessionAllowance: null,
          sessionsUsed: 0,
        },
      ],
      paymentAssignments: [],
      transactions: [],
      reports: emptyReports(),
      auditLogs: [],
    });
    app.setView('dashboard');
    fixture.detectChanges();
    await fixture.whenStable();

    const compiled = fixture.nativeElement as HTMLElement;
    const boothStatusCard = Array.from(compiled.querySelectorAll('article.card')).find((card) =>
      card.textContent?.includes('Booth Status'),
    );

    expect(boothStatusCard?.textContent).toContain('Booth A');
    expect(boothStatusCard?.textContent).not.toContain('By Time');
    expect(boothStatusCard?.textContent).not.toContain('mins left');
  });

  it('should show extra print controls for latest eligible completed session', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();

    const app = fixture.componentInstance as unknown as {
      session: { set: (value: unknown) => void };
      overview: { set: (value: unknown) => void };
      setView: (value: string) => void;
    };
    const session = {
      userId: 'user-id',
      name: 'Cashier',
      email: 'cashier@memorybox.local',
      role: 'CASHIER',
      clientAccountId: 'client-id',
      assignedBoothId: 'booth-id',
    };

    app.session.set(session);
    app.overview.set({
      session,
      clients: [],
      subscriptionPlans: [],
      subscriptions: [],
      users: [],
      locations: [],
      booths: [
        {
          id: 'booth-id',
          clientAccountId: 'client-id',
          locationId: 'location-id',
          name: 'Booth A',
          code: 'SMA-001',
          status: 'ACTIVE',
          currentState: 'COMPLETED',
          lastHeartbeatAt: null,
        },
      ],
      offers: [],
      activations: [],
      paymentAssignments: [],
      transactions: [
        {
          id: 'transaction-id',
          boothId: 'booth-id',
          boothOfferActivationId: null,
          transactionNumber: 'TXN-1',
          transactionType: 'SESSION_PURCHASE',
          status: 'COMPLETED',
          paymentMethod: 'CASH',
          amountCents: 25000,
          parentTransactionId: null,
          extraPrintCount: 0,
          canCreateExtraPrintAddOn: true,
          extraPrintUnitPriceCents: 5000,
          offerName: 'Per Session',
          offerType: 'PER_SESSION',
          includedPrintEntitlement: '2 pcs 6x2 or 1 pc 6x4',
          sessionAllowance: null,
          coveredSessionSequence: null,
          createdAt: new Date().toISOString(),
          paidAt: new Date().toISOString(),
          completedAt: new Date().toISOString(),
        },
      ],
      reports: emptyReports(),
      auditLogs: [],
    });
    app.setView('pos');
    fixture.detectChanges();
    await fixture.whenStable();

    const compiled = fixture.nativeElement as HTMLElement;

    expect(compiled.textContent).toContain('Extra Prints');
    expect(compiled.textContent).toContain('Total PHP 50');
    expect(compiled.textContent).toContain('Create Extra Prints');
  });

  it('should keep package creation and editing on a separate detail view', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();

    const app = fixture.componentInstance as unknown as {
      session: { set: (value: unknown) => void };
      overview: { set: (value: unknown) => void };
      setView: (value: string) => void;
    };
    const session = {
      userId: 'client-owner-id',
      name: 'Client Owner',
      email: 'owner@memorybox.local',
      role: 'CLIENT_OWNER',
      clientAccountId: 'client-id',
      assignedBoothId: null,
    };

    app.session.set(session);
    app.overview.set({
      session,
      clients: [{ id: 'client-id', name: 'The Memory Box', status: 'ACTIVE' }],
      subscriptionPlans: [],
      subscriptions: [],
      users: [],
      locations: [],
      booths: [],
      offers: [
        {
          id: 'offer-id',
          clientAccountId: 'client-id',
          name: 'Per Session',
          description: 'Standard booth session',
          offerType: 'PER_SESSION',
          priceCents: 25000,
          currency: 'PHP',
          includedPrintEntitlement: '2 pcs 6x2 or 1 pc 6x4',
          durationHours: null,
          sessionAllowance: null,
          allowsExtraPrintAddOn: true,
          extraPrintPriceCents: 5000,
          lumaboothSessionMode: 'PRINT',
          active: true,
        },
      ],
      printEntitlements: [
        {
          id: 'entitlement-id',
          clientAccountId: 'client-id',
          name: '2 pcs 6x2 or 1 pc 6x4',
          status: 'ACTIVE',
        },
      ],
      activations: [],
      paymentAssignments: [],
      transactions: [],
      reports: emptyReports(),
      auditLogs: [],
    });
    app.setView('packages');
    fixture.detectChanges();
    await fixture.whenStable();

    let compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Packages');
    expect(compiled.textContent).toContain('Add-On Print Price');
    expect(compiled.textContent).toContain('Print Entitlements');
    expect(compiled.textContent).toContain('+ New Entitlement');
    expect(compiled.textContent).toContain('Manage');
    expect(compiled.textContent).not.toContain('Config');
    expect(compiled.textContent).not.toContain('Per paid session');
    expect(compiled.textContent).not.toContain('Configurable Packages');
    expect(compiled.textContent).not.toContain('One selected per booth');
    expect(compiled.textContent).not.toContain('PACKAGE NAME');
    expect(compiled.textContent).not.toContain('Print Entitlement Detail');

    const newEntitlementButton = Array.from(compiled.querySelectorAll('button')).find(
      (button) => button.textContent?.trim() === '+ New Entitlement',
    ) as HTMLButtonElement;
    newEntitlementButton.click();
    fixture.detectChanges();
    await fixture.whenStable();

    compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('New Print Entitlement');
    expect(compiled.textContent).toContain('PRINT ENTITLEMENT');
    expect(compiled.querySelector('.modal-backdrop')).not.toBeNull();

    const closeEntitlementButton = Array.from(compiled.querySelectorAll('button')).find(
      (button) => button.textContent?.trim() === 'Close',
    ) as HTMLButtonElement;
    closeEntitlementButton.click();
    fixture.detectChanges();
    await fixture.whenStable();

    const newPackageButton = Array.from(compiled.querySelectorAll('button')).find(
      (button) => button.textContent?.trim() === '+ New Package',
    ) as HTMLButtonElement;
    newPackageButton.click();
    fixture.detectChanges();
    await fixture.whenStable();

    compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Package Definition');
    expect(compiled.textContent).toContain('PACKAGE NAME');
    expect(compiled.textContent).toContain('DESCRIPTION');
    expect(compiled.textContent).toContain('TYPE');
    expect(compiled.textContent).toContain('PRINT ENTITLEMENT');
    expect(compiled.textContent).toContain('ADD-ON PRINT PRICE');
    expect(compiled.querySelector('select[name="packageStatus"]')).toBeNull();

    const cancelButton = Array.from(compiled.querySelectorAll('button')).find(
      (button) => button.textContent?.trim() === 'Cancel',
    ) as HTMLButtonElement;
    cancelButton.click();
    fixture.detectChanges();
    await fixture.whenStable();

    compiled = fixture.nativeElement as HTMLElement;
    const manageButton = Array.from(compiled.querySelectorAll('button')).find(
      (button) => button.textContent?.trim() === 'Manage',
    ) as HTMLButtonElement;
    manageButton.click();
    fixture.detectChanges();
    await fixture.whenStable();

    compiled = fixture.nativeElement as HTMLElement;
    expect((compiled.querySelector('input[name="packageName"]') as HTMLInputElement).value).toBe(
      'Per Session',
    );
    expect((compiled.querySelector('input[name="packagePrice"]') as HTMLInputElement).value).toBe(
      '250',
    );
    expect(compiled.textContent).toContain('Deactivate');
    expect(compiled.querySelector('select[name="packageStatus"]')).toBeNull();
  });

  it('should render reports and audit log views', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();

    const app = fixture.componentInstance as unknown as {
      session: { set: (value: unknown) => void };
      overview: { set: (value: unknown) => void };
      setView: (value: string) => void;
    };
    const session = {
      userId: 'user-id',
      name: 'Client Owner',
      email: 'owner@memorybox.local',
      role: 'CLIENT_OWNER',
      clientAccountId: 'client-id',
      assignedBoothId: null,
    };

    app.session.set(session);
    app.overview.set({
      session,
      clients: [],
      subscriptionPlans: [],
      subscriptions: [],
      users: [],
      locations: [],
      booths: [],
      offers: [],
      activations: [],
      paymentAssignments: [],
      transactions: [],
      reports: {
        ...emptyReports(),
        sales: {
          todayGrossSalesCents: 50000,
          todayCompletedSessions: 2,
          todayCashSalesCents: 50000,
          pendingCashCount: 0,
          failedOrExpiredCount: 0,
        },
        boothSales: [
          {
            boothId: 'booth-id',
            boothName: 'Booth A',
            completedSessions: 2,
            grossSalesCents: 50000,
          },
        ],
      },
      auditLogs: [
        {
          id: 'audit-id',
          clientAccountId: 'client-id',
          userId: 'user-id',
          action: 'transaction.cash_approved',
          entityType: 'Transaction',
          entityId: 'transaction-id',
          metadata: '{}',
          createdAt: new Date().toISOString(),
        },
      ],
    });

    app.setView('reports');
    fixture.detectChanges();
    await fixture.whenStable();

    let compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Today sales');
    expect(compiled.textContent).toContain('Booth A');

    app.setView('audit');
    fixture.detectChanges();
    await fixture.whenStable();

    compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Recent Audit Events');
    expect(compiled.textContent).toContain('transaction.cash_approved');
  });
});

function emptyReports() {
  return {
    platform: {
      activeClients: 0,
      activeBooths: 0,
      offlineBooths: 0,
      trialSubscriptions: 0,
      activeSubscriptions: 0,
      pastDueSubscriptions: 0,
      suspendedSubscriptions: 0,
      cancelledSubscriptions: 0,
      manualMrrCents: 0,
      clientsOverAllowance: 0,
    },
    sales: {
      todayGrossSalesCents: 0,
      todayCompletedSessions: 0,
      todayCashSalesCents: 0,
      pendingCashCount: 0,
      failedOrExpiredCount: 0,
    },
    boothSales: [],
    locationSales: [],
    offerSales: [],
  };
}
