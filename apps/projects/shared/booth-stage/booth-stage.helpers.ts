import {
  BoothStageConfig,
  BoothStageScreenState,
  BoothStageThemePreset,
} from './booth-stage.models';

export function normalizeStagePreset(value: string | null | undefined): BoothStageThemePreset {
  switch ((value ?? '').toUpperCase()) {
    case 'POP':
    case 'MODERN_POP':
      return 'POP';
    case 'CLEAN_MODERN':
    case 'MODERN_CLEAN':
    case 'CLASSIC_LIGHT':
      return 'CLEAN_MODERN';
    default:
      return 'VINTAGE';
  }
}

export function stageTitle(config: BoothStageConfig | null, screen: BoothStageScreenState): string {
  switch (screen) {
    case 'connect':
      return 'Waiting For Booth Launch';
    case 'offline':
      return 'Agent Offline';
    case 'unavailable':
      return 'Booth Unavailable';
    case 'payment':
      return 'Payment Options';
    case 'waiting':
      return 'Cashier Approval';
    case 'approved':
      return 'Starting Session';
    case 'session':
      return 'Session In Progress';
    case 'completed':
      return config?.session.completionThankYouMessage?.trim() || 'Thanks for sharing your smile.';
    case 'expired':
      return 'Request Expired';
    case 'cancelled':
      return 'Request Cancelled';
    case 'payment-failed':
      return 'Payment Failed';
    case 'error':
      return 'Recovery Needed';
    default:
      return config?.session.welcomeHeadline ?? 'Welcome';
  }
}

export function stageMessage(
  config: BoothStageConfig | null,
  screen: BoothStageScreenState,
): string {
  switch (screen) {
    case 'connect':
      return 'Start the Windows Agent to open this booth.';
    case 'offline':
      return 'Start the Windows Agent on the booth laptop.';
    case 'unavailable':
      if (config?.activeOffer?.activationStatus === 'PENDING_PAYMENT') {
        return 'This package is awaiting cashier activation. Please go to the cashier.';
      }
      return config?.session.welcomeSubtitle ?? 'Ask staff to configure this booth.';
    case 'payment':
      return 'Choose how to pay before the session starts.';
    case 'waiting':
      return 'Please wait while the cashier confirms payment option. Please pay at the cashier after using the booth.';
    case 'approved':
      return 'Payment confirmed. The booth session is starting.';
    case 'session':
      return 'Follow the booth operator screen.';
    case 'completed':
      return config?.activeOffer?.type === 'PER_SESSION'
        ? 'Need extra prints? Please go to the cashier.'
        : 'Your session is complete.';
    case 'expired':
      return 'Please start again.';
    case 'cancelled':
      return config?.recentTransaction?.reason ?? 'Please ask the cashier.';
    case 'payment-failed':
      return config?.recentTransaction?.reason ?? 'Please ask the cashier.';
    case 'error':
      return 'Ask the cashier for booth recovery.';
    default:
      return config?.session.welcomeSubtitle ?? 'Review the active offer.';
  }
}

export function stageEyebrow(config: BoothStageConfig | null): string {
  return config?.session.label || 'Self Photo Booth';
}

export function stageBrandInitials(config: BoothStageConfig | null): string {
  return config?.client?.displayName?.slice(0, 2).toUpperCase() ?? 'PB';
}

export function stageCashOption(
  config: BoothStageConfig | null,
): { readonly method: string; readonly label: string; readonly runtimeEnabled: boolean } | null {
  return (
    config?.paymentOptions.find((option) => option.method === 'CASH' && option.runtimeEnabled) ??
    null
  );
}

export function shouldShowStageOfferDetails(config: BoothStageConfig | null): boolean {
  return config?.activeOffer?.type === 'PER_SESSION';
}

export function stageBackgroundImage(config: BoothStageConfig | null): string {
  const image =
    config?.theme.backgroundImageDataUrl?.trim() || config?.theme.backgroundImageUrl?.trim();
  return image ? `url("${image.replaceAll('"', '\\"')}")` : 'none';
}

export function formatStageMoney(cents: number): string {
  return `PHP ${(cents / 100).toLocaleString('en-PH', { maximumFractionDigits: 0 })}`;
}
