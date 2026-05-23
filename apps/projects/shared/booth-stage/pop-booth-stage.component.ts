import { CommonModule } from '@angular/common';
import {
  Component,
  DestroyRef,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
} from '@angular/core';

import {
  formatStageMoney,
  stageBackgroundImage,
  stageCashOption,
  stageEyebrow,
  stageMessage,
  stageTitle,
  shouldShowStageOfferDetails,
} from './booth-stage.helpers';
import { BoothStageAction, BoothStageConfig, BoothStageScreenState } from './booth-stage.models';

@Component({
  selector: 'photobiz-pop-booth-stage',
  imports: [CommonModule],
  templateUrl: './pop-booth-stage.component.html',
  styleUrl: './pop-booth-stage.component.scss',
})
export class PopBoothStageComponent {
  private readonly paymentIdleTimeoutMs = 30_000;
  private readonly pendingCashTimeoutMs = 60_000;
  private readonly autoReturnTimeoutMs = 15_000;
  private readonly destroyRef = inject(DestroyRef);
  private readonly now = signal(Date.now());
  private readonly autoReturnKey = signal('');
  private readonly autoReturnStartedAt = signal(Date.now());
  protected readonly backConfirmOpen = signal(false);
  protected readonly paymentConfirmOpen = signal(false);
  readonly config = input<BoothStageConfig | null>(null);
  readonly screen = input<BoothStageScreenState>('connect');
  readonly loading = input(false);
  readonly action = output<BoothStageAction>();

  protected readonly title = computed(() => this.popTitle());
  protected readonly message = computed(() => this.popMessage());
  protected readonly eyebrow = computed(() => stageEyebrow(this.config()));
  protected readonly cashOption = computed(() => stageCashOption(this.config()));
  protected readonly backgroundImage = computed(() => stageBackgroundImage(this.config()));
  protected readonly activeTransaction = computed(() => this.config()?.activeTransaction ?? null);
  protected readonly showOfferDetails = computed(() => shouldShowStageOfferDetails(this.config()));
  protected readonly clientName = computed(() => this.config()?.client?.displayName || 'Pixel Pop Studio');
  protected readonly boothLocationLine = computed(() => {
    const booth = this.config()?.booth;
    const location = booth?.locationName?.trim();
    const code = booth?.code?.trim();
    const label = this.eyebrow();

    if (location && code && label) {
      return `${location} | ${code} | ${label}`;
    }

    if (location && code) {
      return `${location} | ${code}`;
    }

    return location || code || booth?.name?.trim() || label || 'Booth';
  });
  protected readonly friendlyBoothState = computed(() =>
    this.humanizeBoothState(this.config()?.booth?.state),
  );
  protected readonly stateClass = computed(() => `state-${this.screen()}`);
  protected readonly toneClass = computed(() => {
    switch (this.screen()) {
      case 'approved':
      case 'completed':
        return 'tone-ok';
      case 'offline':
      case 'unavailable':
      case 'expired':
      case 'cancelled':
      case 'payment-failed':
      case 'error':
        return 'tone-danger';
      default:
        return 'tone-normal';
    }
  });
  protected readonly isTicketScreen = computed(() =>
    ['waiting', 'expired', 'cancelled', 'payment-failed'].includes(this.screen()),
  );
  protected readonly isFrameScreen = computed(() => ['approved', 'session'].includes(this.screen()));
  protected readonly shouldShowBackButton = computed(() =>
    this.screen() === 'payment' || this.screen() === 'waiting',
  );
  protected readonly ticketEyebrow = computed(() => {
    switch (this.screen()) {
      case 'waiting':
        return 'Cashier Approval';
      case 'expired':
        return 'Expired';
      case 'cancelled':
        return 'Cancelled';
      case 'payment-failed':
        return 'Payment Failed';
      default:
        return this.eyebrow();
    }
  });
  protected readonly frameChip = computed(() => {
    switch (this.screen()) {
      case 'completed':
        return 'Fresh Shots';
      case 'offline':
      case 'unavailable':
      case 'error':
        return 'Staff Check';
      case 'expired':
      case 'cancelled':
      case 'payment-failed':
        return 'Status';
      case 'payment':
        return 'Pick One';
      case 'waiting':
        return 'Cashier POS';
      default:
        return 'Photo Ready';
    }
  });
  protected readonly autoReturnSeconds = computed(() => {
    if (this.screen() === 'payment') {
      return this.paymentAutoReturnSeconds();
    }

    if (
      this.screen() === 'completed' ||
      this.screen() === 'expired' ||
      this.screen() === 'cancelled' ||
      this.screen() === 'payment-failed'
    ) {
      return this.localAutoReturnSeconds();
    }

    return null;
  });
  private readonly paymentAutoReturnSeconds = computed(() => {
    const createdAt = this.activeTransaction()?.createdAt;

    if (!createdAt) {
      return 30;
    }

    const createdAtMs = Date.parse(createdAt);

    if (Number.isNaN(createdAtMs)) {
      return 30;
    }

    const remainingMs = createdAtMs + this.paymentIdleTimeoutMs - this.now();
    return Math.max(0, Math.ceil(remainingMs / 1000));
  });
  private readonly localAutoReturnSeconds = computed(() => {
    const remainingMs = this.autoReturnStartedAt() + this.autoReturnTimeoutMs - this.now();
    return Math.max(0, Math.ceil(remainingMs / 1000));
  });
  protected readonly progress = computed(() => {
    switch (this.screen()) {
      case 'connect':
        return '15%';
      case 'offline':
      case 'expired':
      case 'cancelled':
      case 'payment-failed':
        return '0%';
      case 'unavailable':
        return '20%';
      case 'payment':
        return '35%';
      case 'waiting':
        return '55%';
      case 'approved':
        return '75%';
      case 'session':
        return '88%';
      case 'completed':
      case 'offer':
        return '100%';
      case 'error':
        return '25%';
      default:
        return '50%';
    }
  });
  protected readonly transactionNumber = computed(
    () => this.activeTransaction()?.transactionNumber ?? 'Pending request',
  );
  protected readonly countdownRemainingMs = computed(() => {
    const expiresAt = this.activeTransaction()?.expiresAt;

    if (!expiresAt) {
      return null;
    }

    const expirationMs = Date.parse(expiresAt);

    if (Number.isNaN(expirationMs)) {
      return null;
    }

    return Math.max(0, expirationMs - this.now());
  });
  protected readonly timeoutProgress = computed(() => {
    const remainingMs = this.countdownRemainingMs();

    if (remainingMs === null) {
      return '0%';
    }

    const percent = Math.min(100, Math.max(0, (remainingMs / this.pendingCashTimeoutMs) * 100));
    return `${percent.toFixed(2)}%`;
  });
  protected readonly footerLeft = computed(() => {
    const screen = this.screen();
    const offer = this.config()?.activeOffer;

    switch (screen) {
      case 'connect':
        return 'Setup only';
      case 'offline':
        return 'No fresh heartbeat';
      case 'unavailable':
        return offer?.activationStatus === 'PENDING_PAYMENT'
          ? 'Awaiting activation'
          : 'Package not active';
      case 'offer':
        return this.cashOption() ? 'Cash payment available' : '';
      case 'payment':
        return offer ? `${offer.name} | ${offer.includedPrintEntitlement}` : '';
      case 'waiting':
        return this.transactionNumber();
      case 'completed':
        return 'Auto return';
      case 'expired':
      case 'cancelled':
      case 'payment-failed':
        return 'Recent transaction';
      case 'error':
        return 'Backend recovery';
      default:
        return '';
    }
  });
  protected readonly footerRight = computed(() => {
    const offer = this.config()?.activeOffer;

    switch (this.screen()) {
      case 'connect':
        return 'Agent opens kiosk mode';
      case 'offline':
        return 'Sessions are blocked';
      case 'unavailable':
        return 'No checkout shown';
      case 'offer':
        return offer?.allowsExtraPrintAddOn ? 'Extra prints after session' : '';
      case 'payment':
        return 'Auto returns if idle';
      case 'waiting':
        return 'Cashier POS approval';
      case 'error':
        return 'Do not restart';
      default:
        return '';
    }
  });
  protected readonly countdown = computed(() => {
    const remainingMs = this.countdownRemainingMs();

    if (remainingMs === null) {
      return '';
    }

    const minutes = Math.floor(remainingMs / 60_000);
    const seconds = Math.floor((remainingMs % 60_000) / 1000);

    return `${minutes}:${seconds.toString().padStart(2, '0')}`;
  });
  protected readonly formatMoney = formatStageMoney;

  constructor() {
    effect(() => {
      const key = [
        this.screen(),
        this.config()?.booth.state ?? '',
        this.config()?.activeTransaction?.id ?? '',
        this.config()?.recentTransaction?.id ?? '',
      ].join('|');

      if (key !== this.autoReturnKey()) {
        untracked(() => {
          this.autoReturnKey.set(key);
          this.autoReturnStartedAt.set(Date.now());
        });
      }
    });

    const timer = window.setInterval(() => this.now.set(Date.now()), 1000);
    this.destroyRef.onDestroy(() => window.clearInterval(timer));
  }

  protected openBackConfirm(): void {
    this.backConfirmOpen.set(true);
  }

  protected closeBackConfirm(): void {
    this.backConfirmOpen.set(false);
  }

  protected confirmBack(): void {
    this.closeBackConfirm();
    this.action.emit('cancel-transaction');
  }

  protected openPaymentConfirm(): void {
    this.paymentConfirmOpen.set(true);
  }

  protected closePaymentConfirm(): void {
    this.paymentConfirmOpen.set(false);
  }

  protected confirmPayment(): void {
    this.closePaymentConfirm();
    this.action.emit('cash');
  }

  private popTitle(): string {
    switch (this.screen()) {
      case 'session':
        return 'Session Active';
      case 'error':
        return 'Call Cashier';
      default:
        return stageTitle(this.config(), this.screen());
    }
  }

  private popMessage(): string {
    switch (this.screen()) {
      case 'approved':
        return 'Payment confirmed. Follow the booth screen.';
      case 'session':
        return 'Follow LumaBooth.';
      case 'completed':
        return this.config()?.activeOffer?.type === 'PER_SESSION'
          ? 'Fresh shots, big energy.'
          : 'Your session is complete.';
      default:
        return stageMessage(this.config(), this.screen());
    }
  }

  private humanizeBoothState(state: string | null | undefined): string {
    switch ((state ?? '').toUpperCase()) {
      case 'NO_KIOSK_TOKEN':
        return 'Connect Booth';
      case 'OFFLINE':
        return 'Offline';
      case 'WELCOME':
        return 'Welcome';
      case 'OFFER_CONFIRMED':
        return 'Offer Confirmed';
      case 'PAYMENT_PENDING':
        return 'Payment Pending';
      case 'STARTING_LUMABOOTH':
        return 'Starting Session';
      case 'IN_LUMABOOTH_SESSION':
        return 'Session In Progress';
      case 'COMPLETED':
        return 'Completed';
      case 'ERROR':
        return 'Recovery Needed';
      case 'RECENT EXPIRED':
      case 'EXPIRED':
        return 'Request Expired';
      case 'RECENT CANCELLED':
      case 'CANCELLED':
        return 'Request Cancelled';
      case 'RECENT PAYMENT_FAILED':
      case 'PAYMENT_FAILED':
        return 'Payment Failed';
      default:
        return state ? this.titleCaseState(state) : 'Offline';
    }
  }

  private titleCaseState(state: string): string {
    return state
      .toLowerCase()
      .replaceAll('_', ' ')
      .replace(/\b\w/g, (letter) => letter.toUpperCase());
  }
}
