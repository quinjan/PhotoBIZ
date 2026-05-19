import { CommonModule } from '@angular/common';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { BoothStageAction, BoothStageComponent, BoothStageConfig, BoothStageScreenState } from '@photobiz/booth-stage';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { firstValueFrom, interval } from 'rxjs';

type BoothTransaction = {
  readonly id: string;
  readonly status: string;
};

type PaymentNotice = {
  readonly transactionId: string;
  readonly title: string;
  readonly message: string;
  readonly expiresAt: number;
};

type DismissedPaymentNotice = {
  readonly transactionId: string;
  readonly expiresAt: number;
};

@Component({
  selector: 'app-root',
  imports: [CommonModule, RouterOutlet, BoothStageComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  private static readonly apiBaseUrl = 'http://localhost:5082';
  private static readonly paymentNoticeStoragePrefix = 'photobiz.paymentNotice.';
  private static readonly dismissedPaymentNoticeStoragePrefix = 'photobiz.dismissedPaymentNotice.';
  private static readonly paymentNoticeDurationMs = 5_000;
  private static readonly dismissedPaymentNoticeDurationMs = 5 * 60_000;
  private static readonly completedPromptDurationMs = 15_000;
  private static readonly completedPromptRetryMs = 1_000;
  private readonly http = inject(HttpClient);
  private readonly destroyRef = inject(DestroyRef);
  private paymentNoticeDismissTimer: number | null = null;
  private completedReturnTimer: number | null = null;
  private returnToWelcomeInFlight = false;
  private showReturnToWelcomeErrorOnFailure = false;

  protected readonly kioskToken = signal(
    App.readTokenFromUrl() ?? localStorage.getItem('photobiz.kioskToken') ?? '',
  );
  protected readonly config = signal<BoothStageConfig | null>(null);
  protected readonly transaction = signal<BoothTransaction | null>(null);
  protected readonly dismissedRecentTransactionId = signal<string | null>(
    App.readDismissedPaymentNoticeId(this.kioskToken()),
  );
  protected readonly storedPaymentNotice = signal<PaymentNotice | null>(
    App.readStoredPaymentNotice(this.kioskToken()),
  );
  protected readonly error = signal('');
  protected readonly loading = signal(false);
  protected readonly screen = computed<BoothStageScreenState>(() => {
    const config = this.config();
    const recentTransaction = config?.recentTransaction;
    const hasVisibleRecentTransaction =
      recentTransaction && recentTransaction.id !== this.dismissedRecentTransactionId();

    if (!config) {
      return this.kioskToken() ? 'error' : 'connect';
    }

    if (config.booth.state === 'OFFLINE') {
      return 'offline';
    }

    if (hasVisibleRecentTransaction && recentTransaction.status === 'EXPIRED') {
      return 'expired';
    }

    if (!config.activeOffer || config.activeOffer.activationStatus === 'PENDING_PAYMENT') {
      return 'unavailable';
    }

    if (config.activeOffer.type === 'PER_SESSION' && config.paymentOptions.length === 0) {
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
  protected readonly paymentNotice = computed<PaymentNotice | null>(() => {
    return this.storedPaymentNotice();
  });
  constructor() {
    const storedNotice = this.storedPaymentNotice();
    if (storedNotice) {
      this.schedulePaymentNoticeDismiss(storedNotice);
    }

    if (this.kioskToken()) {
      localStorage.setItem('photobiz.kioskToken', this.kioskToken());
      void this.loadConfig();
    }

    interval(3000)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        if (this.kioskToken()) {
          this.loadConfig();
        }
      });

    this.destroyRef.onDestroy(() => {
      if (this.paymentNoticeDismissTimer !== null) {
        window.clearTimeout(this.paymentNoticeDismissTimer);
      }

      this.clearCompletedReturnTimer();
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

      this.clearPaymentNotice();
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

  protected async loadConfig(suppressErrors = false): Promise<void> {
    if (suppressErrors) {
      try {
        await this.loadConfigCore();
      } catch {
        // Return-to-welcome is a kiosk recovery path; do not surface transient
        // refresh failures after the customer has chosen to leave the completed screen.
      }

      return;
    }

    await this.run(() => this.loadConfigCore(), 'Could not load booth config.');
  }

  protected handleStageAction(action: BoothStageAction): void {
    void (async () => {
      switch (action) {
        case 'connect':
          await this.connect();
          break;
        case 'confirm-offer':
          await this.confirmOffer();
          break;
        case 'cash':
          await this.chooseCash();
          break;
        case 'refresh':
          if (this.config()?.recentTransaction) {
            this.rememberDismissedPaymentNotice(this.config()?.recentTransaction?.id ?? '');
          }
          await this.loadConfig();
          break;
        case 'return-welcome':
          await this.returnToWelcome(true);
          break;
      }
    })();
  }

  private async returnToWelcome(showErrorOnFailure = false): Promise<void> {
    if (showErrorOnFailure) {
      this.showReturnToWelcomeErrorOnFailure = true;
    }

    if (this.returnToWelcomeInFlight) {
      return;
    }

    this.returnToWelcomeInFlight = true;
    this.clearCompletedReturnTimer();
    this.loading.set(true);
    if (showErrorOnFailure) {
      this.error.set('');
    }
    let commandSucceeded = false;

    try {
      await firstValueFrom(
        this.http.post(
          `${App.apiBaseUrl}/api/booth-ui/return-to-welcome`,
          {},
          { headers: this.headers() },
        ),
      );
      commandSucceeded = true;
    } catch {
      // Keep the customer-facing completed screen calm and retry. The screen should
      // not move to welcome until the backend accepts the return command.
      if (this.showReturnToWelcomeErrorOnFailure) {
        this.error.set('Could not return to welcome. Please wait or ask the cashier to return the booth to welcome.');
      }
    } finally {
      if (commandSucceeded) {
        this.showReturnToWelcomeErrorOnFailure = false;
        this.error.set('');
        this.transaction.set(null);
        this.applyBackendConfirmedWelcome();
        await this.loadConfig(true);
      } else if (this.config()?.booth.state === 'COMPLETED') {
        this.completedReturnTimer = window.setTimeout(() => {
          this.completedReturnTimer = null;
          void this.returnToWelcome();
        }, App.completedPromptRetryMs);
      }

      this.loading.set(false);
      this.returnToWelcomeInFlight = false;
    }
  }

  private async loadConfigCore(): Promise<void> {
    const config = await firstValueFrom(
      this.http.get<BoothStageConfig>(`${App.apiBaseUrl}/api/booth-ui/config`, {
        headers: this.headers(),
      }),
    );

    this.config.set(config);
    this.capturePaymentNotice(config);
    this.updateCompletedReturnTimer(config);

    if (config.booth.state === 'WELCOME') {
      this.transaction.set(null);
    }
  }

  private applyBackendConfirmedWelcome(): void {
    const currentConfig = this.config();

    if (currentConfig?.booth.state === 'COMPLETED') {
      this.config.set({
        ...currentConfig,
        booth: { ...currentConfig.booth, state: 'WELCOME' },
      });
    }
  }

  private updateCompletedReturnTimer(config: BoothStageConfig): void {
    if (config.booth.state !== 'COMPLETED') {
      this.clearCompletedReturnTimer();
      return;
    }

    if (this.completedReturnTimer !== null) {
      return;
    }

    this.completedReturnTimer = window.setTimeout(() => {
      this.completedReturnTimer = null;
      void this.returnToWelcome();
    }, App.completedPromptDurationMs);
  }

  private clearCompletedReturnTimer(): void {
    if (this.completedReturnTimer !== null) {
      window.clearTimeout(this.completedReturnTimer);
      this.completedReturnTimer = null;
    }
  }

  protected dismissPaymentNotice(transactionId: string): void {
    this.rememberDismissedPaymentNotice(transactionId);
    this.clearPaymentNotice();
  }

  private headers(): HttpHeaders {
    return new HttpHeaders({ 'X-Kiosk-Token': this.kioskToken() });
  }

  private capturePaymentNotice(config: BoothStageConfig): void {
    const recentTransaction = config.recentTransaction;

    if (!recentTransaction || this.isPaymentNoticeDismissed(recentTransaction.id)) {
      return;
    }

    if (this.storedPaymentNotice()?.transactionId === recentTransaction.id) {
      return;
    }

    const notice = App.toPaymentNotice(recentTransaction);
    if (!notice) {
      return;
    }

    this.storedPaymentNotice.set(notice);
    localStorage.setItem(App.paymentNoticeStorageKey(this.kioskToken()), JSON.stringify(notice));
    this.schedulePaymentNoticeDismiss(notice);
  }

  private clearPaymentNotice(): void {
    if (this.paymentNoticeDismissTimer !== null) {
      window.clearTimeout(this.paymentNoticeDismissTimer);
      this.paymentNoticeDismissTimer = null;
    }

    this.storedPaymentNotice.set(null);
    localStorage.removeItem(App.paymentNoticeStorageKey(this.kioskToken()));
  }

  private schedulePaymentNoticeDismiss(notice: PaymentNotice): void {
    if (this.paymentNoticeDismissTimer !== null) {
      window.clearTimeout(this.paymentNoticeDismissTimer);
    }

    const remainingMs = Math.max(0, notice.expiresAt - Date.now());
    this.paymentNoticeDismissTimer = window.setTimeout(() => {
      this.rememberDismissedPaymentNotice(notice.transactionId);
      this.clearPaymentNotice();
    }, remainingMs);
  }

  private isPaymentNoticeDismissed(transactionId: string): boolean {
    return this.dismissedRecentTransactionId() === transactionId ||
      App.readDismissedPaymentNoticeId(this.kioskToken()) === transactionId;
  }

  private rememberDismissedPaymentNotice(transactionId: string): void {
    this.dismissedRecentTransactionId.set(transactionId);
    localStorage.setItem(
      App.dismissedPaymentNoticeStorageKey(this.kioskToken()),
      JSON.stringify({
        transactionId,
        expiresAt: Date.now() + App.dismissedPaymentNoticeDurationMs,
      } satisfies DismissedPaymentNotice),
    );
  }

  private static toPaymentNotice(recentTransaction: NonNullable<BoothStageConfig['recentTransaction']>): PaymentNotice | null {
    switch (recentTransaction.status) {
      case 'CANCELLED':
        return {
          transactionId: recentTransaction.id,
          title: 'Payment request cancelled',
          message:
            recentTransaction.reason ??
            'The cashier cancelled or rejected this payment request. Please start again when ready.',
          expiresAt: Date.now() + App.paymentNoticeDurationMs,
        };
      case 'PAYMENT_FAILED':
        return {
          transactionId: recentTransaction.id,
          title: 'Payment failed',
          message:
            recentTransaction.reason ??
            'Payment could not be completed. Please choose another method or ask the cashier.',
          expiresAt: Date.now() + App.paymentNoticeDurationMs,
        };
      default:
        return null;
    }
  }

  private static readStoredPaymentNotice(kioskToken: string): PaymentNotice | null {
    const rawNotice = localStorage.getItem(App.paymentNoticeStorageKey(kioskToken));

    if (!rawNotice) {
      return null;
    }

    try {
      const notice = JSON.parse(rawNotice) as PaymentNotice;
      if (!notice.transactionId || !notice.title || !notice.message || notice.expiresAt <= Date.now()) {
        localStorage.removeItem(App.paymentNoticeStorageKey(kioskToken));
        return null;
      }

      return notice;
    } catch {
      return null;
    }
  }

  private static paymentNoticeStorageKey(kioskToken: string): string {
    return `${App.paymentNoticeStoragePrefix}${kioskToken || 'unpaired'}`;
  }

  private static readDismissedPaymentNoticeId(kioskToken: string): string | null {
    const rawNotice = localStorage.getItem(App.dismissedPaymentNoticeStorageKey(kioskToken));

    if (!rawNotice) {
      return null;
    }

    try {
      const notice = JSON.parse(rawNotice) as DismissedPaymentNotice;
      if (!notice.transactionId || notice.expiresAt <= Date.now()) {
        localStorage.removeItem(App.dismissedPaymentNoticeStorageKey(kioskToken));
        return null;
      }

      return notice.transactionId;
    } catch {
      return null;
    }
  }

  private static dismissedPaymentNoticeStorageKey(kioskToken: string): string {
    return `${App.dismissedPaymentNoticeStoragePrefix}${kioskToken || 'unpaired'}`;
  }

  private static readTokenFromUrl(): string | null {
    const params = new URLSearchParams(window.location.search);
    const queryToken = params.get('token');

    if (queryToken) {
      return queryToken;
    }

    const firstPathSegment = window.location.pathname
      .split('/')
      .filter(Boolean)[0];

    return firstPathSegment ? decodeURIComponent(firstPathSegment) : null;
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
