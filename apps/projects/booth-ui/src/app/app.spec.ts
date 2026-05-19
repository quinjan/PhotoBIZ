import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { vi } from 'vitest';
import { App } from './app';

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

  it('should not render manual kiosk token controls', async () => {
    const fixture = TestBed.createComponent(App);

    await fixture.whenStable();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).not.toContain('Kiosk token');
    expect(compiled.querySelector('input')).toBeNull();
  });

  it('should read the kiosk token from the launch URL path', async () => {
    window.history.replaceState(null, '', '/launch-token');
    const fixture = TestBed.createComponent(App);
    const http = TestBed.inject(HttpTestingController);

    const request = http.expectOne('http://localhost:5082/api/booth-ui/config');
    expect(request.request.headers.get('X-Kiosk-Token')).toBe('launch-token');
    request.flush({
      client: { displayName: 'The Memory Box', logoUrl: null },
      theme: {
        preset: 'VINTAGE_FILM',
        primaryColor: '#2f6868',
        accentColor: '#f5d27e',
        backgroundImageUrl: null,
        fontMode: 'serif',
      },
      session: {
        label: 'SM Manila',
        welcomeHeadline: 'Step Into The Memory Box',
        welcomeSubtitle: 'Welcome',
      },
      booth: { id: 'booth-id', state: 'WELCOME' },
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
      paymentOptions: [{ method: 'CASH', label: 'Cash', runtimeEnabled: true }],
    });

    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Step Into The Memory Box');
    expect(compiled.querySelector('input')).toBeNull();
    http.verify();
  });

  it('should show agent offline retry state when booth is offline', async () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance as unknown as {
      config: { set: (value: unknown) => void };
    };

    const config = {
      client: { displayName: 'The Memory Box', logoUrl: null },
      theme: {
        preset: 'VINTAGE_FILM',
        primaryColor: '#2f6868',
        accentColor: '#f5d27e',
        backgroundImageUrl: null,
        fontMode: 'serif',
      },
      session: {
        label: 'SM Manila',
        welcomeHeadline: 'Step Into The Memory Box',
        welcomeSubtitle: 'Welcome',
      },
      booth: { id: 'booth-id', state: 'OFFLINE' },
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
      paymentOptions: [{ method: 'CASH', label: 'Cash', runtimeEnabled: true }],
    };
    app.config.set(config);
    fixture.detectChanges();
    await fixture.whenStable();

    const compiled = fixture.nativeElement as HTMLElement;

    expect(compiled.textContent).toContain('Agent Offline');
    expect(compiled.textContent).toContain('Retry');
  });

  it('should render payment selection after offer confirmation', async () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance as unknown as {
      config: { set: (value: unknown) => void };
      transaction: { set: (value: unknown) => void };
    };

    app.config.set({
      client: { displayName: 'The Memory Box', logoUrl: null },
      theme: {
        preset: 'VINTAGE_FILM',
        primaryColor: '#2f6868',
        accentColor: '#f5d27e',
        backgroundImageUrl: null,
        fontMode: 'serif',
      },
      session: {
        label: 'SM Manila',
        welcomeHeadline: 'Step Into The Memory Box',
        welcomeSubtitle: 'Welcome',
      },
      booth: { id: 'booth-id', state: 'OFFER_CONFIRMED' },
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
      paymentOptions: [{ method: 'CASH', label: 'Cash', runtimeEnabled: true }],
    });
    app.transaction.set({ id: 'transaction-id', status: 'CREATED' });
    fixture.detectChanges();
    await fixture.whenStable();

    const compiled = fixture.nativeElement as HTMLElement;

    expect(compiled.textContent).toContain('Choose Payment');
    expect(compiled.textContent).toContain('Cash');
  });

  it('should render the post-session extra print prompt when booth is completed', async () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance as unknown as {
      config: { set: (value: unknown) => void };
    };

    const config = {
      client: { displayName: 'The Memory Box', logoUrl: null },
      theme: {
        preset: 'VINTAGE_FILM',
        primaryColor: '#2f6868',
        accentColor: '#f5d27e',
        backgroundImageUrl: null,
        fontMode: 'serif',
      },
      session: {
        label: 'SM Manila',
        welcomeHeadline: 'Step Into The Memory Box',
        welcomeSubtitle: 'Welcome',
      },
      booth: { id: 'booth-id', state: 'COMPLETED' },
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
      paymentOptions: [{ method: 'CASH', label: 'Cash', runtimeEnabled: true }],
    };
    app.config.set(config);
    fixture.detectChanges();
    await fixture.whenStable();

    const compiled = fixture.nativeElement as HTMLElement;

    expect(compiled.textContent).toContain('Need extra prints?');
    expect(compiled.textContent).toContain('Please go to the cashier.');
    expect(compiled.textContent).toContain('No Extra Prints');
    expect(compiled.textContent).toContain('Extra prints PHP 50 each');
  });

  it('should not render the extra print prompt for session-count completed sessions', async () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance as unknown as {
      config: { set: (value: unknown) => void };
    };

    app.config.set({
      client: { displayName: 'The Memory Box', logoUrl: null },
      theme: {
        preset: 'VINTAGE_FILM',
        primaryColor: '#2f6868',
        accentColor: '#f5d27e',
        backgroundImageUrl: null,
        fontMode: 'serif',
      },
      session: {
        label: 'SM Manila',
        welcomeHeadline: 'Step Into The Memory Box',
        welcomeSubtitle: 'Welcome',
      },
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
    });
    fixture.detectChanges();
    await fixture.whenStable();

    const compiled = fixture.nativeElement as HTMLElement;

    expect(compiled.textContent).toContain('Session Complete');
    expect(compiled.textContent).toContain('Back To Welcome');
    expect(compiled.textContent).not.toContain('Need extra prints?');
    expect(compiled.textContent).not.toContain('Extra prints');
  });

  it('should return a completed booth to welcome when no extra prints are needed', async () => {
    const fixture = TestBed.createComponent(App);
    const http = TestBed.inject(HttpTestingController);
    const app = fixture.componentInstance as unknown as {
      kioskToken: { set: (value: string) => void };
      config: { set: (value: unknown) => void };
      handleStageAction: (action: string) => void;
    };
    const config = {
      client: { displayName: 'The Memory Box', logoUrl: null },
      theme: {
        preset: 'VINTAGE_FILM',
        primaryColor: '#2f6868',
        accentColor: '#f5d27e',
        backgroundImageUrl: null,
        fontMode: 'serif',
      },
      session: {
        label: 'SM Manila',
        welcomeHeadline: 'Step Into The Memory Box',
        welcomeSubtitle: 'Welcome',
      },
      booth: { id: 'booth-id', state: 'COMPLETED' },
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
      paymentOptions: [{ method: 'CASH', label: 'Cash', runtimeEnabled: true }],
    };
    app.kioskToken.set('kiosk-token');
    app.config.set(config);

    app.handleStageAction('return-welcome');

    const returnRequest = http.expectOne('http://localhost:5082/api/booth-ui/return-to-welcome');
    expect(returnRequest.request.method).toBe('POST');
    expect(returnRequest.request.headers.get('X-Kiosk-Token')).toBe('kiosk-token');
    returnRequest.flush({});
    await Promise.resolve();

    const configRequest = http.expectOne('http://localhost:5082/api/booth-ui/config');
    configRequest.flush({ ...config, booth: { id: 'booth-id', state: 'WELCOME' } });

    await fixture.whenStable();
    http.verify();
  });

  it('should show an error, keep completed state, and retry when the button return command fails', async () => {
    vi.useFakeTimers();

    try {
      const fixture = TestBed.createComponent(App);
      const http = TestBed.inject(HttpTestingController);
      const app = fixture.componentInstance as unknown as {
        kioskToken: { set: (value: string) => void };
        config: { set: (value: unknown) => void };
        handleStageAction: (action: string) => void;
      };
      const config = {
        client: { displayName: 'The Memory Box', logoUrl: null },
        theme: {
          preset: 'VINTAGE_FILM',
          primaryColor: '#2f6868',
          accentColor: '#f5d27e',
          backgroundImageUrl: null,
          fontMode: 'serif',
        },
        session: {
          label: 'SM Manila',
          welcomeHeadline: 'Step Into The Memory Box',
          welcomeSubtitle: 'Welcome',
        },
        booth: { id: 'booth-id', state: 'COMPLETED' },
        activeOffer: {
          id: 'offer-id',
          name: 'Session Pass',
          type: 'TIME_UNLIMITED',
          priceCents: 150000,
          currency: 'PHP',
          includedPrintEntitlement: '2 pcs 6x2 or 1 pc 6x4',
          allowsExtraPrintAddOn: false,
          extraPrintPriceCents: null,
          activationStatus: 'ACTIVE',
          startsAt: '2026-05-18T08:00:00Z',
          endsAt: '2026-05-18T13:00:00Z',
          sessionAllowance: null,
          sessionsUsed: 0,
        },
        paymentOptions: [],
      };
      app.kioskToken.set('kiosk-token');
      app.config.set(config);

      app.handleStageAction('return-welcome');

      const returnRequest = http.expectOne('http://localhost:5082/api/booth-ui/return-to-welcome');
      returnRequest.flush({}, { status: 404, statusText: 'Not Found' });
      await Promise.resolve();
      fixture.detectChanges();

      let compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.textContent).toContain('Session Complete');
      expect(compiled.textContent).toContain('Could not return to welcome.');

      vi.advanceTimersByTime(1_000);
      const retryRequest = http.expectOne('http://localhost:5082/api/booth-ui/return-to-welcome');
      retryRequest.flush({});
      await Promise.resolve();

      const configRequest = http.expectOne('http://localhost:5082/api/booth-ui/config');
      configRequest.flush({ ...config, booth: { id: 'booth-id', state: 'WELCOME' } });
      await Promise.resolve();
      fixture.detectChanges();

      compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.textContent).toContain('Step Into The Memory Box');
      expect(compiled.textContent).not.toContain('Session Complete');
      expect(compiled.textContent).not.toContain('Could not return to welcome.');
      http.verify();
    } finally {
      vi.useRealTimers();
    }
  });

  it('should auto-return the completed prompt to welcome after fifteen seconds', async () => {
    vi.useFakeTimers();

    try {
      const fixture = TestBed.createComponent(App);
      const http = TestBed.inject(HttpTestingController);
      const app = fixture.componentInstance as unknown as {
        loadConfig: () => Promise<void>;
      };
      const completedConfig = {
        client: { displayName: 'The Memory Box', logoUrl: null },
        theme: {
          preset: 'VINTAGE_FILM',
          primaryColor: '#2f6868',
          accentColor: '#f5d27e',
          backgroundImageUrl: null,
          fontMode: 'serif',
        },
        session: {
          label: 'SM Manila',
          welcomeHeadline: 'Step Into The Memory Box',
          welcomeSubtitle: 'Welcome',
        },
        booth: { id: 'booth-id', state: 'COMPLETED' },
        activeOffer: {
          id: 'offer-id',
          name: 'Session Pass',
          type: 'TIME_UNLIMITED',
          priceCents: 150000,
          currency: 'PHP',
          includedPrintEntitlement: '2 pcs 6x2 or 1 pc 6x4',
          allowsExtraPrintAddOn: false,
          extraPrintPriceCents: null,
          activationStatus: 'ACTIVE',
          startsAt: '2026-05-18T08:00:00Z',
          endsAt: '2026-05-18T13:00:00Z',
          sessionAllowance: null,
          sessionsUsed: 0,
        },
        paymentOptions: [],
      };
      void app.loadConfig();
      const initialConfigRequest = http.expectOne('http://localhost:5082/api/booth-ui/config');
      initialConfigRequest.flush(completedConfig);
      await Promise.resolve();

      vi.advanceTimersByTime(15_000);
      const returnRequest = http.expectOne('http://localhost:5082/api/booth-ui/return-to-welcome');
      expect(returnRequest.request.method).toBe('POST');
      returnRequest.flush({});
      await Promise.resolve();

      const refreshRequest = http.expectOne('http://localhost:5082/api/booth-ui/config');
      refreshRequest.flush({
        ...completedConfig,
        booth: { id: 'booth-id', state: 'WELCOME' },
      });
      await Promise.resolve();
      fixture.detectChanges();

      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.textContent).toContain('Step Into The Memory Box');
      expect(compiled.textContent).not.toContain('Session Complete');
      expect(compiled.textContent).not.toContain('Could not return to welcome.');
      http.verify();
    } finally {
      vi.useRealTimers();
    }
  });

  it('should render a cancelled payment notice over the welcome screen from recent booth config', async () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance as unknown as {
      config: { set: (value: unknown) => void };
      capturePaymentNotice: (value: unknown) => void;
    };

    const config = {
      client: { displayName: 'The Memory Box', logoUrl: null },
      theme: {
        preset: 'VINTAGE_FILM',
        primaryColor: '#2f6868',
        accentColor: '#f5d27e',
        backgroundImageUrl: null,
        fontMode: 'serif',
      },
      session: {
        label: 'SM Manila',
        welcomeHeadline: 'Step Into The Memory Box',
        welcomeSubtitle: 'Welcome',
      },
      booth: { id: 'booth-id', state: 'WELCOME' },
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
      paymentOptions: [{ method: 'CASH', label: 'Cash', runtimeEnabled: true }],
      recentTransaction: {
        id: 'transaction-id',
        status: 'CANCELLED',
        transactionType: 'SESSION_PURCHASE',
        occurredAt: new Date().toISOString(),
        reason: 'The cashier cancelled this request.',
      },
    };
    app.config.set(config);
    app.capturePaymentNotice(config);
    fixture.detectChanges();
    await fixture.whenStable();

    const compiled = fixture.nativeElement as HTMLElement;

    expect(compiled.textContent).toContain('Payment request cancelled');
    expect(compiled.textContent).toContain('The cashier cancelled this request.');
    expect(compiled.textContent).toContain('Touch To Start');
  });

  it('should render a payment failed notice over the welcome screen from recent booth config', async () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance as unknown as {
      config: { set: (value: unknown) => void };
      capturePaymentNotice: (value: unknown) => void;
    };

    const config = {
      client: { displayName: 'The Memory Box', logoUrl: null },
      theme: {
        preset: 'CLEAN_MODERN',
        primaryColor: '#155eef',
        accentColor: '#111827',
        backgroundImageUrl: null,
        fontMode: 'sans',
      },
      session: {
        label: 'SM Manila',
        welcomeHeadline: 'Step Into The Memory Box',
        welcomeSubtitle: 'Welcome',
      },
      booth: { id: 'booth-id', state: 'WELCOME' },
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
      paymentOptions: [{ method: 'CASH', label: 'Cash', runtimeEnabled: true }],
      recentTransaction: {
        id: 'transaction-id',
        status: 'PAYMENT_FAILED',
        transactionType: 'SESSION_PURCHASE',
        occurredAt: new Date().toISOString(),
        reason: null,
      },
    };
    app.config.set(config);
    app.capturePaymentNotice(config);
    fixture.detectChanges();
    await fixture.whenStable();

    const compiled = fixture.nativeElement as HTMLElement;

    expect(compiled.textContent).toContain('Payment failed');
    expect(compiled.textContent).toContain('Payment could not be completed.');
    expect(compiled.textContent).toContain('Start Session');
  });

  it('should auto-dismiss the payment notice after five seconds', () => {
    vi.useFakeTimers();

    try {
      const fixture = TestBed.createComponent(App);
      const app = fixture.componentInstance as unknown as {
        config: { set: (value: unknown) => void };
        capturePaymentNotice: (value: unknown) => void;
      };
      const config = {
        client: { displayName: 'The Memory Box', logoUrl: null },
        theme: {
          preset: 'VINTAGE_FILM',
          primaryColor: '#2f6868',
          accentColor: '#f5d27e',
          backgroundImageUrl: null,
          fontMode: 'serif',
        },
        session: {
          label: 'SM Manila',
          welcomeHeadline: 'Step Into The Memory Box',
          welcomeSubtitle: 'Welcome',
        },
        booth: { id: 'booth-id', state: 'WELCOME' },
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
        paymentOptions: [{ method: 'CASH', label: 'Cash', runtimeEnabled: true }],
        recentTransaction: {
          id: 'transaction-id',
          status: 'CANCELLED',
          transactionType: 'SESSION_PURCHASE',
          occurredAt: new Date().toISOString(),
          reason: 'The cashier cancelled this request.',
        },
      };

      app.config.set(config);
      app.capturePaymentNotice(config);
      fixture.detectChanges();

      expect((fixture.nativeElement as HTMLElement).textContent).toContain('Payment request cancelled');

      vi.advanceTimersByTime(5000);
      fixture.detectChanges();

      expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('Payment request cancelled');
    } finally {
      vi.useRealTimers();
    }
  });
});
