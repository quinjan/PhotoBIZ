import { CommonModule } from '@angular/common';
import { HttpClient, HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterOutlet } from '@angular/router';
import {
  BoothStageAction,
  BoothStageComponent,
  BoothStageConfig,
  BoothStageScreenState,
} from '@photobiz/booth-stage';
import { firstValueFrom, interval } from 'rxjs';

type BoothTransaction = {
  readonly id: string;
  readonly status: string;
  readonly createdAt?: string;
};

type BoothUiCancelTrigger = 'BACK_BUTTON' | 'IDLE_TIMEOUT';

type RunOptions = {
  readonly errorMessage?: string;
  readonly showBusy?: boolean;
  readonly showError?: boolean;
};

@Component({
  selector: 'app-root',
  imports: [CommonModule, RouterOutlet, BoothStageComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  private static readonly apiBaseUrl = 'http://localhost:5082';
  private static readonly completedPromptDurationMs = 15_000;
  private static readonly completedPromptRetryMs = 1_000;
  private static readonly paymentIdleCancelMs = 30_000;
  private static readonly terminalAcknowledgeMs = 15_000;
  private readonly http = inject(HttpClient);
  private readonly destroyRef = inject(DestroyRef);
  private completedReturnTimer: number | null = null;
  private paymentCancelTimer: number | null = null;
  private paymentCancelTransactionId: string | null = null;
  private pendingCashExpiryRefreshTimer: number | null = null;
  private pendingCashExpiryTransactionId: string | null = null;
  private terminalAcknowledgeTimer: number | null = null;
  private terminalAcknowledgeTransactionId: string | null = null;
  private returnToWelcomeInFlight = false;
  private showReturnToWelcomeErrorOnFailure = false;
  private returnToWelcomeErrorShown = false;

  protected readonly kioskToken = signal(
    App.readTokenFromUrl() ?? localStorage.getItem('photobiz.kioskToken') ?? '',
  );
  protected readonly config = signal<BoothStageConfig | null>(null);
  protected readonly transaction = signal<BoothTransaction | null>(null);
  protected readonly error = signal('');
  private readonly busyRequests = signal(0);
  protected readonly loading = computed(() => this.busyRequests() > 0);
  protected readonly screen = computed<BoothStageScreenState>(() => {
    const config = this.config();

    if (!config) {
      return this.kioskToken() ? 'error' : 'connect';
    }

    const recentTransaction = config.recentTransaction;
    if (recentTransaction?.status === 'EXPIRED') {
      return 'expired';
    }

    if (recentTransaction?.status === 'CANCELLED') {
      return 'cancelled';
    }

    if (recentTransaction?.status === 'PAYMENT_FAILED') {
      return 'payment-failed';
    }

    if (config.booth.state === 'OFFLINE') {
      return 'offline';
    }

    const activeTransaction = config.activeTransaction ?? this.transaction();
    if (activeTransaction?.status === 'SESSION_FAILED') {
      return 'error';
    }

    if (activeTransaction?.status === 'CREATED') {
      return 'payment';
    }

    if (activeTransaction?.status === 'PENDING_CASH') {
      return 'waiting';
    }

    if (activeTransaction?.status === 'PAID' || activeTransaction?.status === 'STARTING_SESSION') {
      return 'approved';
    }

    if (activeTransaction?.status === 'IN_SESSION') {
      return 'session';
    }

    if (config.booth.state === 'ERROR') {
      return 'error';
    }

    if (config.booth.state === 'COMPLETED') {
      return 'completed';
    }

    if (!config.activeOffer || config.activeOffer.activationStatus === 'PENDING_PAYMENT') {
      return 'unavailable';
    }

    if (config.activeOffer.type === 'PER_SESSION' && config.paymentOptions.length === 0) {
      return 'unavailable';
    }

    if (config.booth.state === 'OFFER_CONFIRMED') {
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

    return 'offer';
  });

  constructor() {
    if (this.kioskToken()) {
      localStorage.setItem('photobiz.kioskToken', this.kioskToken());
      void this.loadConfig();
    }

    interval(3000)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        if (this.kioskToken()) {
          void this.loadConfig({
            errorMessage: 'Could not refresh booth config.',
            showBusy: false,
            showError: false,
          });
        }
      });

    this.destroyRef.onDestroy(() => {
      this.clearCompletedReturnTimer();
      this.clearPaymentCancelTimer();
      this.clearPendingCashExpiryRefreshTimer();
      this.clearTerminalAcknowledgeTimer();
    });
  }

  protected async connect(): Promise<void> {
    localStorage.setItem('photobiz.kioskToken', this.kioskToken());
    await this.loadConfig();
  }

  protected async confirmOffer(): Promise<void> {
    await this.run(
      async () => {
        const transaction = await firstValueFrom(
          this.http.post<BoothTransaction>(
            `${App.apiBaseUrl}/api/booth-ui/transactions`,
            {},
            { headers: this.headers() },
          ),
        );

        this.transaction.set(transaction);
        await this.loadConfig({ showBusy: false });
      },
      { errorMessage: 'Could not confirm this offer.' },
    );
  }

  protected async chooseCash(): Promise<void> {
    const transaction = this.currentTransaction();

    if (!transaction) {
      this.error.set('Could not select cash payment. Please start again.');
      return;
    }

    await this.run(
      async () => {
        const updated = await firstValueFrom(
          this.http.post<BoothTransaction>(
            `${App.apiBaseUrl}/api/booth-ui/transactions/${transaction.id}/payment-method`,
            { method: 'CASH' },
            { headers: this.headers() },
          ),
        );

        this.transaction.set(updated);
        await this.loadConfig({ showBusy: false });
      },
      { errorMessage: 'Could not select cash payment.' },
    );
  }

  protected async loadConfig(options: RunOptions = {}): Promise<void> {
    await this.run(() => this.loadConfigCore(), {
      errorMessage: 'Could not load booth config.',
      ...options,
    });
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
        case 'cancel-transaction':
          await this.cancelActiveTransaction(true, 'BACK_BUTTON');
          break;
        case 'acknowledge-recent':
          await this.acknowledgeRecentTransaction(true);
          break;
        case 'return-welcome':
          await this.returnToWelcome(true);
          break;
        case 'refresh':
          await this.loadConfig();
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
    this.beginBusy();
    if (showErrorOnFailure) {
      this.error.set('');
      this.returnToWelcomeErrorShown = false;
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
      if (this.showReturnToWelcomeErrorOnFailure && !this.returnToWelcomeErrorShown) {
        this.fail(
          'Could not return to welcome. Please wait or ask the cashier to return the booth to welcome.',
        );
        this.returnToWelcomeErrorShown = true;
      }
    } finally {
      if (commandSucceeded) {
        this.showReturnToWelcomeErrorOnFailure = false;
        this.returnToWelcomeErrorShown = false;
        this.error.set('');
        await this.loadConfig({ showBusy: false, showError: false });
      } else if (this.config()?.booth.state === 'COMPLETED') {
        this.completedReturnTimer = window.setTimeout(() => {
          this.completedReturnTimer = null;
          void this.returnToWelcome();
        }, App.completedPromptRetryMs);
      }

      this.endBusy();
      this.returnToWelcomeInFlight = false;
    }
  }

  private async cancelActiveTransaction(
    showErrorOnFailure: boolean,
    trigger: BoothUiCancelTrigger,
  ): Promise<void> {
    const transaction = this.currentTransaction();

    if (!transaction) {
      if (showErrorOnFailure) {
        await this.loadConfig();
      }
      return;
    }

    await this.run(
      async () => {
        await firstValueFrom(
          this.http.post(
            `${App.apiBaseUrl}/api/booth-ui/transactions/${transaction.id}/cancel`,
            { trigger },
            { headers: this.headers() },
          ),
        );
        this.transaction.set(null);
        await this.loadConfig({ showBusy: false, showError: false });
      },
      {
        errorMessage: 'Could not cancel this request.',
        showBusy: showErrorOnFailure,
        showError: showErrorOnFailure,
      },
    );
  }

  private async acknowledgeRecentTransaction(showErrorOnFailure: boolean): Promise<void> {
    const recentTransaction = this.config()?.recentTransaction;

    if (!recentTransaction) {
      if (showErrorOnFailure) {
        await this.loadConfig();
      }
      return;
    }

    await this.run(
      async () => {
        await firstValueFrom(
          this.http.post(
            `${App.apiBaseUrl}/api/booth-ui/recent-transactions/${recentTransaction.id}/acknowledge`,
            {},
            { headers: this.headers() },
          ),
        );
        await this.loadConfig({ showBusy: false, showError: false });
      },
      {
        errorMessage: 'Could not clear this status.',
        showBusy: showErrorOnFailure,
        showError: showErrorOnFailure,
      },
    );
  }

  private async loadConfigCore(): Promise<void> {
    const config = await firstValueFrom(
      this.http.get<BoothStageConfig>(`${App.apiBaseUrl}/api/booth-ui/config`, {
        headers: this.headers(),
      }),
    );

    this.config.set(config);
    this.transaction.set(
      config.activeTransaction
        ? { id: config.activeTransaction.id, status: config.activeTransaction.status }
        : null,
    );
    this.updateCompletedReturnTimer(config);
    this.updatePaymentCancelTimer(config);
    this.updatePendingCashExpiryRefreshTimer(config);
    this.updateTerminalAcknowledgeTimer(config);
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

  private updatePaymentCancelTimer(config: BoothStageConfig): void {
    const activeTransaction = config.activeTransaction;

    if (!activeTransaction || activeTransaction.status !== 'CREATED') {
      this.clearPaymentCancelTimer();
      return;
    }

    if (
      this.paymentCancelTimer !== null &&
      this.paymentCancelTransactionId === activeTransaction.id
    ) {
      return;
    }

    this.clearPaymentCancelTimer();
    this.paymentCancelTransactionId = activeTransaction.id;
    const remainingMs = App.getPaymentIdleRemainingMs(activeTransaction);
    this.paymentCancelTimer = window.setTimeout(() => {
      this.paymentCancelTimer = null;
      this.paymentCancelTransactionId = null;
      void this.cancelActiveTransaction(false, 'IDLE_TIMEOUT');
    }, remainingMs);
  }

  private clearPaymentCancelTimer(): void {
    if (this.paymentCancelTimer !== null) {
      window.clearTimeout(this.paymentCancelTimer);
      this.paymentCancelTimer = null;
    }

    this.paymentCancelTransactionId = null;
  }

  private updatePendingCashExpiryRefreshTimer(config: BoothStageConfig): void {
    const activeTransaction = config.activeTransaction;

    if (!activeTransaction || activeTransaction.status !== 'PENDING_CASH') {
      this.clearPendingCashExpiryRefreshTimer();
      return;
    }

    if (
      this.pendingCashExpiryRefreshTimer !== null &&
      this.pendingCashExpiryTransactionId === activeTransaction.id
    ) {
      return;
    }

    this.clearPendingCashExpiryRefreshTimer();
    this.pendingCashExpiryTransactionId = activeTransaction.id;
    const expiresAtMs = Date.parse(activeTransaction.expiresAt);
    const remainingMs = Number.isNaN(expiresAtMs) ? 0 : Math.max(0, expiresAtMs - Date.now());
    this.pendingCashExpiryRefreshTimer = window.setTimeout(() => {
      this.pendingCashExpiryRefreshTimer = null;
      this.pendingCashExpiryTransactionId = null;
      void this.loadConfig({
        errorMessage: 'Could not refresh booth config.',
        showBusy: false,
        showError: false,
      });
    }, remainingMs);
  }

  private clearPendingCashExpiryRefreshTimer(): void {
    if (this.pendingCashExpiryRefreshTimer !== null) {
      window.clearTimeout(this.pendingCashExpiryRefreshTimer);
      this.pendingCashExpiryRefreshTimer = null;
    }

    this.pendingCashExpiryTransactionId = null;
  }

  private updateTerminalAcknowledgeTimer(config: BoothStageConfig): void {
    const recentTransaction = config.recentTransaction;

    if (!App.isAcknowledgeableRecentTransaction(recentTransaction)) {
      this.clearTerminalAcknowledgeTimer();
      return;
    }

    if (
      this.terminalAcknowledgeTimer !== null &&
      this.terminalAcknowledgeTransactionId === recentTransaction.id
    ) {
      return;
    }

    this.clearTerminalAcknowledgeTimer();
    this.terminalAcknowledgeTransactionId = recentTransaction.id;
    this.terminalAcknowledgeTimer = window.setTimeout(() => {
      this.terminalAcknowledgeTimer = null;
      this.terminalAcknowledgeTransactionId = null;
      void this.acknowledgeRecentTransaction(false);
    }, App.terminalAcknowledgeMs);
  }

  private clearTerminalAcknowledgeTimer(): void {
    if (this.terminalAcknowledgeTimer !== null) {
      window.clearTimeout(this.terminalAcknowledgeTimer);
      this.terminalAcknowledgeTimer = null;
    }

    this.terminalAcknowledgeTransactionId = null;
  }

  private headers(): HttpHeaders {
    return new HttpHeaders({ 'X-Kiosk-Token': this.kioskToken() });
  }

  private currentTransaction():
    | BoothTransaction
    | NonNullable<BoothStageConfig['activeTransaction']>
    | null {
    return this.config()?.activeTransaction ?? this.transaction();
  }

  private static isAcknowledgeableRecentTransaction(
    transaction: BoothStageConfig['recentTransaction'] | null | undefined,
  ): transaction is NonNullable<BoothStageConfig['recentTransaction']> {
    return (
      transaction?.status === 'EXPIRED' ||
      transaction?.status === 'CANCELLED' ||
      transaction?.status === 'PAYMENT_FAILED'
    );
  }

  private static getPaymentIdleRemainingMs(
    transaction: NonNullable<BoothStageConfig['activeTransaction']>,
  ): number {
    if (!transaction.createdAt) {
      return App.paymentIdleCancelMs;
    }

    const createdAtMs = Date.parse(transaction.createdAt);

    if (Number.isNaN(createdAtMs)) {
      return App.paymentIdleCancelMs;
    }

    return Math.max(0, createdAtMs + App.paymentIdleCancelMs - Date.now());
  }

  private async run(operation: () => Promise<void>, options: RunOptions = {}): Promise<boolean> {
    const showBusy = options.showBusy ?? true;
    if (showBusy && this.loading()) {
      return false;
    }

    if (showBusy) {
      this.beginBusy();
    }
    this.error.set('');

    try {
      await operation();
      return true;
    } catch (error) {
      if (options.showError !== false) {
        this.error.set(
          this.getRequestErrorMessage(error, options.errorMessage ?? 'Request failed.'),
        );
      }
      return false;
    } finally {
      if (showBusy) {
        this.endBusy();
      }
    }
  }

  private fail(message: string): void {
    this.error.set(message);
  }

  private beginBusy(): void {
    this.busyRequests.update((count) => count + 1);
  }

  private endBusy(): void {
    this.busyRequests.update((count) => Math.max(0, count - 1));
  }

  private getRequestErrorMessage(error: unknown, fallback: string): string {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 0) {
        return `${fallback} The API is unreachable.`;
      }

      const apiMessage = this.getApiErrorMessage(error.error);
      return apiMessage ? `${fallback} ${apiMessage}` : fallback;
    }

    if (error instanceof Error && error.message) {
      return `${fallback} ${error.message}`;
    }

    return fallback;
  }

  private getApiErrorMessage(errorBody: unknown): string {
    if (typeof errorBody === 'string') {
      return errorBody;
    }

    if (!errorBody || typeof errorBody !== 'object') {
      return '';
    }

    const body = errorBody as {
      detail?: unknown;
      errors?: Record<string, unknown>;
      title?: unknown;
    };
    if (typeof body.detail === 'string') {
      return body.detail;
    }

    if (body.errors) {
      for (const value of Object.values(body.errors)) {
        if (Array.isArray(value) && typeof value[0] === 'string') {
          return value[0];
        }

        if (typeof value === 'string') {
          return value;
        }
      }
    }

    return typeof body.title === 'string' ? body.title : '';
  }

  private static readTokenFromUrl(): string | null {
    const params = new URLSearchParams(window.location.search);
    const queryToken = params.get('token');

    if (queryToken) {
      return queryToken;
    }

    const firstPathSegment = window.location.pathname.split('/').filter(Boolean)[0];

    return firstPathSegment ? decodeURIComponent(firstPathSegment) : null;
  }
}
