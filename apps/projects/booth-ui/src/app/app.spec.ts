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

  it('should show agent offline and disable session start when booth is offline', async () => {
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
    const startButton = compiled.querySelector('.welcome-panel button') as HTMLButtonElement;

    expect(compiled.textContent).toContain('Agent offline');
    expect(startButton.disabled).toBe(true);
    expect(startButton.textContent).toContain('Agent Offline');
  });
});
