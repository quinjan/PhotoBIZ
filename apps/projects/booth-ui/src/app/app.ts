import { CommonModule } from '@angular/common';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterOutlet } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { firstValueFrom, interval } from 'rxjs';

type BoothConfig = {
  readonly client: { displayName: string; logoUrl: string | null };
  readonly theme: {
    preset: string;
    primaryColor: string;
    accentColor: string;
    backgroundImageUrl: string | null;
    fontMode: string;
  };
  readonly session: { label: string; welcomeHeadline: string; welcomeSubtitle: string };
  readonly booth: { id: string; state: string };
  readonly activeOffer: {
    id: string;
    name: string;
    type: string;
    priceCents: number;
    currency: string;
    includedPrintEntitlement: string;
    allowsExtraPrintAddOn: boolean;
    extraPrintPriceCents: number | null;
  } | null;
  readonly paymentOptions: readonly { method: string; label: string; runtimeEnabled: boolean }[];
};

type BoothTransaction = {
  readonly id: string;
  readonly status: string;
};

type ScreenState =
  | 'connect'
  | 'offline'
  | 'unavailable'
  | 'offer'
  | 'payment'
  | 'waiting'
  | 'approved'
  | 'session'
  | 'completed'
  | 'expired'
  | 'error';

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

  protected readonly kioskToken = signal(localStorage.getItem('photobiz.kioskToken') ?? '');
  protected readonly config = signal<BoothConfig | null>(null);
  protected readonly transaction = signal<BoothTransaction | null>(null);
  protected readonly error = signal('');
  protected readonly loading = signal(false);
  protected readonly screen = computed<ScreenState>(() => {
    const config = this.config();

    if (!config) {
      return 'connect';
    }

    if (config.booth.state === 'OFFLINE') {
      return 'offline';
    }

    if (!config.activeOffer || config.paymentOptions.length === 0) {
      return 'unavailable';
    }

    if (config.booth.state === 'OFFER_CONFIRMED' || this.transaction()?.status === 'CREATED') {
      return 'payment';
    }

    if (config.booth.state === 'PAYMENT_PENDING') {
      return 'waiting';
    }

    if (config.booth.state === 'PAID' || config.booth.state === 'STARTING_LUMABOOTH') {
      return 'approved';
    }

    if (
      config.booth.state === 'IN_LUMABOOTH_SESSION' ||
      config.booth.state === 'PRINTING_OR_SHARING'
    ) {
      return 'session';
    }

    if (config.booth.state === 'ERROR') {
      return 'error';
    }

    if (config.booth.state === 'COMPLETED') {
      return 'completed';
    }

    if (config.booth.state === 'RETURNING_TO_WELCOME') {
      return 'expired';
    }

    return 'offer';
  });
  protected readonly cashOption = computed(
    () =>
      this.config()?.paymentOptions.find(
        (option) => option.method === 'CASH' && option.runtimeEnabled,
      ) ?? null,
  );

  constructor() {
    interval(3000)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        if (this.kioskToken()) {
          this.loadConfig();
        }
      });
  }

  protected async connect(): Promise<void> {
    localStorage.setItem('photobiz.kioskToken', this.kioskToken());
    await this.loadConfig();
  }

  protected async confirmOffer(): Promise<void> {
    await this.run(async () => {
      const transaction = await firstValueFrom(
        this.http.post<BoothTransaction>(
          `${App.apiBaseUrl}/api/booth-ui/transactions`,
          {},
          { headers: this.headers() },
        ),
      );

      this.transaction.set(transaction);
      await this.loadConfig();
    }, 'Could not confirm this offer.');
  }

  protected async chooseCash(): Promise<void> {
    const transaction = this.transaction();

    if (!transaction) {
      return;
    }

    await this.run(async () => {
      const updated = await firstValueFrom(
        this.http.post<BoothTransaction>(
          `${App.apiBaseUrl}/api/booth-ui/transactions/${transaction.id}/payment-method`,
          { method: 'CASH' },
          { headers: this.headers() },
        ),
      );

      this.transaction.set(updated);
      await this.loadConfig();
    }, 'Could not select cash payment.');
  }

  protected async loadConfig(): Promise<void> {
    await this.run(async () => {
      const config = await firstValueFrom(
        this.http.get<BoothConfig>(`${App.apiBaseUrl}/api/booth-ui/config`, {
          headers: this.headers(),
        }),
      );

      this.config.set(config);

      if (config.booth.state === 'WELCOME') {
        this.transaction.set(null);
      }
    }, 'Could not load booth config.');
  }

  protected formatMoney(cents: number): string {
    return `PHP ${(cents / 100).toLocaleString('en-PH', { maximumFractionDigits: 0 })}`;
  }

  protected screenTitle(): string {
    switch (this.screen()) {
      case 'connect':
        return 'Connect Booth';
      case 'offline':
        return 'Agent Offline';
      case 'unavailable':
        return 'Booth Unavailable';
      case 'payment':
        return 'Choose Payment';
      case 'waiting':
        return 'Cashier Approval';
      case 'approved':
        return 'Starting Session';
      case 'session':
        return 'Session In Progress';
      case 'completed':
        return 'Extra Prints';
      case 'expired':
        return 'Returning To Welcome';
      case 'error':
        return 'Recovery Needed';
      default:
        return this.config()?.session.welcomeHeadline ?? 'Welcome';
    }
  }

  protected screenMessage(): string {
    switch (this.screen()) {
      case 'connect':
        return 'Enter the booth kiosk token.';
      case 'offline':
        return 'Start the Windows Agent on the booth laptop.';
      case 'unavailable':
        return this.config()?.session.welcomeSubtitle ?? 'Ask staff to configure this booth.';
      case 'payment':
        return 'Pay cash at the counter after selecting the payment method.';
      case 'waiting':
        return 'Please wait while the cashier confirms payment.';
      case 'approved':
        return 'Payment confirmed. The booth session is starting.';
      case 'session':
        return 'Follow the booth operator screen.';
      case 'completed':
        return 'Need extra prints? Please go to the cashier.';
      case 'expired':
        return 'This session state has ended.';
      case 'error':
        return 'Ask the cashier for booth recovery.';
      default:
        return this.config()?.session.welcomeSubtitle ?? 'Review the active offer.';
    }
  }

  private headers(): HttpHeaders {
    return new HttpHeaders({ 'X-Kiosk-Token': this.kioskToken() });
  }

  private async run(operation: () => Promise<void>, fallbackMessage: string): Promise<void> {
    this.loading.set(true);
    this.error.set('');

    try {
      await operation();
    } catch (error) {
      this.error.set(error instanceof Error ? error.message : fallbackMessage);
    } finally {
      this.loading.set(false);
    }
  }
}
