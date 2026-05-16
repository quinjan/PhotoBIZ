import { CommonModule } from '@angular/common';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterOutlet } from '@angular/router';
import { firstValueFrom, interval } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

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
  } | null;
  readonly paymentOptions: readonly { method: string; label: string; runtimeEnabled: boolean }[];
};

type BoothTransaction = {
  readonly id: string;
  readonly status: string;
};

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

  protected async startSession(): Promise<void> {
    try {
      const transaction = await firstValueFrom(
        this.http.post<BoothTransaction>(
          `${App.apiBaseUrl}/api/booth-ui/transactions`,
          {},
          { headers: this.headers() },
        ),
      );

      if (!transaction) {
        return;
      }

      this.transaction.set(transaction);

      await firstValueFrom(
        this.http.post(
          `${App.apiBaseUrl}/api/booth-ui/transactions/${transaction.id}/payment-method`,
          { method: 'CASH' },
          { headers: this.headers() },
        ),
      );

      await this.loadConfig();
    } catch (error) {
      this.error.set(error instanceof Error ? error.message : 'Could not start kiosk transaction.');
    }
  }

  protected async loadConfig(): Promise<void> {
    try {
      const config = await firstValueFrom(
        this.http.get<BoothConfig>(`${App.apiBaseUrl}/api/booth-ui/config`, {
          headers: this.headers(),
        }),
      );

      if (config) {
        this.config.set(config);
        this.error.set('');
      }
    } catch (error) {
      this.error.set(error instanceof Error ? error.message : 'Could not load booth config.');
    }
  }

  protected stateMessage(): string {
    const state = this.config()?.booth.state;

    switch (state) {
      case 'OFFLINE':
        return 'Agent offline. Start the Windows Agent on the booth laptop before accepting a session.';
      case 'PAYMENT_PENDING':
        return 'Waiting for cashier cash approval.';
      case 'PAID':
      case 'STARTING_LUMABOOTH':
        return 'Payment approved. Starting the session.';
      case 'IN_LUMABOOTH_SESSION':
        return 'Session is in progress on the operator machine.';
      case 'ERROR':
        return 'The booth needs manual recovery from the cashier view.';
      default:
        return this.config()?.session.welcomeSubtitle ?? 'Connect a kiosk token to begin.';
    }
  }

  protected isStartDisabled(): boolean {
    const state = this.config()?.booth.state;

    return !this.config()?.activeOffer || state === 'OFFLINE' || state === 'PAYMENT_PENDING';
  }

  protected startButtonLabel(): string {
    const state = this.config()?.booth.state;

    if (state === 'OFFLINE') {
      return 'Agent Offline';
    }

    if (state === 'PAYMENT_PENDING') {
      return 'Waiting For Cashier';
    }

    return 'Start Session';
  }

  private headers(): HttpHeaders {
    return new HttpHeaders({ 'X-Kiosk-Token': this.kioskToken() });
  }
}
