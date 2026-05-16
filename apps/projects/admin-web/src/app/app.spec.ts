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

  it('should render the MVP console headline', async () => {
    const fixture = TestBed.createComponent(App);

    await fixture.whenStable();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain(
      'Backend-authoritative operator workspace',
    );
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
});
