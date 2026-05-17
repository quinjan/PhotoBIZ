import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { App } from './app';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideHttpClient()],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;

    expect(app).toBeTruthy();
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

  it('should render booth offline state from the overview API', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();

    const app = fixture.componentInstance as unknown as {
      session: { set: (value: unknown) => void };
      overview: { set: (value: unknown) => void };
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
    fixture.detectChanges();
    await fixture.whenStable();

    const compiled = fixture.nativeElement as HTMLElement;

    expect(compiled.textContent).toContain('Booth A');
    expect(compiled.textContent).toContain('OFFLINE');
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
      offers: [],
      activations: [],
      paymentAssignments: [],
      transactions: [
        {
          id: 'transaction-id',
          boothId: 'booth-id',
          transactionNumber: 'TXN-1',
          transactionType: 'SESSION_PURCHASE',
          status: 'PENDING_CASH',
          paymentMethod: 'CASH',
          amountCents: 25000,
          parentTransactionId: null,
          extraPrintCount: 0,
          canCreateExtraPrintAddOn: false,
          extraPrintUnitPriceCents: null,
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
    expect(compiled.textContent).toContain('Approve Cash');
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
          transactionNumber: 'TXN-1',
          transactionType: 'SESSION_PURCHASE',
          status: 'COMPLETED',
          paymentMethod: 'CASH',
          amountCents: 25000,
          parentTransactionId: null,
          extraPrintCount: 0,
          canCreateExtraPrintAddOn: true,
          extraPrintUnitPriceCents: 5000,
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
