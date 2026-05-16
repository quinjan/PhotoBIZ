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

  it('should render the operations headline', async () => {
    const fixture = TestBed.createComponent(App);

    await fixture.whenStable();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('Operations Console');
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
          status: 'PENDING_CASH',
          paymentMethod: 'CASH',
          amountCents: 25000,
          createdAt: new Date().toISOString(),
          paidAt: null,
          completedAt: null,
        },
      ],
    });
    app.setView('pos');
    fixture.detectChanges();
    await fixture.whenStable();

    const compiled = fixture.nativeElement as HTMLElement;

    expect(compiled.textContent).toContain('Cashier POS');
    expect(compiled.textContent).toContain('Approve Cash');
  });
});
