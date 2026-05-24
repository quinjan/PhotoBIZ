import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { By } from '@angular/platform-browser';
import { provideRouter, Router } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ICellRendererParams } from 'ag-grid-community';
import { vi } from 'vitest';
import { AdminGridComponent } from './admin-grid.component';
import {
  BoothDetailPageComponent,
  ClientsPageComponent,
  PackagesPageComponent,
  PrintEntitlementsDialogComponent,
} from './admin-pages';
import {
  AdminWorkspace,
  BoothAppearanceSummary,
  BoothSummary,
  ClientSummary,
  OfferSummary,
  OfferActivationSummary,
  Overview,
  PaymentAssignmentSummary,
  PaymentResourceSummary,
  PrintEntitlementSummary,
  ReportSummary,
  Session,
  SubscriptionPlanSummary,
  SubscriptionSummary,
  TransactionSummary,
  UserSummary,
} from './admin-workspace.service';
import { App } from './app';
import { routes } from './app.routes';

const apiBaseUrl = 'http://localhost:5082';

class ResizeObserverMock {
  observe(): void {}
  unobserve(): void {}
  disconnect(): void {}
}

describe('App', () => {
  let snackBar: { open: ReturnType<typeof vi.fn>; dismiss: ReturnType<typeof vi.fn> };

  beforeEach(async () => {
    snackBar = {
      dismiss: vi.fn(),
      open: vi.fn(),
    };
    vi.stubGlobal('ResizeObserver', ResizeObserverMock);

    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter(routes),
        provideNoopAnimations(),
        { provide: MatSnackBar, useValue: snackBar },
      ],
    }).compileComponents();
  });

  afterEach(() => {
    try {
      TestBed.inject(HttpTestingController).verify({ ignoreCancelled: true });
    } finally {
      TestBed.resetTestingModule();
      vi.unstubAllGlobals();
      vi.restoreAllMocks();
    }
  });

  it('renders the sign in screen when no session is restored', async () => {
    const fixture = TestBed.createComponent(App);
    rejectSessionRestore();

    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('PhotoBIZ');
    expect(compiled.textContent).toContain('Sign in');
    expect(compiled.querySelector('.admin-shell')).toBeNull();
  });

  it('keeps users with forced password changes in the password flow', async () => {
    const fixture = TestBed.createComponent(App);
    rejectSessionRestore();
    const workspace = TestBed.inject(AdminWorkspace);

    workspace.session.set(makeSession({ mustChangePassword: true }));
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Update password');
    expect(compiled.querySelector('.admin-shell')).toBeNull();
  });

  it('resets booth session copy to the selected theme defaults', () => {
    const workspace = createWorkspaceWithRejectedSession();

    workspace.boothAppearanceThemePreset.set('VINTAGE');
    workspace.boothAppearanceSessionLabel.set('Custom label');
    workspace.boothAppearanceHeadline.set('Custom headline');
    workspace.boothAppearanceSubtitle.set('Custom subtitle');
    workspace.boothAppearanceCompletionMessage.set('Custom thanks');

    workspace.resetBoothSessionToThemeDefaults();

    expect(workspace.boothAppearanceSessionLabel()).toBe('Self Photo Booth');
    expect(workspace.boothAppearanceHeadline()).toBe('Ready To Pose?');
    expect(workspace.boothAppearanceSubtitle()).toBe('Tap start when you are ready.');
    expect(workspace.boothAppearanceCompletionMessage()).toBe('Thanks for sharing your smile.');
  });

  it('offers only Vintage and Pop booth themes in Admin choices', () => {
    const workspace = createWorkspaceWithRejectedSession();

    expect(workspace.boothThemePresets.map((preset) => preset.label)).toEqual(['Vintage', 'Pop']);
    expect(workspace.boothThemePresets.map((preset) => preset.value)).toEqual(['VINTAGE', 'POP']);
  });

  it('maps deprecated Clean Modern booth appearance data back to Vintage in Admin', () => {
    const workspace = createWorkspaceWithRejectedSession();
    const session = makeSession();
    const booth = makeBooth();

    workspace.session.set(session);
    workspace.overview.set(
      makeOverview(session, {
        booths: [booth],
        appearanceConfigs: [makeAppearance({ boothId: booth.id, themePreset: 'CLEAN_MODERN' })],
      }),
    );

    workspace.syncBoothDetail(booth.id);

    expect(workspace.boothAppearanceThemePreset()).toBe('VINTAGE');
    expect(workspace.boothAppearanceHeadline()).toBe('Ready To Pose?');
  });

  it('clears booth background from the upload row instead of the footer actions', async () => {
    const fixture = TestBed.createComponent(BoothDetailPageComponent);
    rejectSessionRestore();
    const workspace = TestBed.inject(AdminWorkspace);
    const session = makeSession();
    const booth = makeBooth();

    workspace.session.set(session);
    workspace.overview.set(makeOverview(session, { booths: [booth] }));
    workspace.syncBoothDetail(booth.id);
    workspace.boothDetailTab.set('session');
    workspace.boothAppearanceBackgroundImageDataUrl.set('data:image/png;base64,abc');
    fixture.detectChanges();
    await fixture.whenStable();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).not.toContain('Clear Image');
    expect(compiled.textContent).toContain('Background image uploaded');
    expect(compiled.querySelector('.background-clear-icon')).toBeTruthy();

    (compiled.querySelector('.background-clear-icon') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(workspace.boothAppearanceBackgroundImageDataUrl()).toBe('');
    expect((compiled.querySelector('.background-file-input') as HTMLInputElement).value).toBe('');
    expect(compiled.textContent).toContain('No file chosen');
  });

  it('limits application owner navigation to platform management pages', async () => {
    const fixture = TestBed.createComponent(App);
    rejectSessionRestore();
    const workspace = TestBed.inject(AdminWorkspace);
    const session = makeSession({ role: 'APPLICATION_OWNER' });

    workspace.session.set(session);
    workspace.overview.set(makeOverview(session));
    fixture.detectChanges();

    expect(navLabels(fixture.nativeElement)).toEqual([
      'Dashboard',
      'Subscriptions',
      'Clients',
      'Audit Log',
    ]);
  });

  it('shows operational client pages as routed AG Grid views', async () => {
    const fixture = TestBed.createComponent(App);
    rejectSessionRestore();
    const router = TestBed.inject(Router);
    const workspace = TestBed.inject(AdminWorkspace);
    const session = makeSession({ role: 'APPLICATION_OWNER' });
    const client = makeClient();
    const subscription = makeSubscription({
      clientAccountId: client.id,
      status: 'TRIAL',
    });

    workspace.session.set(session);
    workspace.overview.set(
      makeOverview(session, {
        clients: [client],
        subscriptions: [subscription],
        users: [
          makeUser({
            clientAccountId: client.id,
            email: 'owner@example.test',
            id: 'owner-1',
            name: 'Client Owner',
            role: 'CLIENT_OWNER',
          }),
        ],
      }),
    );

    await router.navigateByUrl('/clients');
    await fixture.whenStable();
    fixture.detectChanges();

    const grid = fixture.debugElement.query(By.directive(AdminGridComponent));
    expect(grid).toBeTruthy();
    expect((grid.componentInstance as AdminGridComponent<ClientSummary>).rowData).toEqual([client]);
    const root = fixture.nativeElement as HTMLElement;
    expect(root.textContent).toContain('New Client');
    expect(root.textContent).not.toContain('Search clients');

    const page = fixture.debugElement.query(By.directive(ClientsPageComponent))
      .componentInstance as ClientsPageComponent;
    const subscriptionColumn = page.columns.find((column) => column.headerName === 'Subscription');
    const subscriptionChip = subscriptionColumn?.cellRenderer?.({
      value: subscription.status,
    } as ICellRendererParams<ClientSummary>) as HTMLElement;

    expect(subscriptionChip.classList.contains('status-chip')).toBe(true);
    expect(subscriptionChip.classList.contains('subscription-status-chip')).toBe(true);
    expect(subscriptionChip.classList.contains('trial')).toBe(true);
  });

  it('keeps grid action buttons connected to existing workflows', async () => {
    const fixture = TestBed.createComponent(App);
    rejectSessionRestore();
    const router = TestBed.inject(Router);
    const workspace = TestBed.inject(AdminWorkspace);
    const session = makeSession({ role: 'APPLICATION_OWNER' });
    const client = makeClient();

    workspace.session.set(session);
    workspace.overview.set(makeOverview(session, { clients: [client] }));

    await router.navigateByUrl('/clients');
    await fixture.whenStable();
    fixture.detectChanges();

    const page = fixture.debugElement.query(By.directive(ClientsPageComponent))
      .componentInstance as ClientsPageComponent;
    const actionColumn = page.columns.find((column) => column.headerName === 'Actions');
    const viewClient = vi.spyOn(workspace, 'viewClient');
    const actionButton = actionColumn?.cellRenderer?.({
      data: client,
    } as ICellRendererParams<ClientSummary>) as HTMLElement;

    actionButton.click();

    expect(viewClient).toHaveBeenCalledWith(client);
  });

  it('renders dashboard recent activity as a paginated activity grid', async () => {
    const fixture = TestBed.createComponent(App);
    rejectSessionRestore();
    const router = TestBed.inject(Router);
    const workspace = TestBed.inject(AdminWorkspace);
    const session = makeSession({ role: 'CLIENT_OWNER' });

    workspace.session.set(session);
    workspace.overview.set(
      makeOverview(session, {
        booths: [makeBooth({ id: 'booth-1', name: 'Booth A' })],
        transactions: [
          makeTransaction({
            id: 'session-1',
            offerName: 'Per Session',
            status: 'COMPLETED',
            transactionType: 'SESSION_PURCHASE',
          }),
          makeTransaction({
            id: 'extra-print-1',
            extraPrintCount: 2,
            offerName: 'Extra Prints',
            status: 'COMPLETED',
            transactionType: 'EXTRA_PRINT_ADD_ON',
          }),
        ],
      }),
    );

    await router.navigateByUrl('/dashboard');
    await fixture.whenStable();
    fixture.detectChanges();

    const activityCard = fixture.nativeElement.querySelector(
      '.dashboard-activity-card',
    ) as HTMLElement;
    const activityGrid = fixture.debugElement.query(By.directive(AdminGridComponent))
      .componentInstance as AdminGridComponent<TransactionSummary>;

    expect(workspace.dashboardActivityFilter()).toBe('ALL');
    expect(activityCard.textContent).toContain('Recent Activity');
    expect(activityCard.querySelector('.filter-row')).toBeNull();
    expect(activityGrid.rowData).toEqual([
      expect.objectContaining({ id: 'session-1' }),
      expect.objectContaining({ id: 'extra-print-1' }),
    ]);
    expect(activityGrid.paginationPageSize).toBe(5);
    expect(activityGrid.columnDefs.map((column) => column.headerName)).toEqual([
      'Transaction',
      'Activity',
      'Details',
      'Status',
      'Amount',
      'Created',
    ]);
  });

  it('formats cancellation actor and source in activity and audit details', () => {
    const workspace = TestBed.inject(AdminWorkspace);
    rejectSessionRestore();
    const session = makeSession({ role: 'CLIENT_OWNER' });
    const cashier = makeUser({ id: 'cashier-1', name: 'Maria Santos' });
    workspace.session.set(session);
    workspace.overview.set(
      makeOverview(session, {
        booths: [makeBooth({ id: 'booth-1', name: 'Booth A' })],
        users: [cashier],
      }),
    );

    const cancelled = makeTransaction({
      cancelledAt: '2026-05-23T00:05:00Z',
      cancelledByActorType: 'CASHIER',
      cancelledByUserId: cashier.id,
      cancellationPreviousStatus: 'STARTING_SESSION',
      cancellationSource: 'CASHIER_POS_RETURN_TO_WELCOME',
      status: 'CANCELLED',
    });

    const activity = workspace.transactionActivityFor(cancelled);
    const auditDetail = workspace.auditDetailFor({
      action: 'transaction.cancelled',
      clientAccountId: 'client-1',
      createdAt: '2026-05-23T00:05:00Z',
      entityId: cancelled.id,
      entityType: 'Transaction',
      id: 'audit-1',
      metadata: JSON.stringify({
        TransactionNumber: cancelled.transactionNumber,
        PreviousStatus: 'STARTING_SESSION',
        CancelledByActorType: 'CASHIER',
        CancelledByUserId: cashier.id,
        CancellationSource: 'CASHIER_POS_RETURN_TO_WELCOME',
      }),
      userId: cashier.id,
    });

    expect(workspace.cancellationDetailFor(cancelled)).toBe(
      'Cancelled by cashier Maria Santos / Return to welcome',
    );
    expect(activity.detail).toContain('Cancelled by cashier Maria Santos / Return to welcome');
    expect(auditDetail).toContain('TXN-001');
    expect(auditDetail).toContain('Cancelled by cashier Maria Santos');
    expect(auditDetail).toContain('Return to welcome');
    expect(auditDetail).toContain('from Starting Session');
  });

  it('opens subscription assignment from the client detail page', async () => {
    const fixture = TestBed.createComponent(App);
    rejectSessionRestore();
    const router = TestBed.inject(Router);
    const workspace = TestBed.inject(AdminWorkspace);
    const session = makeSession({ role: 'APPLICATION_OWNER' });
    const client = makeClient({ name: 'The Memory Box' });
    const plan = makeSubscriptionPlan({ name: 'Per Booth MVP' });

    workspace.session.set(session);
    workspace.overview.set(
      makeOverview(session, {
        clients: [client],
        subscriptionPlans: [plan],
        subscriptions: [
          makeSubscription({
            activeBoothAllowance: 2,
            clientAccountId: client.id,
            status: 'ACTIVE',
            subscriptionPlanId: plan.id,
          }),
        ],
        users: [
          makeUser({
            clientAccountId: client.id,
            email: 'owner@memorybox.local',
            id: 'owner-1',
            name: 'Client Owner',
            role: 'CLIENT_OWNER',
          }),
        ],
      }),
    );

    await router.navigateByUrl('/clients/detail');
    await fixture.whenStable();
    fixture.detectChanges();

    const root = fixture.nativeElement as HTMLElement;
    const subscriptionSummary = root.querySelector('.inline-status-row')?.textContent ?? '';

    expect(root.querySelectorAll('.client-detail-card')).toHaveLength(1);
    expect(root.querySelector('.detail-action-card')).toBeNull();
    expect(subscriptionSummary).toContain('Per Booth MVP');
    expect(subscriptionSummary).toContain('ACTIVE');

    const assignButton = Array.from(root.querySelectorAll('button')).find(
      (button) => button.textContent?.trim() === 'Assign Subscription',
    ) as HTMLButtonElement;

    assignButton.click();
    fixture.detectChanges();
    await fixture.whenStable();

    expect(document.body.querySelector('mat-dialog-container')).toBeTruthy();
    expect(document.body.textContent).toContain('Assign Subscription');
  });

  it('updates the current client subscription instead of creating a duplicate assignment', async () => {
    const workspace = createWorkspaceWithRejectedSession();
    const http = TestBed.inject(HttpTestingController);
    const session = makeSession({ role: 'APPLICATION_OWNER' });
    const client = makeClient({ name: 'The Memory Box' });
    const currentPlan = makeSubscriptionPlan({ id: 'plan-1', name: 'Per Booth MVP' });
    const newPlan = makeSubscriptionPlan({ id: 'plan-2', name: 'Growth Plan' });
    const subscription = makeSubscription({
      activeBoothAllowance: 2,
      clientAccountId: client.id,
      id: 'subscription-1',
      status: 'TRIAL',
      subscriptionPlanId: currentPlan.id,
    });

    workspace.session.set(session);
    workspace.overview.set(
      makeOverview(session, {
        clients: [client],
        subscriptionPlans: [currentPlan, newPlan],
        subscriptions: [subscription],
      }),
    );

    workspace.openSubscriptionModal(client.id);

    expect(workspace.subscriptionPlanId()).toBe(currentPlan.id);
    expect(workspace.subscriptionStatus()).toBe('TRIAL');
    expect(workspace.subscriptionAllowance()).toBe(2);

    workspace.subscriptionPlanId.set(newPlan.id);
    workspace.subscriptionStatus.set('SUSPENDED');
    workspace.subscriptionAllowance.set(3);
    const save = workspace.assignSubscription();

    const update = http.expectOne(`${apiBaseUrl}/api/admin/subscriptions/${subscription.id}`);
    expect(update.request.method).toBe('PUT');
    expect(update.request.body).toEqual(
      expect.objectContaining({
        activeBoothAllowance: 3,
        status: 'SUSPENDED',
        subscriptionPlanId: newPlan.id,
      }),
    );
    update.flush({});

    await Promise.resolve();
    http.expectOne(`${apiBaseUrl}/api/admin/overview`).flush(
      makeOverview(session, {
        clients: [client],
        subscriptionPlans: [currentPlan, newPlan],
        subscriptions: [
          makeSubscription({
            activeBoothAllowance: 3,
            clientAccountId: client.id,
            id: subscription.id,
            status: 'SUSPENDED',
            subscriptionPlanId: newPlan.id,
          }),
        ],
      }),
    );
    await save;

    expect(snackBar.open).toHaveBeenCalledWith(
      'Client subscription updated.',
      'Dismiss',
      expect.objectContaining({ panelClass: ['snackbar-success'] }),
    );
  });

  it('toggles booth cash payment assignment through the existing payment endpoints', async () => {
    const workspace = createWorkspaceWithRejectedSession();
    const http = TestBed.inject(HttpTestingController);
    const session = makeSession({ role: 'CLIENT_OWNER' });
    const booth = makeBooth();
    const enabledAssignment = makePaymentAssignment({ boothId: booth.id, runtimeEnabled: true });

    workspace.session.set(session);
    workspace.overview.set(makeOverview(session, { booths: [booth] }));

    const enable = workspace.setCashPaymentEnabled(booth.id, true);
    const enableRequest = http.expectOne(
      `${apiBaseUrl}/api/admin/booths/${booth.id}/payment-options`,
    );
    expect(enableRequest.request.method).toBe('POST');
    expect(enableRequest.request.body).toEqual({ paymentMethod: 'CASH', runtimeEnabled: true });
    enableRequest.flush(enabledAssignment);

    await Promise.resolve();
    http
      .expectOne(`${apiBaseUrl}/api/admin/overview`)
      .flush(makeOverview(session, { booths: [booth], paymentAssignments: [enabledAssignment] }));
    await enable;

    workspace.overview.set(
      makeOverview(session, { booths: [booth], paymentAssignments: [enabledAssignment] }),
    );

    const disable = workspace.setCashPaymentEnabled(booth.id, false);
    const disableRequest = http.expectOne(
      `${apiBaseUrl}/api/admin/booths/${booth.id}/payment-options/CASH`,
    );
    expect(disableRequest.request.method).toBe('DELETE');
    disableRequest.flush({ ...enabledAssignment, runtimeEnabled: false, status: 'DISABLED' });

    await Promise.resolve();
    http.expectOne(`${apiBaseUrl}/api/admin/overview`).flush(
      makeOverview(session, {
        booths: [booth],
        paymentAssignments: [
          makePaymentAssignment({ boothId: booth.id, runtimeEnabled: false, status: 'DISABLED' }),
        ],
      }),
    );
    await disable;

    expect(snackBar.open).toHaveBeenCalledWith(
      'Payment assignment disabled.',
      'Dismiss',
      expect.objectContaining({ panelClass: ['snackbar-success'] }),
    );
  });

  it('only shows pending cash transactions as the current POS payment request', () => {
    const workspace = createWorkspaceWithRejectedSession();
    const session = makeSession({ assignedBoothId: 'booth-1', role: 'CLIENT_OWNER' });
    const booth = makeBooth({ currentState: 'IN_LUMABOOTH_SESSION' });

    workspace.session.set(session);
    workspace.overview.set(
      makeOverview(session, {
        booths: [booth],
        transactions: [
          makeTransaction({ id: 'paid-transaction', status: 'PAID' }),
          makeTransaction({ id: 'starting-transaction', status: 'STARTING_SESSION' }),
          makeTransaction({ id: 'in-session-transaction', status: 'IN_SESSION' }),
        ],
      }),
    );

    expect(workspace.cashierTransaction()).toBeNull();

    workspace.overview.set(
      makeOverview(session, {
        booths: [booth],
        transactions: [makeTransaction({ id: 'pending-transaction', status: 'PENDING_CASH' })],
      }),
    );

    expect(workspace.cashierTransaction()?.id).toBe('pending-transaction');
  });

  it('renders assigned POS booth details with package, payment, and booth state chip styling', async () => {
    const fixture = TestBed.createComponent(App);
    rejectSessionRestore();
    const router = TestBed.inject(Router);
    const workspace = TestBed.inject(AdminWorkspace);
    const session = makeSession({ assignedBoothId: 'booth-1', role: 'CLIENT_OWNER' });
    const booth = makeBooth({ currentState: 'IN_LUMABOOTH_SESSION' });
    const offer = makeOffer({ id: 'offer-1', name: 'Per Session', offerType: 'PER_SESSION' });
    const activation = makeActivation({ boothId: booth.id, boothOfferId: offer.id });

    workspace.session.set(session);
    workspace.overview.set(
      makeOverview(session, {
        activations: [activation],
        booths: [booth],
        offers: [offer],
        paymentAssignments: [makePaymentAssignment({ boothId: booth.id })],
      }),
    );

    await router.navigateByUrl('/pos');
    await fixture.whenStable();
    fixture.detectChanges();

    const root = fixture.nativeElement as HTMLElement;
    const summary = root.querySelector('.pos-booth-summary') as HTMLElement;

    expect(summary.textContent).toContain('Booth A');
    expect(summary.textContent).toContain('IN_LUMABOOTH_SESSION');
    expect(summary.textContent).toContain('Per Session');
    expect(summary.textContent).toContain('Pay per session');
    expect(summary.textContent).toContain('Cash');
    expect(summary.querySelector('.status-chip')?.classList.contains('in-lumabooth-session')).toBe(
      true,
    );
    expect(summary.querySelectorAll('.pos-booth-actions button')).toHaveLength(2);
  });

  it('opens eligible extra prints for the previous booth transaction', async () => {
    const fixture = TestBed.createComponent(App);
    rejectSessionRestore();
    const router = TestBed.inject(Router);
    const http = TestBed.inject(HttpTestingController);
    const workspace = TestBed.inject(AdminWorkspace);
    const session = makeSession({ assignedBoothId: 'booth-1', role: 'CLIENT_OWNER' });
    const booth = makeBooth({ id: 'booth-1', name: 'Booth A' });
    const extraPrintCandidate = makeTransaction({
      boothId: booth.id,
      canCreateExtraPrintAddOn: true,
      createdAt: '2026-05-23T00:01:00Z',
      extraPrintUnitPriceCents: 5000,
      id: 'session-1',
      includedPrintEntitlement: '2 pcs 6x2 or 1 pc 6x4',
      offerName: 'Per Session',
      status: 'COMPLETED',
      transactionNumber: 'TXN-EXTRA-1',
      transactionType: 'SESSION_PURCHASE',
    });
    const currentPendingSession = makeTransaction({
      boothId: booth.id,
      createdAt: '2026-05-23T00:02:00Z',
      id: 'current-session',
      status: 'PENDING_CASH',
      transactionNumber: 'TXN-CURRENT',
    });
    const completedExtraPrint = makeTransaction({
      boothId: booth.id,
      createdAt: '2026-05-23T00:01:30Z',
      extraPrintCount: 2,
      id: 'extra-print-1',
      status: 'COMPLETED',
      transactionNumber: 'TXN-EXTRA-PRINT',
      transactionType: 'EXTRA_PRINT_ADD_ON',
    });

    workspace.session.set(session);
    workspace.overview.set(
      makeOverview(session, {
        booths: [booth],
        transactions: [currentPendingSession, completedExtraPrint, extraPrintCandidate],
      }),
    );

    await router.navigateByUrl('/pos');
    await fixture.whenStable();
    fixture.detectChanges();

    const root = fixture.nativeElement as HTMLElement;
    expect(root.textContent).not.toContain('Extra PrintsCopies');
    const extraPrintButton = Array.from(root.querySelectorAll('.pos-booth-actions button')).find(
      (button) => button.textContent?.trim() === 'Extra Print',
    ) as HTMLButtonElement;

    extraPrintButton.click();
    fixture.detectChanges();
    await fixture.whenStable();

    const dialog = document.body.querySelector('admin-extra-print-dialog') as HTMLElement;
    expect(dialog).toBeTruthy();
    expect(dialog.textContent).toContain('TXN-EXTRA-1');
    expect(dialog.textContent).toContain('Booth A / Per Session / 2 pcs 6x2 or 1 pc 6x4');
    expect(dialog.textContent).toContain('PHP 50');
    expect(dialog.textContent).toContain('Total PHP 50');

    const createButton = Array.from(dialog.querySelectorAll('button')).find(
      (button) => button.textContent?.trim() === 'Create Extra Prints',
    ) as HTMLButtonElement;
    createButton.click();
    fixture.detectChanges();
    await fixture.whenStable();

    const confirmation = document.body.querySelector('admin-confirmation-dialog') as HTMLElement;
    expect(confirmation).toBeTruthy();
    expect(confirmation.textContent).toContain('Create Extra Print Transaction?');
    const confirmButton = Array.from(confirmation.querySelectorAll('button')).find(
      (button) => button.textContent?.trim() === 'Create Extra Prints',
    ) as HTMLButtonElement;
    confirmButton.click();
    fixture.detectChanges();
    await fixture.whenStable();
    await new Promise<void>((resolve) => setTimeout(resolve));

    const request = http.expectOne(`${apiBaseUrl}/api/cashier/transactions/session-1/extra-prints`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ copyCount: 1 });
    request.flush({});

    await Promise.resolve();
    http.expectOne(`${apiBaseUrl}/api/admin/overview`).flush(
      makeOverview(session, {
        booths: [booth],
        transactions: [currentPendingSession, completedExtraPrint, extraPrintCandidate],
      }),
    );
    await fixture.whenStable();

    expect(snackBar.open).toHaveBeenCalledWith(
      'Extra print add-on created. Collect cash, then approve.',
      'Dismiss',
      expect.objectContaining({ panelClass: ['snackbar-success'] }),
    );
  });

  it('does not scan older history when the previous transaction cannot receive extra prints', async () => {
    const fixture = TestBed.createComponent(App);
    rejectSessionRestore();
    const router = TestBed.inject(Router);
    const http = TestBed.inject(HttpTestingController);
    const workspace = TestBed.inject(AdminWorkspace);
    const session = makeSession({ assignedBoothId: 'booth-1', role: 'CLIENT_OWNER' });
    const booth = makeBooth({ id: 'booth-1', name: 'Booth A' });
    const currentPendingSession = makeTransaction({
      boothId: booth.id,
      createdAt: '2026-05-23T00:03:00Z',
      id: 'current-session',
      status: 'PENDING_CASH',
    });
    const previousCoveredSession = makeTransaction({
      boothId: booth.id,
      createdAt: '2026-05-23T00:02:00Z',
      id: 'covered-session',
      status: 'COMPLETED',
      transactionNumber: 'TXN-COVERED',
      transactionType: 'COVERED_PLAN_SESSION',
    });
    const olderEligibleSession = makeTransaction({
      boothId: booth.id,
      canCreateExtraPrintAddOn: true,
      createdAt: '2026-05-23T00:01:00Z',
      extraPrintUnitPriceCents: 5000,
      id: 'older-session',
      status: 'COMPLETED',
      transactionNumber: 'TXN-OLDER',
    });

    workspace.session.set(session);
    workspace.overview.set(
      makeOverview(session, {
        booths: [booth],
        transactions: [currentPendingSession, previousCoveredSession, olderEligibleSession],
      }),
    );

    await router.navigateByUrl('/pos');
    await fixture.whenStable();
    fixture.detectChanges();

    const root = fixture.nativeElement as HTMLElement;
    const extraPrintButton = Array.from(root.querySelectorAll('.pos-booth-actions button')).find(
      (button) => button.textContent?.trim() === 'Extra Print',
    ) as HTMLButtonElement;
    expect(extraPrintButton.disabled).toBe(false);

    extraPrintButton.click();
    fixture.detectChanges();
    await fixture.whenStable();

    const dialog = document.body.querySelector('admin-extra-print-dialog') as HTMLElement;
    expect(dialog).toBeTruthy();
    expect(dialog.textContent).toContain('No eligible transaction for extra print.');
    expect(dialog.textContent).toContain('TXN-COVERED');
    expect(dialog.textContent).not.toContain('TXN-OLDER');

    const createButton = Array.from(dialog.querySelectorAll('button')).find(
      (button) => button.textContent?.trim() === 'Create Extra Prints',
    ) as HTMLButtonElement;
    expect(createButton.disabled).toBe(true);
    http.expectNone(`${apiBaseUrl}/api/cashier/transactions/older-session/extra-prints`);

    const cancelButton = Array.from(dialog.querySelectorAll('button')).find(
      (button) => button.textContent?.trim() === 'Cancel',
    ) as HTMLButtonElement;
    cancelButton.click();
  });

  it('shows assigned booth session activity on Cashier POS', async () => {
    const fixture = TestBed.createComponent(App);
    rejectSessionRestore();
    const router = TestBed.inject(Router);
    const workspace = TestBed.inject(AdminWorkspace);
    const session = makeSession({ assignedBoothId: 'booth-1', role: 'CLIENT_OWNER' });
    const booth = makeBooth({ id: 'booth-1', name: 'Booth A' });
    const otherBooth = makeBooth({ id: 'booth-2', name: 'Booth B' });

    workspace.session.set(session);
    workspace.overview.set(
      makeOverview(session, {
        booths: [booth, otherBooth],
        transactions: [
          makeTransaction({
            boothId: booth.id,
            coveredSessionSequence: 2,
            id: 'covered-session-1',
            offerName: 'Session Pack',
            offerType: 'SESSION_COUNT',
            sessionAllowance: 5,
            status: 'COMPLETED',
            transactionType: 'COVERED_PLAN_SESSION',
          }),
          makeTransaction({
            boothId: booth.id,
            extraPrintCount: 2,
            id: 'extra-print-1',
            offerName: 'Extra Prints',
            status: 'COMPLETED',
            transactionType: 'EXTRA_PRINT_ADD_ON',
          }),
          makeTransaction({
            boothId: otherBooth.id,
            id: 'other-booth-session',
            offerName: 'Other Booth Package',
            status: 'COMPLETED',
          }),
        ],
      }),
    );

    await router.navigateByUrl('/pos');
    await fixture.whenStable();
    fixture.detectChanges();

    const activityCard = fixture.nativeElement.querySelector('.pos-activity-card') as HTMLElement;
    const activityGrid = fixture.debugElement.query(By.directive(AdminGridComponent))
      .componentInstance as AdminGridComponent<TransactionSummary>;

    expect(workspace.cashierActivityFilter()).toBe('ALL');
    expect(activityCard.textContent).toContain('Recent Session Activity');
    expect(activityCard.querySelector('.filter-row')).toBeNull();
    expect(activityGrid.rowData).toEqual([
      expect.objectContaining({
        id: 'covered-session-1',
        transactionType: 'COVERED_PLAN_SESSION',
      }),
      expect.objectContaining({
        id: 'extra-print-1',
        transactionType: 'EXTRA_PRINT_ADD_ON',
      }),
    ]);
    expect(activityGrid.paginationPageSize).toBe(5);
    expect(activityGrid.columnDefs.map((column) => column.headerName)).toEqual([
      'Transaction',
      'Activity',
      'Details',
      'Status',
      'Amount',
      'Created',
    ]);
  });

  it('shows tenant information and opens PayMongo setup from the payment resource row', async () => {
    const fixture = TestBed.createComponent(App);
    rejectSessionRestore();
    const router = TestBed.inject(Router);
    const workspace = TestBed.inject(AdminWorkspace);
    const client = makeClient({ id: 'client-1', name: 'The Memory Box', status: 'ACTIVE' });
    const session = makeSession({ clientAccountId: client.id, role: 'CLIENT_OWNER' });
    const plan = makeSubscriptionPlan({ id: 'plan-1', name: 'Per Booth MVP' });
    const subscription = makeSubscription({
      activeBoothAllowance: 2,
      clientAccountId: client.id,
      status: 'TRIAL',
      subscriptionPlanId: plan.id,
    });

    workspace.session.set(session);
    workspace.overview.set(
      makeOverview(session, {
        booths: [
          makeBooth({ clientAccountId: client.id, id: 'booth-1', status: 'ACTIVE' }),
          makeBooth({ clientAccountId: client.id, id: 'booth-2', status: 'INACTIVE' }),
        ],
        clients: [client],
        subscriptionPlans: [plan],
        subscriptions: [subscription],
        users: [
          makeUser({
            clientAccountId: client.id,
            email: 'owner@memorybox.local',
            id: 'owner-1',
            name: 'Client Owner',
            role: 'CLIENT_OWNER',
          }),
        ],
      }),
    );

    await router.navigateByUrl('/settings');
    await fixture.whenStable();
    fixture.detectChanges();

    const root = fixture.nativeElement as HTMLElement;
    const text = root.textContent ?? '';
    const resourceRows = Array.from(root.querySelectorAll('.payment-resource-row'));
    const toggles = Array.from(root.querySelectorAll('mat-slide-toggle'));
    const ownerFields = Array.from(
      root.querySelectorAll('.tenant-owner-form input'),
    ) as HTMLInputElement[];

    expect(text).toContain('The Memory Box');
    expect(ownerFields.map((field) => field.readOnly)).toEqual([true, true]);
    expect(ownerFields.map((field) => field.value)).toEqual([
      'Client Owner',
      'owner@memorybox.local',
    ]);
    expect(text).toContain('Per Booth MVP');
    expect(text).toContain('1 of 2 active booths used');
    expect(resourceRows.map((row) => row.textContent?.trim())).toEqual([
      expect.stringContaining('Cash'),
      expect.stringContaining('PayMongo QR Ph'),
    ]);
    expect(text).not.toContain('PayMongo Dashboard Setup');
    expect(text).not.toContain('payment.paid, payment.failed, and qrph.expired');
    expect(text).not.toContain('Maya Checkout QR');
    expect(text).not.toContain('Maya Terminal ECR');
    expect(root.querySelector('.payment-resource-icon')?.textContent?.trim()).toBe('PHP');
    expect(toggles).toHaveLength(2);
    expect(toggles[0].textContent).toContain('Enabled');
    expect(toggles[1].textContent).toContain('Disabled');
    expect(toggles[1].querySelector('button')?.hasAttribute('disabled')).toBe(true);

    (resourceRows[1] as HTMLElement).click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(router.url).toBe('/settings/paymongo');
    const payMongoText = root.textContent ?? '';
    expect(payMongoText).toContain('PayMongo Dashboard Setup');
    expect(payMongoText).toContain('Generate PhotoBIZ Webhook URL');
    expect(payMongoText).toContain('Save Step 1 And Generate Webhook URL');
    expect(payMongoText).toContain('Create PayMongo Webhook And Verify');
    expect(payMongoText).toContain('Step 2 is locked until Step 1 is saved with account name');
    expect(payMongoText).toContain('Generate the PhotoBIZ webhook URL first');
    expect(payMongoText).toContain('Leave webhook secret blank for now');
    expect(payMongoText).toContain('PayMongo creates it only after the webhook is created');
    expect(payMongoText).toContain('your-ngrok-domain.ngrok-free.app');
    expect(payMongoText).toContain('payment.paid, payment.failed, and qrph.expired');
    expect(payMongoText).toContain('Return to PhotoBIZ and verify');
  });

  it('updates tenant payment resources through the settings toggle', async () => {
    const workspace = createWorkspaceWithRejectedSession();
    const http = TestBed.inject(HttpTestingController);
    const client = makeClient({ id: 'client-1' });
    const session = makeSession({ clientAccountId: client.id, role: 'CLIENT_OWNER' });

    workspace.session.set(session);
    workspace.overview.set(
      makeOverview(session, {
        clients: [client],
        paymentResources: [
          makePaymentResource({
            clientAccountId: client.id,
            enabled: false,
            paymentMethod: 'PAYMONGO_QRPH',
            status: 'VERIFIED',
          }),
        ],
      }),
    );

    const update = workspace.setPaymentResourceEnabled('PAYMONGO_QRPH', true);
    const request = http.expectOne(`${apiBaseUrl}/api/admin/payment-resources/PAYMONGO_QRPH`);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ enabled: true });
    request.flush(
      makePaymentResource({
        clientAccountId: client.id,
        enabled: true,
        paymentMethod: 'PAYMONGO_QRPH',
        status: 'VERIFIED',
      }),
    );

    await Promise.resolve();
    http.expectOne(`${apiBaseUrl}/api/admin/overview`).flush(
      makeOverview(session, {
        clients: [client],
        paymentResources: [
          makePaymentResource({
            clientAccountId: client.id,
            enabled: true,
            paymentMethod: 'PAYMONGO_QRPH',
            status: 'VERIFIED',
          }),
        ],
      }),
    );
    await update;

    expect(snackBar.open).toHaveBeenCalledWith(
      'Payment resource enabled.',
      'Dismiss',
      expect.objectContaining({ panelClass: ['snackbar-success'] }),
    );
  });

  it('guards and confirms PayMongo setup saves', async () => {
    const fixture = TestBed.createComponent(App);
    rejectSessionRestore();
    const router = TestBed.inject(Router);
    const workspace = TestBed.inject(AdminWorkspace);
    const http = TestBed.inject(HttpTestingController);
    const client = makeClient({ id: 'client-1' });
    const session = makeSession({ clientAccountId: client.id, role: 'CLIENT_OWNER' });

    workspace.session.set(session);
    workspace.overview.set(makeOverview(session, { clients: [client] }));

    await router.navigateByUrl('/settings/paymongo');
    await fixture.whenStable();
    fixture.detectChanges();

    const root = fixture.nativeElement as HTMLElement;
    const stepOneButton = Array.from(root.querySelectorAll('button')).find((button) =>
      button.textContent?.includes('Save Step 1'),
    ) as HTMLButtonElement;
    stepOneButton.click();
    fixture.detectChanges();
    await fixture.whenStable();

    expect(snackBar.open).toHaveBeenCalledWith(
      expect.stringContaining('Complete Step 1 first'),
      'Dismiss',
      expect.objectContaining({ panelClass: ['snackbar-error'] }),
    );
    http.expectNone(`${apiBaseUrl}/api/admin/payment-resources/PAYMONGO_QRPH`);

    workspace.setPayMongoBusinessAccountName('PayMongo Test');
    workspace.setPayMongoPublicKey('pk_test_123');
    workspace.setPayMongoSecretKey('sk_test_123');
    fixture.detectChanges();

    stepOneButton.click();
    fixture.detectChanges();
    await fixture.whenStable();

    const stepOneConfirmation = document.body.querySelector(
      'admin-confirmation-dialog',
    ) as HTMLElement;
    expect(stepOneConfirmation).toBeTruthy();
    expect(stepOneConfirmation.textContent).toContain('Save PayMongo Step 1?');
    const confirmStepOne = Array.from(stepOneConfirmation.querySelectorAll('button')).find(
      (button) => button.textContent?.trim() === 'Save Step 1',
    ) as HTMLButtonElement;
    confirmStepOne.click();
    fixture.detectChanges();
    await fixture.whenStable();
    await new Promise<void>((resolve) => setTimeout(resolve));

    const stepOneRequest = http.expectOne(
      `${apiBaseUrl}/api/admin/payment-resources/PAYMONGO_QRPH`,
    );
    expect(stepOneRequest.request.body).toEqual({
      enabled: true,
      paymentMode: 'test',
      businessAccountName: 'PayMongo Test',
      publicKey: 'pk_test_123',
      secretKey: 'sk_test_123',
      webhookSecret: '',
      verify: false,
    });
    stepOneRequest.flush(
      makePaymentResource({
        clientAccountId: client.id,
        enabled: true,
        paymentMethod: 'PAYMONGO_QRPH',
        resourceId: 'paymongo-config-1',
        status: 'DRAFT',
        businessAccountName: 'PayMongo Test',
        publicKeyMasked: 'pk_test_***_123',
        hasSecretKey: true,
        webhookUrl: 'https://localhost/api/payments/paymongo/webhooks/paymongo-config-1',
      }),
    );
    await Promise.resolve();
    http.expectOne(`${apiBaseUrl}/api/admin/overview`).flush(
      makeOverview(session, {
        clients: [client],
        paymentResources: [
          makePaymentResource({
            clientAccountId: client.id,
            enabled: true,
            paymentMethod: 'PAYMONGO_QRPH',
            resourceId: 'paymongo-config-1',
            status: 'DRAFT',
            businessAccountName: 'PayMongo Test',
            publicKeyMasked: 'pk_test_***_123',
            hasSecretKey: true,
            webhookUrl: 'https://localhost/api/payments/paymongo/webhooks/paymongo-config-1',
          }),
        ],
      }),
    );
    await fixture.whenStable();
    fixture.detectChanges();

    const saveWebhookButton = Array.from(root.querySelectorAll('button')).find((button) =>
      button.textContent?.includes('Save Webhook Secret'),
    ) as HTMLButtonElement;
    saveWebhookButton.click();
    fixture.detectChanges();
    await fixture.whenStable();

    expect(snackBar.open).toHaveBeenCalledWith(
      expect.stringContaining('Paste the PayMongo webhook secret'),
      'Dismiss',
      expect.objectContaining({ panelClass: ['snackbar-error'] }),
    );

    workspace.setPayMongoWebhookSecret('whsec_test');
    fixture.detectChanges();
    saveWebhookButton.click();
    fixture.detectChanges();
    await fixture.whenStable();

    const stepTwoConfirmation = document.body.querySelector(
      'admin-confirmation-dialog',
    ) as HTMLElement;
    expect(stepTwoConfirmation).toBeTruthy();
    expect(stepTwoConfirmation.textContent).toContain('Save PayMongo Webhook Secret?');
  });

  it('shows account actions and opens password changes in a dialog', async () => {
    const fixture = TestBed.createComponent(App);
    rejectSessionRestore();
    const router = TestBed.inject(Router);
    const workspace = TestBed.inject(AdminWorkspace);
    const session = makeSession({ role: 'CLIENT_OWNER' });

    workspace.session.set(session);
    workspace.overview.set(makeOverview(session));

    await router.navigateByUrl('/account');
    await fixture.whenStable();
    fixture.detectChanges();

    const root = fixture.nativeElement as HTMLElement;
    expect(root.textContent).toContain('Account Information');
    expect(root.textContent).toContain('Account Actions');
    expect(root.textContent).toContain('Change the password for the signed-in account.');
    expect(root.textContent).not.toContain('Current password');
    const readonlyFields = Array.from(
      root.querySelectorAll('.account-readonly-form input'),
    ) as HTMLInputElement[];
    expect(readonlyFields.map((field) => field.readOnly)).toEqual([true, true, true]);
    expect(readonlyFields.map((field) => field.value)).toEqual([
      session.name,
      session.email,
      'Client Owner',
    ]);

    const changePasswordButton = Array.from(root.querySelectorAll('button')).find(
      (button) => button.textContent?.trim() === 'Change Password',
    ) as HTMLButtonElement;

    changePasswordButton.click();
    fixture.detectChanges();
    await fixture.whenStable();

    const dialog = document.body.querySelector('admin-change-password-dialog');
    expect(dialog).toBeTruthy();
    expect(document.body.textContent).toContain('Current password');
    expect(document.body.textContent).toContain('New password');
    expect(document.body.textContent).toContain('Confirm new password');
  });

  it('shows friendly package type names in the packages grid', async () => {
    const fixture = TestBed.createComponent(App);
    rejectSessionRestore();
    const router = TestBed.inject(Router);
    const workspace = TestBed.inject(AdminWorkspace);
    const session = makeSession({ role: 'CLIENT_OWNER' });
    const packageOffer = makeOffer({ offerType: 'PER_SESSION' });

    workspace.session.set(session);
    workspace.overview.set(makeOverview(session, { offers: [packageOffer] }));

    await router.navigateByUrl('/packages');
    await fixture.whenStable();
    fixture.detectChanges();

    const page = fixture.debugElement.query(By.directive(PackagesPageComponent))
      .componentInstance as PackagesPageComponent;
    const typeColumn = page.columns.find((column) => column.headerName === 'Type');

    expect(typeof typeColumn?.valueGetter).toBe('function');
    expect(
      typeof typeColumn?.valueGetter === 'function'
        ? typeColumn.valueGetter({ data: packageOffer } as never)
        : null,
    ).toBe('Per Session');
  });

  it('renders package detail status and currency affordances', async () => {
    const fixture = TestBed.createComponent(App);
    rejectSessionRestore();
    const workspace = TestBed.inject(AdminWorkspace);
    const packageOffer = makeOffer({ active: true, currency: 'PHP', offerType: 'PER_SESSION' });
    const session = makeSession({ role: 'CLIENT_OWNER' });

    workspace.session.set(session);
    workspace.overview.set(makeOverview(session, { offers: [packageOffer] }));
    workspace.viewPackage(packageOffer);

    await fixture.whenStable();
    fixture.detectChanges();

    const root = fixture.nativeElement as HTMLElement;
    const statusChip = root.querySelector('.package-detail-status-chip');
    const prefixes = Array.from(root.querySelectorAll('[mattextprefix]')).map((prefix) =>
      prefix.textContent?.trim(),
    );
    const actionBar = root.querySelector('.package-detail-actions');

    expect(statusChip?.textContent?.trim()).toBe('ACTIVE');
    expect(statusChip?.classList.contains('status-chip')).toBe(true);
    expect(statusChip?.classList.contains('active')).toBe(true);
    expect(prefixes).toEqual(['PHP', 'PHP']);
    expect(actionBar?.textContent?.trim().startsWith('Deactivate')).toBe(true);
    expect(root.querySelector('.danger-flat-button')?.textContent?.trim()).toBe('Deactivate');
  });

  it('keeps the new package detail header title on the left', async () => {
    const fixture = TestBed.createComponent(App);
    rejectSessionRestore();
    const workspace = TestBed.inject(AdminWorkspace);
    const session = makeSession({ role: 'CLIENT_OWNER' });

    workspace.session.set(session);
    workspace.overview.set(makeOverview(session));
    workspace.startNewPackage();

    await fixture.whenStable();
    fixture.detectChanges();

    const root = fixture.nativeElement as HTMLElement;
    const header = root.querySelector('.package-detail-header') as HTMLElement | null;
    const title = root.querySelector('.package-detail-title');

    expect(header).not.toBeNull();
    expect(title?.textContent?.trim()).toBe('Package Definition');
    expect(title?.parentElement).toBe(header);
    expect(root.querySelector('.package-detail-status-chip')).toBeNull();
  });

  it('opens tenant print entitlements as a grid modal from packages', async () => {
    const fixture = TestBed.createComponent(App);
    rejectSessionRestore();
    const router = TestBed.inject(Router);
    const workspace = TestBed.inject(AdminWorkspace);
    const session = makeSession({ role: 'CLIENT_OWNER' });
    const inUse = makePrintEntitlement({
      id: 'print-1',
      name: '2 pcs 6x2 or 1 pc 6x4',
    });
    const notUsed = makePrintEntitlement({ id: 'print-2', name: '1 pc 6x4' });

    workspace.session.set(session);
    workspace.overview.set(
      makeOverview(session, {
        offers: [makeOffer({ includedPrintEntitlement: inUse.name })],
        printEntitlements: [inUse, notUsed],
      }),
    );

    await router.navigateByUrl('/packages');
    await fixture.whenStable();
    fixture.detectChanges();

    const root = fixture.nativeElement as HTMLElement;
    const printEntitlementButtons = Array.from(root.querySelectorAll('button')).filter(
      (button) => button.textContent?.trim() === 'Print Entitlements',
    );

    expect(printEntitlementButtons).toHaveLength(1);
    const cardTitles = Array.from(root.querySelectorAll('mat-card-title')).map((title) =>
      title.textContent?.trim(),
    );
    expect(cardTitles).not.toContain('Print Entitlements');
    expect(root.textContent).not.toContain('New Print Entitlement');

    (printEntitlementButtons[0] as HTMLButtonElement).click();
    fixture.detectChanges();
    await fixture.whenStable();

    const dialog = document.body.querySelector('admin-print-entitlements-dialog');
    expect(dialog).toBeTruthy();
    expect(document.body.textContent).toContain('New Print Entitlement');

    const grid = TestBed.createComponent(PrintEntitlementsDialogComponent);
    grid.detectChanges();
    const gridComponent = grid.debugElement.query(By.directive(AdminGridComponent));
    expect(
      (gridComponent.componentInstance as AdminGridComponent<PrintEntitlementSummary>).rowData,
    ).toEqual([inUse, notUsed]);
  });

  it('wraps failed login calls with busy state and snackbar feedback', async () => {
    const fixture = TestBed.createComponent(App);
    rejectSessionRestore();
    const workspace = TestBed.inject(AdminWorkspace);

    workspace.loginEmail.set('owner@example.test');
    workspace.loginPassword.set('bad-password');
    const login = workspace.login();

    expect(workspace.loading()).toBe(true);
    TestBed.inject(HttpTestingController)
      .expectOne(`${apiBaseUrl}/api/auth/login`)
      .flush({ title: 'Invalid credentials' }, { status: 401, statusText: 'Unauthorized' });
    await login;

    expect(workspace.loading()).toBe(false);
    expect(workspace.error()).toBe('Login failed.');
    expect(snackBar.open).toHaveBeenCalledWith(
      'Login failed.',
      'Dismiss',
      expect.objectContaining({ panelClass: ['snackbar-error'] }),
    );
  });

  it('shows success snackbar feedback for client creation', async () => {
    const fixture = TestBed.createComponent(App);
    rejectSessionRestore();
    const workspace = TestBed.inject(AdminWorkspace);
    const session = makeSession({ role: 'APPLICATION_OWNER' });

    workspace.session.set(session);
    workspace.overview.set(makeOverview(session));
    workspace.clientName.set('New Client');

    const createClient = workspace.createClient();

    const http = TestBed.inject(HttpTestingController);
    http.expectOne(`${apiBaseUrl}/api/admin/clients`).flush({});
    await Promise.resolve();
    http
      .expectOne(`${apiBaseUrl}/api/admin/overview`)
      .flush(makeOverview(session, { clients: [makeClient({ name: 'New Client' })] }));
    await createClient;

    expect(snackBar.open).toHaveBeenCalledWith(
      'Client account created.',
      'Dismiss',
      expect.objectContaining({ panelClass: ['snackbar-success'] }),
    );
  });

  it('shows error snackbar feedback for deactivate failures', async () => {
    const fixture = TestBed.createComponent(App);
    rejectSessionRestore();
    const workspace = TestBed.inject(AdminWorkspace);
    const session = makeSession({ role: 'CLIENT_OWNER', userId: 'owner-1' });
    const user = makeUser({ id: 'cashier-1', role: 'CASHIER' });

    workspace.session.set(session);
    workspace.overview.set(makeOverview(session, { users: [user] }));

    const deactivate = workspace.updateUserStatus(user, 'INACTIVE');

    TestBed.inject(HttpTestingController)
      .expectOne(`${apiBaseUrl}/api/admin/users/${user.id}`)
      .flush(
        { detail: 'The user is assigned to an active booth.' },
        { status: 400, statusText: 'Bad Request' },
      );
    await deactivate;

    expect(snackBar.open).toHaveBeenCalledWith(
      'User deactivation failed. The user is assigned to an active booth.',
      'Dismiss',
      expect.objectContaining({ panelClass: ['snackbar-error'] }),
    );
  });

  it('uses print entitlement usage state instead of active/inactive lifecycle text', () => {
    const workspace = createWorkspaceWithRejectedSession();
    const session = makeSession({ role: 'CLIENT_OWNER' });
    const inUse = makePrintEntitlement({ id: 'print-1', name: '2 pcs 6x2' });
    const notUsed = makePrintEntitlement({ id: 'print-2', name: '1 pc 6x4' });

    workspace.session.set(session);
    workspace.overview.set(
      makeOverview(session, {
        offers: [makeOffer({ includedPrintEntitlement: inUse.name })],
        printEntitlements: [inUse, notUsed],
      }),
    );

    expect(workspace.printEntitlementUsageStatus(inUse)).toBe('In Use');
    expect(workspace.printEntitlementUsageStatus(notUsed)).toBe('Not Used');
    expect(workspace.canDeletePrintEntitlement(inUse)).toBe(false);
    expect(workspace.canDeletePrintEntitlement(notUsed)).toBe(true);
  });
});

function createWorkspaceWithRejectedSession(): AdminWorkspace {
  const workspace = TestBed.inject(AdminWorkspace);
  rejectSessionRestore();
  return workspace;
}

function rejectSessionRestore(): void {
  TestBed.inject(HttpTestingController)
    .expectOne(`${apiBaseUrl}/api/auth/session`)
    .flush({}, { status: 401, statusText: 'Unauthorized' });
}

function navLabels(root: HTMLElement): string[] {
  return Array.from(root.querySelectorAll('.nav-item')).map(
    (item) => item.textContent?.trim() ?? '',
  );
}

function makeSession(overrides: Partial<Session> = {}): Session {
  return {
    assignedBoothId: null,
    canApproveCash: true,
    canCancelTransaction: true,
    canReturnBoothToWelcome: true,
    clientAccountId: overrides.role === 'APPLICATION_OWNER' ? null : 'client-1',
    email: 'user@example.test',
    mustChangePassword: false,
    name: 'Test User',
    role: 'CLIENT_OWNER',
    userId: 'user-1',
    ...overrides,
  };
}

function makeOverview(session: Session, overrides: Partial<Overview> = {}): Overview {
  return {
    activations: [],
    appearanceConfigs: [],
    auditLogs: [],
    booths: [],
    clients: [],
    locations: [],
    offers: [],
    paymentAssignments: [],
    paymentResources: [],
    printEntitlements: [],
    reports: makeReports(),
    session,
    subscriptionPlans: [],
    subscriptions: [],
    transactions: [],
    users: [],
    ...overrides,
  };
}

function makeReports(): ReportSummary {
  return {
    boothSales: [],
    locationSales: [],
    offerSales: [],
    platform: {
      activeBooths: 0,
      activeClients: 0,
      activeSubscriptions: 0,
      cancelledSubscriptions: 0,
      clientsOverAllowance: 0,
      manualMrrCents: 0,
      offlineBooths: 0,
      suspendedSubscriptions: 0,
      trialSubscriptions: 0,
    },
    sales: {
      failedOrExpiredCount: 0,
      pendingCashCount: 0,
      todayCashSalesCents: 0,
      todayCompletedSessions: 0,
      todayGrossSalesCents: 0,
    },
  };
}

function makeClient(overrides: Partial<ClientSummary> = {}): ClientSummary {
  return {
    id: 'client-1',
    name: 'Acme Events',
    status: 'ACTIVE',
    ...overrides,
  };
}

function makeSubscriptionPlan(
  overrides: Partial<SubscriptionPlanSummary> = {},
): SubscriptionPlanSummary {
  return {
    active: true,
    currency: 'PHP',
    id: 'plan-1',
    name: 'Per Booth MVP',
    pricePerBoothCents: 200000,
    ...overrides,
  };
}

function makeSubscription(overrides: Partial<SubscriptionSummary> = {}): SubscriptionSummary {
  return {
    activeBoothAllowance: 2,
    clientAccountId: 'client-1',
    id: 'subscription-1',
    status: 'ACTIVE',
    subscriptionPlanId: 'plan-1',
    ...overrides,
  };
}

function makeUser(overrides: Partial<UserSummary> = {}): UserSummary {
  return {
    assignedBoothId: null,
    canApproveCash: true,
    canCancelTransaction: true,
    canReturnBoothToWelcome: true,
    clientAccountId: 'client-1',
    email: 'cashier@example.test',
    id: 'user-2',
    name: 'Cashier User',
    role: 'CASHIER',
    status: 'ACTIVE',
    ...overrides,
  };
}

function makeBooth(overrides: Partial<BoothSummary> = {}): BoothSummary {
  return {
    clientAccountId: 'client-1',
    code: 'SMA-001',
    currentState: 'OFFLINE',
    id: 'booth-1',
    lastHeartbeatAt: null,
    locationId: 'location-1',
    name: 'Booth A',
    status: 'ACTIVE',
    agentStatus: {
      apiReachable: null,
      chromeLaunched: null,
      healthStatus: 'UNKNOWN',
      kioskRunning: false,
      lumaBoothMode: null,
      lumaBoothReachable: null,
      metadataUpdatedAt: null,
      runtimeKind: null,
      triggerListenerRunning: null,
      updateStatus: 'UNKNOWN',
      version: null,
    },
    ...overrides,
  };
}

function makePaymentAssignment(
  overrides: Partial<PaymentAssignmentSummary> = {},
): PaymentAssignmentSummary {
  return {
    boothId: 'booth-1',
    id: 'payment-assignment-1',
    paymentMethod: 'CASH',
    runtimeEnabled: true,
    status: 'ASSIGNED',
    ...overrides,
  };
}

function makeAppearance(overrides: Partial<BoothAppearanceSummary> = {}): BoothAppearanceSummary {
  return {
    accentColor: '#f5d27e',
    backgroundImageDataUrl: null,
    backgroundImageUrl: null,
    boothId: 'booth-1',
    completionThankYouMessage: '',
    defaultWelcomeHeadline: '',
    defaultWelcomeSubtitle: '',
    id: 'appearance-1',
    primaryColor: '#4f2d1d',
    sessionLabel: '',
    themePreset: 'VINTAGE',
    ...overrides,
  };
}

function makePaymentResource(
  overrides: Partial<PaymentResourceSummary> = {},
): PaymentResourceSummary {
  return {
    clientAccountId: 'client-1',
    enabled: true,
    paymentMethod: 'CASH',
    status: 'VERIFIED',
    ...overrides,
  };
}

function makeTransaction(overrides: Partial<TransactionSummary> = {}): TransactionSummary {
  return {
    amountCents: 25000,
    boothId: 'booth-1',
    boothOfferActivationId: null,
    canCreateExtraPrintAddOn: false,
    completedAt: null,
    cancelledAt: null,
    cancelledByActorType: null,
    cancelledByUserId: null,
    cancellationPreviousStatus: null,
    cancellationSource: null,
    coveredSessionSequence: null,
    createdAt: '2026-05-23T00:00:00Z',
    extraPrintCount: 0,
    extraPrintUnitPriceCents: null,
    failureReason: null,
    id: 'transaction-1',
    includedPrintEntitlement: null,
    offerName: 'Per Session',
    offerType: 'PER_SESSION',
    paidAt: null,
    parentTransactionId: null,
    paymentMethod: 'CASH',
    sessionAllowance: null,
    status: 'PENDING_CASH',
    transactionNumber: 'TXN-001',
    transactionType: 'SESSION_PURCHASE',
    ...overrides,
  };
}

function makePrintEntitlement(
  overrides: Partial<PrintEntitlementSummary> = {},
): PrintEntitlementSummary {
  return {
    clientAccountId: 'client-1',
    id: 'print-1',
    name: '2 pcs 6x2',
    ...overrides,
  };
}

function makeOffer(overrides: Partial<OfferSummary> = {}): OfferSummary {
  return {
    active: true,
    allowsExtraPrintAddOn: true,
    clientAccountId: 'client-1',
    currency: 'PHP',
    description: null,
    durationHours: null,
    extraPrintPriceCents: 5000,
    id: 'offer-1',
    includedPrintEntitlement: '2 pcs 6x2',
    lumaboothSessionMode: 'PRINT',
    name: 'Three Sessions',
    offerType: 'SESSION_COUNT',
    priceCents: 100000,
    sessionAllowance: 3,
    ...overrides,
  };
}

function makeActivation(overrides: Partial<OfferActivationSummary> = {}): OfferActivationSummary {
  return {
    boothId: 'booth-1',
    boothOfferId: 'offer-1',
    endsAt: null,
    id: 'activation-1',
    sessionAllowance: null,
    sessionsUsed: 0,
    startsAt: null,
    status: 'ACTIVE',
    ...overrides,
  };
}
