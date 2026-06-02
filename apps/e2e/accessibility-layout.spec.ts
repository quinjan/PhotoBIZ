import AxeBuilder from '@axe-core/playwright';
import {
  APIRequestContext,
  Page,
  expect,
  request as playwrightRequest,
  test,
} from '@playwright/test';

type ViewportCase = {
  readonly name: string;
  readonly width: number;
  readonly height: number;
};

type ReviewBooth = {
  readonly boothId: string;
  readonly boothCode: string;
  readonly kioskToken: string;
  readonly agentCredential: string;
};

type ReviewData = {
  readonly suffix: string;
  readonly clientAccountId: string;
  readonly ownerEmail: string;
  readonly ownerPassword: string;
  readonly adminEmail: string;
  readonly adminPassword: string;
  readonly cashierEmail: string;
  readonly cashierPassword: string;
  readonly forcedEmail: string;
  readonly vintageBooth: ReviewBooth;
  readonly popBooth: ReviewBooth;
};

const API_BASE_URL = process.env.PHOTOBIZ_API_URL ?? 'http://localhost:5082';
const ADMIN_BASE_URL = process.env.ADMIN_WEB_URL ?? 'http://localhost:4200';
const BOOTH_BASE_URL = process.env.BOOTH_UI_URL ?? 'http://localhost:4201';
const BOOTSTRAP_OWNER_EMAIL = process.env.PHOTOBIZ_E2E_OWNER_EMAIL ?? 'owner@photobiz.local';
const BOOTSTRAP_OWNER_PASSWORD = process.env.PHOTOBIZ_E2E_OWNER_PASSWORD ?? 'PhotoBIZ!123';
const DEFAULT_INITIAL_PASSWORD = 'PhotoBIZ!123';

const viewports: readonly ViewportCase[] = [
  { name: 'widescreen', width: 1920, height: 1080 },
  { name: 'large-laptop', width: 1440, height: 900 },
  { name: 'laptop', width: 1366, height: 768 },
  { name: 'tablet-landscape', width: 1024, height: 768 },
  { name: 'tablet-portrait', width: 768, height: 1024 },
];

const ownerRoutes = [
  { path: '/dashboard', heading: 'PhotoBIZ Platform Dashboard' },
  { path: '/subscriptions', heading: 'Subscriptions' },
  { path: '/clients', heading: 'Client Accounts' },
  { path: '/audit', heading: 'Audit Log' },
  { path: '/account', heading: 'Account' },
] as const;

const clientRoutes = [
  { path: '/dashboard', heading: 'Operations Dashboard' },
  { path: '/users', heading: 'Users' },
  { path: '/locations', heading: 'Location & Booth Inventory' },
  { path: '/booths', heading: 'Booths' },
  { path: '/packages', heading: 'Packages' },
  { path: '/transactions', heading: 'Sales & Audit' },
  { path: '/pos', heading: 'Cashier POS' },
  { path: '/reports', heading: 'Reports' },
  { path: '/settings', heading: 'Payment Resources' },
  { path: '/settings/paymongo', heading: 'PayMongo QR Ph Setup' },
  { path: '/audit', heading: 'Audit Log' },
  { path: '/account', heading: 'Account' },
] as const;

const cashierRoutes = [
  { path: '/dashboard', heading: 'Operations Dashboard' },
  { path: '/pos', heading: 'Cashier POS' },
  { path: '/reports', heading: 'Reports' },
  { path: '/audit', heading: 'Audit Log' },
  { path: '/account', heading: 'Account' },
] as const;

test.describe.configure({ mode: 'serial' });

let reviewData: ReviewData;

test.beforeAll(async () => {
  reviewData = await createReviewData();
});

for (const viewport of viewports) {
  test(`auth screens fit and pass serious axe checks at ${viewport.name} @a11y`, async ({
    page,
  }) => {
    await page.setViewportSize(viewport);
    await page.goto(ADMIN_BASE_URL);

    await expect(page.getByRole('heading', { name: 'Sign in' })).toBeVisible();
    await expectNoPageOverflow(page, `sign-in ${viewport.name}`);
    await expectNoSeriousAxeViolations(page, `sign-in ${viewport.name}`);

    await signIn(page, reviewData.forcedEmail, DEFAULT_INITIAL_PASSWORD, {
      expectPasswordChange: true,
    });
    await expectNoPageOverflow(page, `forced-password ${viewport.name}`);
    await expectNoSeriousAxeViolations(page, `forced-password ${viewport.name}`);
  });

  test(`application owner routes fit and pass serious axe checks at ${viewport.name} @a11y`, async ({
    page,
  }) => {
    await page.setViewportSize(viewport);
    await signIn(page, BOOTSTRAP_OWNER_EMAIL, BOOTSTRAP_OWNER_PASSWORD);

    await expectVisibleNav(page, ['Dashboard', 'Subscriptions', 'Clients', 'Audit Log']);
    for (const route of ownerRoutes) {
      await visitAdminRoute(page, route.path, route.heading);
      await expectNoPageOverflow(page, `owner ${route.path} ${viewport.name}`);
      await expectNoSeriousAxeViolations(page, `owner ${route.path} ${viewport.name}`);
    }
  });

  test(`client admin routes fit and pass serious axe checks at ${viewport.name} @a11y`, async ({
    page,
  }) => {
    await page.setViewportSize(viewport);
    await signIn(page, reviewData.adminEmail, reviewData.adminPassword);

    await expectVisibleNav(page, [
      'Dashboard',
      'Users',
      'Locations',
      'Booths',
      'Packages',
      'Transactions',
      'Cashier POS',
      'Reports',
      'Settings',
      'Audit Log',
    ]);
    for (const route of clientRoutes) {
      await visitAdminRoute(page, route.path, route.heading);
      await expectNoPageOverflow(page, `client-admin ${route.path} ${viewport.name}`);
      await expectNoSeriousAxeViolations(page, `client-admin ${route.path} ${viewport.name}`);
    }
  });

  test(`client owner routes fit and pass serious axe checks at ${viewport.name} @a11y`, async ({
    page,
  }) => {
    await page.setViewportSize(viewport);
    await signIn(page, reviewData.ownerEmail, reviewData.ownerPassword);

    await expectVisibleNav(page, [
      'Dashboard',
      'Users',
      'Locations',
      'Booths',
      'Packages',
      'Transactions',
      'Cashier POS',
      'Reports',
      'Settings',
      'Audit Log',
    ]);
    for (const route of clientRoutes) {
      await visitAdminRoute(page, route.path, route.heading);
      await expectNoPageOverflow(page, `client-owner ${route.path} ${viewport.name}`);
      await expectNoSeriousAxeViolations(page, `client-owner ${route.path} ${viewport.name}`);
    }
  });

  test(`cashier routes fit and pass serious axe checks at ${viewport.name} @a11y`, async ({
    page,
  }) => {
    await page.setViewportSize(viewport);
    await signIn(page, reviewData.cashierEmail, reviewData.cashierPassword);

    await expectVisibleNav(page, ['Dashboard', 'Cashier POS', 'Reports', 'Audit Log']);
    for (const route of cashierRoutes) {
      await visitAdminRoute(page, route.path, route.heading);
      await expectNoPageOverflow(page, `cashier ${route.path} ${viewport.name}`);
      await expectNoSeriousAxeViolations(page, `cashier ${route.path} ${viewport.name}`);
    }
  });

  test(`admin booth preview fits both presets at ${viewport.name} @a11y`, async ({ page }) => {
    await page.setViewportSize(viewport);
    await signIn(page, reviewData.adminEmail, reviewData.adminPassword);
    await visitAdminRoute(page, '/booths', 'Booths');
    await openAdminBoothDetail(page, reviewData.vintageBooth);
    await page.getByRole('tab', { name: 'Session Setup' }).click();
    await expect(page.getByText('Booth UI Preview')).toBeVisible();
    await expect(page.locator('photobiz-booth-stage')).toBeVisible();
    await expectNoPageOverflow(page, `admin booth preview vintage ${viewport.name}`);
    await expectNoSeriousAxeViolations(page, `admin booth preview vintage ${viewport.name}`);

    await page.getByRole('combobox', { name: 'Theme' }).click();
    await page.getByRole('option', { name: 'Pop' }).click();
    await expect(page.locator('.pop-stage')).toBeVisible();
    await expectNoPageOverflow(page, `admin booth preview pop ${viewport.name}`);
    await expectNoSeriousAxeViolations(page, `admin booth preview pop ${viewport.name}`);
  });

  test(`booth kiosk transaction states fit at ${viewport.name} @a11y`, async ({ page }) => {
    const data = await createReviewData(`flow-${viewport.name}`);
    await page.setViewportSize(viewport);
    await openBooth(page, data.vintageBooth.kioskToken);
    await expect(page.getByText('Ready To Pose?')).toBeVisible();
    await expectNoPageOverflow(page, `booth welcome ${viewport.name}`);
    await expectNoSeriousAxeViolations(page, `booth welcome ${viewport.name}`);

    await page.getByRole('button', { name: /start|pose|begin/i }).click();
    await expect(page.getByText('Payment Options')).toBeVisible();
    await expectNoPageOverflow(page, `booth payment ${viewport.name}`);
    await expectNoSeriousAxeViolations(page, `booth payment ${viewport.name}`);

    await chooseCashPayment(page);
    await expect(page.getByText(/Cashier Approval|Waiting/i)).toBeVisible();
    const transactionId = await currentBoothTransactionId(data.vintageBooth.kioskToken);
    await expectNoPageOverflow(page, `booth cash waiting ${viewport.name}`);
    await expectNoSeriousAxeViolations(page, `booth cash waiting ${viewport.name}`);

    await approveCash(data, transactionId);
    await page.reload();
    await expect(page.getByText(/approved|starting/i)).toBeVisible();
    await expectNoPageOverflow(page, `booth approved ${viewport.name}`);
    await expectNoSeriousAxeViolations(page, `booth approved ${viewport.name}`);

    await acquireNextAgentCommand(data.vintageBooth);
    await markSessionStarted(data.vintageBooth, transactionId);
    await page.reload();
    await expect(page.getByRole('heading', { name: /session|lumabooth|capture/i })).toBeVisible();
    await expectNoPageOverflow(page, `booth session ${viewport.name}`);
    await expectNoSeriousAxeViolations(page, `booth session ${viewport.name}`);

    await markSessionCompleted(data.vintageBooth, transactionId);
    await page.reload();
    await expect(page.getByRole('heading', { name: /thanks|sharing your smile/i })).toBeVisible();
    await expectNoPageOverflow(page, `booth completed ${viewport.name}`);
    await expectNoSeriousAxeViolations(page, `booth completed ${viewport.name}`);
  });

  test(`booth kiosk unavailable, offline, cancelled, and pop states fit at ${viewport.name} @a11y`, async ({
    page,
  }) => {
    const data = await createReviewData(`edge-${viewport.name}`);
    await page.setViewportSize(viewport);

    await markAgentOffline(data.vintageBooth);
    await openBooth(page, data.vintageBooth.kioskToken);
    await expect(page.getByRole('heading', { name: /agent offline/i })).toBeVisible();
    await expectNoPageOverflow(page, `booth offline ${viewport.name}`);
    await expectNoSeriousAxeViolations(page, `booth offline ${viewport.name}`);

    const unavailable = await createReviewData(`unavailable-${viewport.name}`, {
      activateOffer: false,
    });
    await openBooth(page, unavailable.vintageBooth.kioskToken);
    await expect(page.getByRole('heading', { name: /unavailable|no active/i })).toBeVisible();
    await expectNoPageOverflow(page, `booth unavailable ${viewport.name}`);
    await expectNoSeriousAxeViolations(page, `booth unavailable ${viewport.name}`);

    await openBooth(page, data.popBooth.kioskToken);
    await expect(page.locator('.pop-stage')).toBeVisible();
    await expectNoPageOverflow(page, `booth pop welcome ${viewport.name}`);
    await expectNoSeriousAxeViolations(page, `booth pop welcome ${viewport.name}`);

    await ensureAgentOnline(data.vintageBooth);
    await openBooth(page, data.vintageBooth.kioskToken);
    await page.getByRole('button', { name: /start|pose|begin/i }).click();
    await chooseCashPayment(page);
    await page.getByRole('button', { name: /back/i }).click();
    await page.getByRole('button', { name: /go back/i }).click();
    await expect(
      page.getByRole('heading', { name: /cancelled|canceled|ready to pose|start/i }),
    ).toBeVisible();
    await expectNoPageOverflow(page, `booth cancelled ${viewport.name}`);
    await expectNoSeriousAxeViolations(page, `booth cancelled ${viewport.name}`);
  });
}

async function signIn(
  page: Page,
  email: string,
  password: string,
  options: { readonly expectPasswordChange?: boolean } = {},
): Promise<void> {
  await page.goto(ADMIN_BASE_URL);
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Password').fill(password);
  await page.getByRole('button', { name: 'Sign In' }).click();

  if (options.expectPasswordChange) {
    await expect(page.getByRole('heading', { name: 'Update password' })).toBeVisible();
    return;
  }

  await waitForAdminReady(page);
}

async function visitAdminRoute(page: Page, path: string, heading: string): Promise<void> {
  await page.goto(`${ADMIN_BASE_URL}${path}`);
  await waitForAdminReady(page);
  await expect(page.getByRole('heading', { name: heading })).toBeVisible();
}

async function openAdminBoothDetail(page: Page, booth: ReviewBooth): Promise<void> {
  await page
    .getByRole('button', { name: new RegExp(`^Manage .*${escapeRegExp(booth.boothCode)}$`) })
    .click();
  await expect(page.getByText(booth.boothCode)).toBeVisible();
}

function escapeRegExp(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

async function waitForAdminReady(page: Page): Promise<void> {
  await expect(page.locator('.admin-shell')).toBeVisible();
  await page.waitForFunction(() => !document.querySelector('.busy-overlay'));
}

async function openBooth(page: Page, token: string): Promise<void> {
  await page.goto(`${BOOTH_BASE_URL}/${encodeURIComponent(token)}`);
  await page.waitForFunction(() => !document.querySelector('[booth-stage-error]'));
  await expect(page.locator('photobiz-booth-stage')).toBeVisible();
}

async function expectVisibleNav(page: Page, labels: readonly string[]): Promise<void> {
  const nav = page.getByRole('navigation', { name: 'Admin navigation' });
  for (const label of labels) {
    await expect(nav.getByRole('button', { name: label })).toBeVisible();
  }
}

async function chooseCashPayment(page: Page): Promise<void> {
  await page.getByRole('button', { name: /cash/i }).click();
  const confirmButton = page.getByRole('button', { name: /^confirm$/i });
  await expect(confirmButton).toBeVisible();
  await confirmButton.click();
}

async function expectNoSeriousAxeViolations(page: Page, label: string): Promise<void> {
  const results = await new AxeBuilder({ page }).analyze();
  const violations = results.violations.filter(
    (violation) => violation.impact === 'critical' || violation.impact === 'serious',
  );

  expect(
    violations.map((violation) => ({
      id: violation.id,
      impact: violation.impact,
      description: violation.description,
      nodes: violation.nodes.map((node) => node.target.join(', ')),
    })),
    `${label} has critical/serious axe violations`,
  ).toEqual([]);
}

async function expectNoPageOverflow(page: Page, label: string): Promise<void> {
  const overflow = await page.evaluate(() => {
    const viewportWidth = document.documentElement.clientWidth;
    const pageScrollWidth = Math.max(
      document.documentElement.scrollWidth,
      document.body.scrollWidth,
    );
    const offenders = Array.from(document.body.querySelectorAll<HTMLElement>('*'))
      .filter((element) => {
        if (element.closest('.ag-root, .cdk-overlay-container')) {
          return false;
        }

        const style = window.getComputedStyle(element);
        if (
          style.position === 'fixed' ||
          style.visibility === 'hidden' ||
          style.display === 'none'
        ) {
          return false;
        }

        const rect = element.getBoundingClientRect();
        return rect.width > 0 && (rect.left < -2 || rect.right > viewportWidth + 2);
      })
      .slice(0, 8)
      .map((element) => ({
        tag: element.tagName.toLowerCase(),
        className: element.className.toString(),
        text: element.textContent?.trim().slice(0, 80) ?? '',
        rect: element.getBoundingClientRect().toJSON(),
      }));

    return { pageScrollWidth, viewportWidth, offenders };
  });

  expect(overflow.offenders, `${label} has elements outside the viewport`).toEqual([]);
  expect(
    overflow.pageScrollWidth,
    `${label} document is wider than the viewport`,
  ).toBeLessThanOrEqual(overflow.viewportWidth + 2);
}

async function createReviewData(
  label = 'review',
  options: { readonly activateOffer?: boolean } = {},
): Promise<ReviewData> {
  const activateOffer = options.activateOffer ?? true;
  const suffix = `${label}-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
  const owner = await playwrightRequest.newContext({ baseURL: API_BASE_URL });

  try {
    await assertOk(
      owner.post('/api/auth/login', {
        data: { email: BOOTSTRAP_OWNER_EMAIL, password: BOOTSTRAP_OWNER_PASSWORD },
      }),
      'log in bootstrap Application Owner',
    );

    const plan = await postJson(owner, '/api/admin/subscription-plans', {
      name: `A11y Starter ${suffix}`,
      pricePerBoothCents: 150_000,
      currency: 'PHP',
    });
    const onboarded = await postJson(owner, '/api/admin/clients/onboard', {
      clientName: `A11y Studio ${suffix}`,
      ownerName: 'A11y Client Owner',
      ownerEmail: `owner-${suffix}@photobiz.test`,
    });
    const clientAccountId = onboarded.client.id as string;
    const ownerEmail = onboarded.owner.email as string;

    await postJson(owner, '/api/admin/subscriptions', {
      clientAccountId,
      subscriptionPlanId: plan.id,
      status: 'ACTIVE',
      activeBoothAllowance: 3,
      notes: 'Created by Playwright accessibility/layout setup.',
    });

    const clientAdmin = await postJson(owner, '/api/admin/users', {
      clientAccountId,
      assignedBoothId: null,
      name: 'A11y Client Admin',
      email: `admin-${suffix}@photobiz.test`,
      role: 'CLIENT_ADMIN',
      canApproveCash: true,
      canReturnBoothToWelcome: true,
      canCancelTransaction: true,
    });
    const cashier = await postJson(owner, '/api/admin/users', {
      clientAccountId,
      assignedBoothId: null,
      name: 'A11y Cashier',
      email: `cashier-${suffix}@photobiz.test`,
      role: 'CASHIER',
      canApproveCash: true,
      canReturnBoothToWelcome: true,
      canCancelTransaction: true,
    });
    const forced = await postJson(owner, '/api/admin/users', {
      clientAccountId,
      assignedBoothId: null,
      name: 'A11y Forced Password',
      email: `forced-${suffix}@photobiz.test`,
      role: 'CLIENT_ADMIN',
      canApproveCash: true,
      canReturnBoothToWelcome: true,
      canCancelTransaction: true,
    });
    const location = await postJson(owner, '/api/admin/locations', {
      clientAccountId,
      name: `A11y Location ${suffix}`,
      address: 'Local Playwright setup',
    });
    const vintageBoothResponse = await postJson(owner, '/api/admin/booths', {
      clientAccountId,
      locationId: location.id,
      name: `Booth A ${suffix}`,
      code: `A11Y-${suffix.slice(-6)}`.toUpperCase(),
      cashierUserId: cashier.id,
    });
    const popBoothResponse = await postJson(owner, '/api/admin/booths', {
      clientAccountId,
      locationId: location.id,
      name: `Booth Pop ${suffix}`,
      code: `POP-${suffix.slice(-6)}`.toUpperCase(),
      cashierUserId: null,
    });
    const offer = await postJson(owner, '/api/admin/offers', {
      clientAccountId,
      name: `Per Session ${suffix}`,
      description: 'Standard booth session for Playwright review.',
      offerType: 'PER_SESSION',
      priceCents: 25_000,
      currency: 'PHP',
      includedPrintEntitlement: '2 pcs 6x2 or 1 pc 6x4',
      durationHours: null,
      sessionAllowance: null,
      allowsExtraPrintAddOn: true,
      extraPrintPriceCents: 5_000,
      lumaboothSessionMode: 'PRINT',
    });

    const vintageBooth = toReviewBooth(vintageBoothResponse);
    const popBooth = toReviewBooth(popBoothResponse);
    if (activateOffer) {
      await postJson(owner, `/api/admin/booths/${vintageBooth.boothId}/activate-offer`, {
        boothOfferId: offer.id,
      });
      await postJson(owner, `/api/admin/booths/${popBooth.boothId}/activate-offer`, {
        boothOfferId: offer.id,
      });
    }

    await postJson(owner, `/api/admin/booths/${vintageBooth.boothId}/payment-options`, {
      paymentMethod: 'CASH',
      runtimeEnabled: true,
      displayLabel: 'Cash',
    });
    await postJson(owner, `/api/admin/booths/${popBooth.boothId}/payment-options`, {
      paymentMethod: 'CASH',
      runtimeEnabled: true,
      displayLabel: 'Cash',
    });
    await putJson(owner, `/api/admin/booths/${popBooth.boothId}/appearance`, {
      themePreset: 'POP',
      sessionLabel: 'Self Photo Booth',
      defaultWelcomeHeadline: 'Ready To Pop?',
      defaultWelcomeSubtitle: 'Tap start when you are ready.',
      completionThankYouMessage: 'Thanks for sharing your smile.',
      backgroundImageDataUrl: null,
    });

    const ownerPassword = `PhotoBIZ!${suffix}Owner`;
    const adminPassword = `PhotoBIZ!${suffix}Admin`;
    const cashierPassword = `PhotoBIZ!${suffix}Cashier`;
    await completePasswordChange(ownerEmail, DEFAULT_INITIAL_PASSWORD, ownerPassword);
    await completePasswordChange(clientAdmin.email, DEFAULT_INITIAL_PASSWORD, adminPassword);
    await completePasswordChange(cashier.email, DEFAULT_INITIAL_PASSWORD, cashierPassword);
    await ensureAgentOnline(vintageBooth);
    await ensureAgentOnline(popBooth);

    return {
      suffix,
      clientAccountId,
      ownerEmail,
      ownerPassword,
      adminEmail: clientAdmin.email,
      adminPassword,
      cashierEmail: cashier.email,
      cashierPassword,
      forcedEmail: forced.email,
      vintageBooth,
      popBooth,
    };
  } finally {
    await owner.dispose();
  }
}

function toReviewBooth(response: {
  readonly booth: { readonly id: string; readonly code: string };
  readonly kioskToken: string;
  readonly agentCredential: string;
}): ReviewBooth {
  return {
    boothId: response.booth.id,
    boothCode: response.booth.code,
    kioskToken: response.kioskToken,
    agentCredential: response.agentCredential,
  };
}

async function completePasswordChange(
  email: string,
  currentPassword: string,
  newPassword: string,
): Promise<void> {
  const user = await playwrightRequest.newContext({ baseURL: API_BASE_URL });
  try {
    await assertOk(
      user.post('/api/auth/login', { data: { email, password: currentPassword } }),
      `log in ${email}`,
    );
    await assertOk(
      user.post('/api/auth/change-password', {
        data: { currentPassword, newPassword, confirmPassword: newPassword },
      }),
      `complete password change for ${email}`,
    );
  } finally {
    await user.dispose();
  }
}

async function ensureAgentOnline(booth: ReviewBooth): Promise<void> {
  await postAgent(booth, '/api/agent/heartbeat', {
    boothCode: booth.boothCode,
    agentVersion: 'playwright-a11y',
    runtimeKind: 'Playwright',
    kioskRunning: true,
    lumaBoothMode: 'Simulator',
    apiReachable: true,
    chromeLaunched: true,
    triggerListenerRunning: true,
    lumaBoothReachable: true,
  });
}

async function markAgentOffline(booth: ReviewBooth): Promise<void> {
  await postAgent(booth, '/api/agent/offline', { boothCode: booth.boothCode });
}

async function acquireNextAgentCommand(booth: ReviewBooth): Promise<void> {
  const agent = await playwrightRequest.newContext({
    baseURL: API_BASE_URL,
    extraHTTPHeaders: { 'X-Agent-Credential': booth.agentCredential },
  });
  try {
    await assertOk(
      agent.get(`/api/agent/commands/next?boothCode=${encodeURIComponent(booth.boothCode)}`),
      `acquire agent command for ${booth.boothCode}`,
    );
  } finally {
    await agent.dispose();
  }
}

async function markSessionStarted(booth: ReviewBooth, transactionId: string): Promise<void> {
  await postAgent(booth, `/api/agent/transactions/${transactionId}/session-started`, {
    boothCode: booth.boothCode,
    lumaboothSessionRef: `session-${transactionId}`,
    lumaboothEventType: 'session_start',
  });
}

async function markSessionCompleted(booth: ReviewBooth, transactionId: string): Promise<void> {
  await postAgent(booth, `/api/agent/transactions/${transactionId}/session-completed`, {
    boothCode: booth.boothCode,
    lumaboothSessionRef: `session-${transactionId}`,
    lumaboothEventType: 'session_end',
  });
}

async function postAgent(
  booth: ReviewBooth,
  path: string,
  data: Record<string, unknown>,
): Promise<void> {
  const agent = await playwrightRequest.newContext({
    baseURL: API_BASE_URL,
    extraHTTPHeaders: { 'X-Agent-Credential': booth.agentCredential },
  });
  try {
    await assertOk(agent.post(path, { data }), `${path} for ${booth.boothCode}`);
  } finally {
    await agent.dispose();
  }
}

async function currentBoothTransactionId(kioskToken: string): Promise<string> {
  const booth = await playwrightRequest.newContext({
    baseURL: API_BASE_URL,
    extraHTTPHeaders: { 'X-Kiosk-Token': kioskToken },
  });
  try {
    const config = await getJson(booth, '/api/booth-ui/config');
    const transactionId = config.activeTransaction?.id;
    expect(transactionId, 'active kiosk transaction id').toBeTruthy();
    return transactionId;
  } finally {
    await booth.dispose();
  }
}

async function approveCash(data: ReviewData, transactionId: string): Promise<void> {
  const cashier = await playwrightRequest.newContext({ baseURL: API_BASE_URL });
  try {
    await assertOk(
      cashier.post('/api/auth/login', {
        data: { email: data.cashierEmail, password: data.cashierPassword },
      }),
      `log in cashier ${data.cashierEmail}`,
    );
    await assertOk(
      cashier.post(`/api/cashier/transactions/${transactionId}/approve-cash`, { data: {} }),
      `approve cash ${transactionId}`,
    );
  } finally {
    await cashier.dispose();
  }
}

async function postJson(
  api: APIRequestContext,
  path: string,
  data: Record<string, unknown>,
): Promise<any> {
  const response = await assertOk(api.post(path, { data }), `POST ${path}`);
  return await response.json();
}

async function putJson(
  api: APIRequestContext,
  path: string,
  data: Record<string, unknown>,
): Promise<any> {
  const response = await assertOk(api.put(path, { data }), `PUT ${path}`);
  return await responseJsonOrEmpty(response);
}

async function getJson(api: APIRequestContext, path: string): Promise<any> {
  const response = await assertOk(api.get(path), `GET ${path}`);
  return await response.json();
}

async function responseJsonOrEmpty(response: {
  readonly text: () => Promise<string>;
}): Promise<any> {
  const body = await response.text();
  return body ? JSON.parse(body) : {};
}

async function assertOk(responsePromise: Promise<any>, label: string): Promise<any> {
  const response = await responsePromise;
  if (!response.ok()) {
    const body = await response.text();
    throw new Error(`${label} failed with ${response.status()} ${response.statusText()}: ${body}`);
  }

  return response;
}
