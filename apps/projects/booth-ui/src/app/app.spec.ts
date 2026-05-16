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

  it('should render the kiosk token field', async () => {
    const fixture = TestBed.createComponent(App);

    await fixture.whenStable();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Kiosk token');
  });

  it('should show agent offline retry state when booth is offline', async () => {
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
      booth: { id: 'booth-id', state: 'OFFLINE' },
      activeOffer: {
        id: 'offer-id',
        name: 'Per Session',
        type: 'PER_SESSION',
        priceCents: 25000,
        currency: 'PHP',
        includedPrintEntitlement: '2 pcs 6x2 or 1 pc 6x4',
        allowsExtraPrintAddOn: true,
      },
      paymentOptions: [{ method: 'CASH', label: 'Cash', runtimeEnabled: true }],
    });
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
});
