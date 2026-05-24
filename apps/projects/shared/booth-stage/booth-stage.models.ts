export type BoothStageScreenState =
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
  | 'cancelled'
  | 'payment-failed'
  | 'error';

export type BoothStageThemePreset = 'VINTAGE' | 'CLEAN_MODERN' | 'POP';

export type BoothStageConfig = {
  readonly client: { readonly displayName: string; readonly logoUrl: string | null };
  readonly theme: {
    readonly preset: string;
    readonly primaryColor: string;
    readonly accentColor: string;
    readonly backgroundImageUrl: string | null;
    readonly backgroundImageDataUrl?: string | null;
    readonly fontMode: string;
  };
  readonly session: {
    readonly label: string;
    readonly welcomeHeadline: string;
    readonly welcomeSubtitle: string;
    readonly completionThankYouMessage?: string;
  };
  readonly booth: {
    readonly id: string;
    readonly state: string;
    readonly name?: string;
    readonly code?: string;
    readonly locationName?: string;
  };
  readonly activeOffer: {
    readonly id: string;
    readonly name: string;
    readonly type: string;
    readonly priceCents: number;
    readonly currency: string;
    readonly includedPrintEntitlement: string;
    readonly allowsExtraPrintAddOn: boolean;
    readonly extraPrintPriceCents: number | null;
    readonly activationStatus: string;
    readonly startsAt: string | null;
    readonly endsAt: string | null;
    readonly sessionAllowance: number | null;
    readonly sessionsUsed: number;
  } | null;
  readonly paymentOptions: readonly {
    readonly method: string;
    readonly label: string;
    readonly runtimeEnabled: boolean;
  }[];
  readonly activeTransaction?: {
    readonly id: string;
    readonly transactionNumber: string;
    readonly transactionType: string;
    readonly status: string;
    readonly paymentMethod: string;
    readonly amountCents: number;
    readonly currency: string;
    readonly createdAt?: string;
    readonly expiresAt: string;
    readonly qrPayment?: {
      readonly provider: string;
      readonly providerReference: string | null;
      readonly imageUrl: string | null;
      readonly expiresAt: string;
    } | null;
  } | null;
  readonly recentTransaction?: {
    readonly id: string;
    readonly status: string;
    readonly transactionType: string;
    readonly occurredAt: string;
    readonly reason: string | null;
    readonly cancelledByActorType?: string | null;
    readonly cancelledByUserId?: string | null;
    readonly cancellationSource?: string | null;
    readonly cancellationPreviousStatus?: string | null;
  } | null;
};

export type BoothStageAction =
  | 'connect'
  | 'confirm-offer'
  | 'cash'
  | 'paymongo-qrph'
  | 'refresh'
  | 'return-welcome'
  | 'cancel-transaction'
  | 'acknowledge-recent';
