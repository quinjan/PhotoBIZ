import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { App } from './app';

function boothConfig(overrides: Record<string, unknown> = {}): Record<string, unknown> {
  const base = {
    client: { displayName: 'The Memory Box', logoUrl: null },
    theme: {
      preset: 'VINTAGE',
      primaryColor: '#2f6868',
      accentColor: '#f5d27e',
      backgroundImageUrl: null,
      fontMode: 'serif',
    },
    session: {
      label: 'SM Manila',
      welcomeHeadline: 'Step Into The Memory Box',
      welcomeSubtitle: 'Welcome',
      completionThankYouMessage: 'Thanks for sharing your smile.',
    },
    booth: {
      id: 'booth-id',
      state: 'WELCOME',
      name: 'Booth A',
      code: 'SM-001',
      locationName: 'SM Manila',
    },
    activeOffer: {
      id: 'offer-id',
      name: 'Per Session',
      type: 'PER_SESSION',
      priceCents: 25000,
      currency: 'PHP',
      includedPrintEntitlement: '2 pcs 6x2 or 1 pc 6x4',
      allowsExtraPrintAddOn: true,
      extraPrintPriceCents: 5000,
      activationStatus: 'ACTIVE',
      startsAt: null,
      endsAt: null,
      sessionAllowance: null,
      sessionsUsed: 0,
    },
    paymentOptions: [{ method: 'CASH', label: 'Pay Cash', runtimeEnabled: true }],
    activeTransaction: null,
    recentTransaction: null,
  };

  return { ...base, ...overrides };
}

describe('App', () => {
  beforeEach(async () => {
    localStorage.clear();
    window.history.replaceState(null, '', '/');
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;

    expect(app).toBeTruthy();
  });

  it('should read the kiosk token from the launch URL path', async () => {
    window.history.replaceState(null, '', '/launch-token');
    const fixture = TestBed.createComponent(App);
    const http = TestBed.inject(HttpTestingController);

    const request = http.expectOne('http://localhost:5082/api/booth-ui/config');
    expect(request.request.headers.get('X-Kiosk-Token')).toBe('launch-token');
    request.flush(
      boothConfig({
        client: { displayName: 'Memory Box PH', logoUrl: '/tenant-logo.png' },
      }),
    );

    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Step Into The Memory Box');
    expect(compiled.querySelector('.booth-name img')).toBeNull();
    expect(compiled.textContent).toContain('Memory Box PH');
    expect(compiled.textContent).toContain('SM Manila | SM-001');
    expect(compiled.querySelector('.state-chip')?.textContent?.trim()).toBe('Welcome');
    expect(compiled.querySelector('.bottombar')?.textContent?.trim()).toBe('');
    expect(compiled.querySelector('input')).toBeNull();
    http.verify();
  });

  it('should render cash-only payment selection from the backend active transaction', async () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance as unknown as {
      config: { set: (value: unknown) => void };
    };

    app.config.set(
      boothConfig({
        booth: { id: 'booth-id', state: 'OFFER_CONFIRMED' },
        activeTransaction: {
          id: 'transaction-id',
          transactionNumber: 'TXN-001',
          transactionType: 'SESSION_PURCHASE',
          status: 'CREATED',
          paymentMethod: 'PENDING',
          amountCents: 25000,
          currency: 'PHP',
          createdAt: new Date(Date.now()).toISOString(),
          expiresAt: new Date(Date.now() + 60_000).toISOString(),
        },
      }),
    );
    fixture.detectChanges();
    await fixture.whenStable();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Payment Options');
    expect(fixture.nativeElement.querySelector('.payment-options-head .label')).toBeNull();
    expect(text).toContain('Pay Cash');
    expect(text).not.toContain('QRPH');
    expect(text).not.toContain('Maya');
  });

  it('should count down the Vintage payment auto-return from the backend transaction creation time', async () => {
    vi.useFakeTimers();

    try {
      const createdAt = new Date('2026-05-23T08:00:00.000Z');
      vi.setSystemTime(createdAt);
      const fixture = TestBed.createComponent(App);
      const app = fixture.componentInstance as unknown as {
        config: { set: (value: unknown) => void };
      };

      app.config.set(
        boothConfig({
          booth: { id: 'booth-id', state: 'OFFER_CONFIRMED' },
          activeTransaction: {
            id: 'transaction-id',
            transactionNumber: 'TXN-001',
            transactionType: 'SESSION_PURCHASE',
            status: 'CREATED',
            paymentMethod: 'PENDING',
            amountCents: 25000,
            currency: 'PHP',
            createdAt: createdAt.toISOString(),
            expiresAt: new Date(createdAt.getTime() + 60_000).toISOString(),
          },
        }),
      );
      fixture.detectChanges();

      expect((fixture.nativeElement as HTMLElement).textContent).toContain('30s');

      vi.advanceTimersByTime(15_000);
      fixture.detectChanges();

      expect((fixture.nativeElement as HTMLElement).textContent).toContain('15s');
    } finally {
      vi.useRealTimers();
    }
  });

  it('should render cash waiting copy without duplicate title or transaction row', async () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance as unknown as {
      config: { set: (value: unknown) => void };
    };

    app.config.set(
      boothConfig({
        booth: { id: 'booth-id', state: 'PAYMENT_PENDING' },
        activeTransaction: {
          id: 'transaction-id',
          transactionNumber: 'TXN-001',
          transactionType: 'SESSION_PURCHASE',
          status: 'PENDING_CASH',
          paymentMethod: 'CASH',
          amountCents: 25000,
          currency: 'PHP',
          createdAt: new Date(Date.now() - 30_000).toISOString(),
          expiresAt: new Date(Date.now() + 30_000).toISOString(),
        },
      }),
    );
    fixture.detectChanges();
    await fixture.whenStable();

    const compiled = fixture.nativeElement as HTMLElement;
    const ticket = compiled.querySelector('.ticket') as HTMLElement;
    const progress = compiled.querySelector('.timeout-fill') as HTMLElement;
    const progressValue = Number.parseFloat(progress.style.getPropertyValue('--progress'));

    expect(ticket.querySelector('h1')).toBeNull();
    expect(ticket.textContent).toContain(
      'Please wait while the cashier confirms payment option. Please pay at the cashier after using the booth.',
    );
    expect(compiled.querySelector('.timeout-panel')?.textContent).not.toContain('Transaction');
    expect(compiled.querySelector('.timeout-panel')?.textContent).toContain('Time Left');
    expect(progressValue).toBeGreaterThan(48);
    expect(progressValue).toBeLessThanOrEqual(50);
  });

  it('should refresh backend config when pending cash reaches its expiry time', async () => {
    vi.useFakeTimers();

    try {
      const now = new Date('2026-05-23T08:00:30.000Z');
      vi.setSystemTime(now);
      const fixture = TestBed.createComponent(App);
      const http = TestBed.inject(HttpTestingController);
      const app = fixture.componentInstance as unknown as {
        loadConfig: () => Promise<void>;
      };

      void app.loadConfig();
      http.expectOne('http://localhost:5082/api/booth-ui/config').flush(
        boothConfig({
          booth: { id: 'booth-id', state: 'PAYMENT_PENDING' },
          activeTransaction: {
            id: 'transaction-id',
            transactionNumber: 'TXN-001',
            transactionType: 'SESSION_PURCHASE',
            status: 'PENDING_CASH',
            paymentMethod: 'CASH',
            amountCents: 25000,
            currency: 'PHP',
            createdAt: new Date(now.getTime() - 30_000).toISOString(),
            expiresAt: new Date(now.getTime() + 30_000).toISOString(),
          },
        }),
      );
      await Promise.resolve();
      fixture.detectChanges();

      vi.advanceTimersByTime(30_000);

      const refreshRequest = http.expectOne('http://localhost:5082/api/booth-ui/config');
      refreshRequest.flush(
        boothConfig({
          recentTransaction: {
            id: 'transaction-id',
            status: 'EXPIRED',
            transactionType: 'SESSION_PURCHASE',
            occurredAt: new Date(now.getTime() + 30_000).toISOString(),
            reason: null,
          },
        }),
      );
      await Promise.resolve();
      fixture.detectChanges();

      expect((fixture.nativeElement as HTMLElement).textContent).toContain('Request Expired');
      http.verify();
    } finally {
      vi.useRealTimers();
    }
  });

  it.each([
    ['VINTAGE', 'SESSION_COUNT', 'Session Count 5'],
    ['CLEAN_MODERN', 'SESSION_COUNT', 'Session Count 5'],
    ['POP', 'SESSION_COUNT', 'Session Count 5'],
    ['VINTAGE', 'BY_TIME', '60 Minute Booth'],
    ['CLEAN_MODERN', 'BY_TIME', '60 Minute Booth'],
    ['POP', 'BY_TIME', '60 Minute Booth'],
  ])(
    'should hide %s %s package and price while preserving a spacer',
    async (preset, offerType, offerName) => {
      const fixture = TestBed.createComponent(App);
      const app = fixture.componentInstance as unknown as {
        config: { set: (value: unknown) => void };
      };

      app.config.set(
        boothConfig({
          theme: {
            preset,
            primaryColor: '#2f6868',
            accentColor: '#f5d27e',
            backgroundImageUrl: null,
            fontMode: preset === 'VINTAGE' ? 'serif' : 'sans',
          },
          activeOffer: {
            id: 'offer-id',
            name: offerName,
            type: offerType,
            priceCents: 80000,
            currency: 'PHP',
            includedPrintEntitlement: '2 pcs 6x2 or 1 pc 6x4',
            allowsExtraPrintAddOn: false,
            extraPrintPriceCents: null,
            activationStatus: 'ACTIVE',
            startsAt: null,
            endsAt: null,
            sessionAllowance: 5,
            sessionsUsed: 0,
          },
        }),
      );
      fixture.detectChanges();
      await fixture.whenStable();

      const compiled = fixture.nativeElement as HTMLElement;
      const text = compiled.textContent ?? '';

      expect(text).not.toContain(offerName);
      expect(text).not.toContain('PHP 800');
      expect(text).not.toContain('2 pcs 6x2 or 1 pc 6x4');
      expect(
        compiled.querySelector('.offer-line-spacer, .offer-panel-spacer, .offer-spacer'),
      ).toBeTruthy();
    },
  );

  it('should hide the Vintage approved status copy', async () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance as unknown as {
      config: { set: (value: unknown) => void };
    };

    app.config.set(boothConfig({ booth: { id: 'booth-id', state: 'STARTING_LUMABOOTH' } }));
    fixture.detectChanges();
    await fixture.whenStable();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Starting Session');
    expect(text).not.toContain('Payment confirmed. The booth session is starting.');
  });

  it('should render the configurable completed thank-you message and Back To Start label', async () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance as unknown as {
      config: { set: (value: unknown) => void };
    };

    app.config.set(
      boothConfig({
        session: {
          label: 'SM Manila',
          welcomeHeadline: 'Step Into The Memory Box',
          welcomeSubtitle: 'Welcome',
          completionThankYouMessage: 'Thank you for making this memory.',
        },
        booth: { id: 'booth-id', state: 'COMPLETED' },
      }),
    );
    fixture.detectChanges();
    await fixture.whenStable();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Thank you for making this memory.');
    expect(text).toContain('Need extra prints? Please go to the cashier.');
    expect(text.match(/Need extra prints\?/g)).toHaveLength(1);
    expect(text).toContain('Back To Start');
  });

  it('should hide extra-print copy for covered completed sessions', async () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance as unknown as {
      config: { set: (value: unknown) => void };
    };

    app.config.set(
      boothConfig({
        booth: { id: 'booth-id', state: 'COMPLETED' },
        activeOffer: {
          id: 'offer-id',
          name: 'Session Pass',
          type: 'SESSION_COUNT',
          priceCents: 150000,
          currency: 'PHP',
          includedPrintEntitlement: '2 pcs 6x2 or 1 pc 6x4',
          allowsExtraPrintAddOn: false,
          extraPrintPriceCents: null,
          activationStatus: 'ACTIVE',
          startsAt: null,
          endsAt: null,
          sessionAllowance: 5,
          sessionsUsed: 1,
        },
        paymentOptions: [],
      }),
    );
    fixture.detectChanges();
    await fixture.whenStable();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Thanks for sharing your smile.');
    expect(text).toContain('Back To Start');
    expect(text).not.toContain('Need extra prints?');
  });

  it('should return a completed booth to welcome through the backend', async () => {
    const fixture = TestBed.createComponent(App);
    const http = TestBed.inject(HttpTestingController);
    const app = fixture.componentInstance as unknown as {
      kioskToken: { set: (value: string) => void };
      config: { set: (value: unknown) => void };
      handleStageAction: (action: string) => void;
    };
    const completedConfig = boothConfig({ booth: { id: 'booth-id', state: 'COMPLETED' } });
    app.kioskToken.set('kiosk-token');
    app.config.set(completedConfig);

    app.handleStageAction('return-welcome');

    const returnRequest = http.expectOne('http://localhost:5082/api/booth-ui/return-to-welcome');
    expect(returnRequest.request.method).toBe('POST');
    expect(returnRequest.request.headers.get('X-Kiosk-Token')).toBe('kiosk-token');
    returnRequest.flush({});
    await Promise.resolve();

    const configRequest = http.expectOne('http://localhost:5082/api/booth-ui/config');
    configRequest.flush(boothConfig());

    await fixture.whenStable();
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain(
      'Step Into The Memory Box',
    );
    http.verify();
  });

  it('should auto-return the completed prompt through the backend after fifteen seconds', async () => {
    vi.useFakeTimers();

    try {
      const fixture = TestBed.createComponent(App);
      const http = TestBed.inject(HttpTestingController);
      const app = fixture.componentInstance as unknown as {
        loadConfig: () => Promise<void>;
      };
      const completedConfig = boothConfig({ booth: { id: 'booth-id', state: 'COMPLETED' } });

      void app.loadConfig();
      http.expectOne('http://localhost:5082/api/booth-ui/config').flush(completedConfig);
      await Promise.resolve();
      fixture.detectChanges();

      expect((fixture.nativeElement as HTMLElement).textContent).toContain('15s');
      vi.advanceTimersByTime(5_000);
      fixture.detectChanges();
      expect((fixture.nativeElement as HTMLElement).textContent).toContain('10s');

      vi.advanceTimersByTime(10_000);
      const returnRequest = http.expectOne('http://localhost:5082/api/booth-ui/return-to-welcome');
      returnRequest.flush({});
      await Promise.resolve();

      const refreshRequest = http.expectOne('http://localhost:5082/api/booth-ui/config');
      refreshRequest.flush(boothConfig());
      await Promise.resolve();
      fixture.detectChanges();

      expect((fixture.nativeElement as HTMLElement).textContent).toContain(
        'Step Into The Memory Box',
      );
      http.verify();
    } finally {
      vi.useRealTimers();
    }
  });

  it('should render cancelled transactions as a full screen and acknowledge through the backend', async () => {
    const fixture = TestBed.createComponent(App);
    const http = TestBed.inject(HttpTestingController);
    const app = fixture.componentInstance as unknown as {
      kioskToken: { set: (value: string) => void };
      config: { set: (value: unknown) => void };
      handleStageAction: (action: string) => void;
    };
    app.kioskToken.set('kiosk-token');
    app.config.set(
      boothConfig({
        recentTransaction: {
          id: 'transaction-id',
          status: 'CANCELLED',
          transactionType: 'SESSION_PURCHASE',
          occurredAt: new Date().toISOString(),
          reason: 'The cashier cancelled this request.',
        },
      }),
    );
    fixture.detectChanges();
    await fixture.whenStable();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Request Cancelled');
    expect(compiled.textContent).toContain('The cashier cancelled this request.');
    expect(compiled.textContent).toContain('Back To Start');
    expect(compiled.querySelector('.payment-notice')).toBeNull();
    expect(compiled.querySelector('.toast-region')).toBeNull();
    expect(compiled.querySelector('.busy-overlay')).toBeNull();

    app.handleStageAction('acknowledge-recent');

    const acknowledgeRequest = http.expectOne(
      'http://localhost:5082/api/booth-ui/recent-transactions/transaction-id/acknowledge',
    );
    expect(acknowledgeRequest.request.method).toBe('POST');
    expect(acknowledgeRequest.request.headers.get('X-Kiosk-Token')).toBe('kiosk-token');
    acknowledgeRequest.flush({});
    await Promise.resolve();

    http.expectOne('http://localhost:5082/api/booth-ui/config').flush(boothConfig());
    await fixture.whenStable();
    http.verify();
  });

  it('should auto-acknowledge terminal outcomes after fifteen seconds without local dismissal', async () => {
    vi.useFakeTimers();

    try {
      const fixture = TestBed.createComponent(App);
      const http = TestBed.inject(HttpTestingController);
      const app = fixture.componentInstance as unknown as {
        loadConfig: () => Promise<void>;
      };

      void app.loadConfig();
      http.expectOne('http://localhost:5082/api/booth-ui/config').flush(
        boothConfig({
          recentTransaction: {
            id: 'transaction-id',
            status: 'PAYMENT_FAILED',
            transactionType: 'SESSION_PURCHASE',
            occurredAt: new Date().toISOString(),
            reason: null,
          },
        }),
      );
      await Promise.resolve();
      fixture.detectChanges();

      expect((fixture.nativeElement as HTMLElement).textContent).toContain('Payment Failed');
      expect((fixture.nativeElement as HTMLElement).textContent).toContain('15s');
      vi.advanceTimersByTime(5_000);
      fixture.detectChanges();
      expect((fixture.nativeElement as HTMLElement).textContent).toContain('Payment Failed');
      expect((fixture.nativeElement as HTMLElement).textContent).toContain('10s');

      vi.advanceTimersByTime(10_000);
      const acknowledgeRequest = http.expectOne(
        'http://localhost:5082/api/booth-ui/recent-transactions/transaction-id/acknowledge',
      );
      acknowledgeRequest.flush({});
      await Promise.resolve();

      http.expectOne('http://localhost:5082/api/booth-ui/config').flush(boothConfig());
      await Promise.resolve();
      fixture.detectChanges();

      expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('Payment Failed');
      http.verify();
    } finally {
      vi.useRealTimers();
    }
  });

  it('should cancel a created transaction from the payment back action', async () => {
    const fixture = TestBed.createComponent(App);
    const http = TestBed.inject(HttpTestingController);
    const app = fixture.componentInstance as unknown as {
      config: { set: (value: unknown) => void };
      handleStageAction: (action: string) => void;
    };
    app.config.set(
      boothConfig({
        booth: { id: 'booth-id', state: 'OFFER_CONFIRMED' },
        activeTransaction: {
          id: 'transaction-id',
          transactionNumber: 'TXN-001',
          transactionType: 'SESSION_PURCHASE',
          status: 'CREATED',
          paymentMethod: 'PENDING',
          amountCents: 25000,
          currency: 'PHP',
          createdAt: new Date(Date.now()).toISOString(),
          expiresAt: new Date(Date.now() + 60_000).toISOString(),
        },
      }),
    );

    app.handleStageAction('cancel-transaction');

    const cancelRequest = http.expectOne(
      'http://localhost:5082/api/booth-ui/transactions/transaction-id/cancel',
    );
    expect(cancelRequest.request.method).toBe('POST');
    expect(cancelRequest.request.body).toEqual({ trigger: 'BACK_BUTTON' });
    cancelRequest.flush({});
    await Promise.resolve();

    http.expectOne('http://localhost:5082/api/booth-ui/config').flush(boothConfig());
    await fixture.whenStable();
    http.verify();
  });

  it('should auto-cancel created payment selections after the idle timer', async () => {
    vi.useFakeTimers();

    try {
      const now = new Date('2026-05-23T08:00:20.000Z');
      const createdAt = new Date(now.getTime() - 20_000);
      vi.setSystemTime(now);
      const fixture = TestBed.createComponent(App);
      const http = TestBed.inject(HttpTestingController);
      const app = fixture.componentInstance as unknown as {
        loadConfig: () => Promise<void>;
      };

      void app.loadConfig();
      http.expectOne('http://localhost:5082/api/booth-ui/config').flush(
        boothConfig({
          booth: { id: 'booth-id', state: 'OFFER_CONFIRMED' },
          activeTransaction: {
            id: 'transaction-id',
            transactionNumber: 'TXN-001',
            transactionType: 'SESSION_PURCHASE',
            status: 'CREATED',
            paymentMethod: 'PENDING',
            amountCents: 25000,
            currency: 'PHP',
            createdAt: createdAt.toISOString(),
            expiresAt: new Date(createdAt.getTime() + 60_000).toISOString(),
          },
        }),
      );
      await Promise.resolve();
      fixture.detectChanges();

      vi.advanceTimersByTime(10_000);
      expect((fixture.nativeElement as HTMLElement).textContent).toContain('Payment Options');

      const cancelRequest = http.expectOne(
        'http://localhost:5082/api/booth-ui/transactions/transaction-id/cancel',
      );
      expect(cancelRequest.request.body).toEqual({ trigger: 'IDLE_TIMEOUT' });
      cancelRequest.flush({});
      await Promise.resolve();

      http.expectOne('http://localhost:5082/api/booth-ui/config').flush(boothConfig());
      await Promise.resolve();
      http.verify();
    } finally {
      vi.useRealTimers();
    }
  });

  it('should map session failed transactions to the recovery screen', async () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance as unknown as {
      config: { set: (value: unknown) => void };
    };

    app.config.set(
      boothConfig({
        activeTransaction: {
          id: 'transaction-id',
          transactionNumber: 'TXN-001',
          transactionType: 'SESSION_PURCHASE',
          status: 'SESSION_FAILED',
          paymentMethod: 'CASH',
          amountCents: 25000,
          currency: 'PHP',
          createdAt: new Date(Date.now()).toISOString(),
          expiresAt: new Date(Date.now() + 60_000).toISOString(),
        },
      }),
    );
    fixture.detectChanges();
    await fixture.whenStable();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Recovery Needed');
    expect(text).toContain('Check Status');
  });
});
