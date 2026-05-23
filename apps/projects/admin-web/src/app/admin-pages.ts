import {
  Component,
  ElementRef,
  HostListener,
  OnDestroy,
  ViewChild,
  computed,
  inject,
  signal,
} from '@angular/core';
import { ComponentType } from '@angular/cdk/portal';
import { MatDialog, MatDialogRef } from '@angular/material/dialog';
import { BoothStageComponent } from '@photobiz/booth-stage';
import { ColDef, ICellRendererParams } from 'ag-grid-community';
import { AdminGridComponent } from './admin-grid.component';
import { AdminUiModule } from './admin-ui.module';
import {
  AdminWorkspace,
  AuditLogSummary,
  BoothSummary,
  ClientSummary,
  LocationSummary,
  OfferSummary,
  PrintEntitlementSummary,
  SubscriptionPlanSummary,
  TransactionSummary,
  UserSummary,
  ViewKey,
} from './admin-workspace.service';

function actionCell<T>(
  label: string,
  action: (row: T) => void,
): (params: ICellRendererParams<T>) => HTMLElement {
  return (params) => {
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'grid-action-button';
    button.textContent = label;
    button.addEventListener('click', () => {
      if (params.data) {
        action(params.data);
      }
    });
    return button;
  };
}

function statusCell<T>(extraClass = ''): (params: ICellRendererParams<T>) => HTMLElement {
  return (params) => {
    const value = String(params.value ?? '');
    const chip = document.createElement('span');
    chip.className = ['status-chip', extraClass, value.toLowerCase().replace(/[^a-z0-9]+/g, '-')]
      .filter(Boolean)
      .join(' ');
    chip.textContent = value;
    return chip;
  };
}

function pageCurrency(cents: number | null | undefined): string {
  return `PHP ${Math.round((cents ?? 0) / 100).toLocaleString('en-PH')}`;
}

function packageOfferTypeLabel(value: string | null | undefined): string {
  switch (value) {
    case 'PER_SESSION':
      return 'Per Session';
    case 'TIME_UNLIMITED':
      return 'Time Unlimited';
    case 'SESSION_COUNT':
      return 'Session Count';
    default:
      return String(value ?? '');
  }
}

function activityGridColumns(workspace: AdminWorkspace): ColDef<TransactionSummary>[] {
  const activityFor = (transaction: TransactionSummary | undefined) =>
    transaction ? workspace.transactionActivityFor(transaction) : null;

  return [
    { field: 'transactionNumber', headerName: 'Transaction', minWidth: 190 },
    {
      headerName: 'Activity',
      minWidth: 170,
      valueGetter: (params) => activityFor(params.data)?.title ?? '',
    },
    {
      headerName: 'Details',
      flex: 2,
      minWidth: 300,
      valueGetter: (params) => activityFor(params.data)?.detail ?? '',
    },
    { field: 'status', headerName: 'Status', cellRenderer: statusCell<TransactionSummary>() },
    {
      headerName: 'Amount',
      minWidth: 130,
      valueGetter: (params) => activityFor(params.data)?.value ?? '',
    },
    {
      field: 'createdAt',
      headerName: 'Created',
      minWidth: 170,
      valueFormatter: (params) => (params.value ? workspace.formatDate(params.value) : ''),
    },
  ];
}

abstract class AdminRoutePage {
  protected readonly w = inject(AdminWorkspace);
  protected readonly dialog = inject(MatDialog);

  protected activate(view: ViewKey): void {
    this.w.activateRouteView(view);
  }

  protected openDialog(component: ComponentType<unknown>, width = '620px'): void {
    this.dialog.open(component, {
      autoFocus: 'dialog',
      maxWidth: 'calc(100vw - 32px)',
      width,
    });
  }
}

@Component({
  selector: 'admin-client-dialog',
  standalone: true,
  imports: [AdminUiModule],
  template: `
    <h2 mat-dialog-title>New Client</h2>
    <mat-dialog-content class="dialog-form">
      <mat-form-field appearance="outline">
        <mat-label>Client name</mat-label>
        <input matInput [ngModel]="w.clientName()" (ngModelChange)="w.clientName.set($event)" />
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Owner name</mat-label>
        <input matInput [ngModel]="w.ownerName()" (ngModelChange)="w.ownerName.set($event)" />
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Owner email</mat-label>
        <input matInput [ngModel]="w.ownerEmail()" (ngModelChange)="w.ownerEmail.set($event)" />
      </mat-form-field>
      <p class="helper-text">Default password will be set to {{ w.defaultInitialPassword }}.</p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close type="button">Cancel</button>
      <button mat-flat-button color="primary" type="button" (click)="save()">Create Client</button>
    </mat-dialog-actions>
  `,
})
export class ClientDialogComponent {
  readonly w = inject(AdminWorkspace);
  private readonly dialogRef = inject(MatDialogRef<ClientDialogComponent>);

  async save(): Promise<void> {
    await this.w.onboardClient();
    this.dialogRef.close();
  }
}

@Component({
  selector: 'admin-subscription-assignment-dialog',
  standalone: true,
  imports: [AdminUiModule],
  template: `
    <h2 mat-dialog-title>Assign Subscription</h2>
    <mat-dialog-content class="dialog-form">
      <mat-form-field appearance="outline">
        <mat-label>Subscription</mat-label>
        <mat-select
          [ngModel]="w.subscriptionPlanId()"
          (ngModelChange)="w.subscriptionPlanId.set($event)"
        >
          @for (plan of w.overview()?.subscriptionPlans ?? []; track plan.id) {
            <mat-option [value]="plan.id">{{ plan.name }}</mat-option>
          } @empty {
            <mat-option disabled>No subscriptions available</mat-option>
          }
        </mat-select>
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Status</mat-label>
        <mat-select
          [ngModel]="w.subscriptionStatus()"
          (ngModelChange)="w.subscriptionStatus.set($event)"
        >
          <mat-option value="TRIAL">Trial</mat-option>
          <mat-option value="ACTIVE">Active</mat-option>
          <mat-option value="SUSPENDED">Suspended</mat-option>
          <mat-option value="CANCELLED">Cancelled</mat-option>
        </mat-select>
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Active booth allowance</mat-label>
        <input
          matInput
          min="0"
          type="number"
          [ngModel]="w.subscriptionAllowance()"
          (ngModelChange)="w.subscriptionAllowance.set(+$event)"
        />
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close type="button">Cancel</button>
      <button mat-flat-button color="primary" type="button" (click)="save()">Assign</button>
    </mat-dialog-actions>
  `,
})
export class SubscriptionAssignmentDialogComponent {
  readonly w = inject(AdminWorkspace);
  private readonly dialogRef = inject(MatDialogRef<SubscriptionAssignmentDialogComponent>);

  async save(): Promise<void> {
    if (await this.w.assignSubscription()) {
      this.dialogRef.close();
    }
  }
}

@Component({
  selector: 'admin-user-dialog',
  standalone: true,
  imports: [AdminUiModule],
  template: `
    <h2 mat-dialog-title>Add User</h2>
    <mat-dialog-content class="dialog-form">
      <mat-form-field appearance="outline">
        <mat-label>Name</mat-label>
        <input matInput [ngModel]="w.newUserName()" (ngModelChange)="w.newUserName.set($event)" />
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Email</mat-label>
        <input matInput [ngModel]="w.newUserEmail()" (ngModelChange)="w.newUserEmail.set($event)" />
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Role</mat-label>
        <mat-select [ngModel]="w.newUserRole()" (ngModelChange)="w.newUserRole.set($event)">
          <mat-option value="CLIENT_ADMIN">Client Admin</mat-option>
          <mat-option value="CASHIER">Cashier</mat-option>
        </mat-select>
      </mat-form-field>
      @if (w.newUserRole() === 'CASHIER') {
        <div class="permission-stack">
          @for (permission of w.cashierPermissionRows; track permission.key) {
            <mat-slide-toggle
              [checked]="w.cashierPermissions()[permission.key]"
              (change)="w.toggleCashierPermission(permission.key)"
            >
              {{ permission.label }}
            </mat-slide-toggle>
          }
        </div>
      }
      <p class="helper-text">Default password will be set to {{ w.defaultInitialPassword }}.</p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close type="button">Cancel</button>
      <button mat-flat-button color="primary" type="button" (click)="save()">Add User</button>
    </mat-dialog-actions>
  `,
})
export class UserDialogComponent {
  readonly w = inject(AdminWorkspace);
  private readonly dialogRef = inject(MatDialogRef<UserDialogComponent>);

  async save(): Promise<void> {
    await this.w.createManagedUser();
    this.dialogRef.close();
  }
}

@Component({
  selector: 'admin-location-dialog',
  standalone: true,
  imports: [AdminUiModule],
  template: `
    <h2 mat-dialog-title>{{ w.selectedLocationDetail() ? 'Manage Location' : 'Add Location' }}</h2>
    <mat-dialog-content class="dialog-form">
      <mat-form-field appearance="outline">
        <mat-label>Location name</mat-label>
        <input
          matInput
          name="locationDetailName"
          [ngModel]="w.locationDetailName()"
          (ngModelChange)="w.locationDetailName.set($event)"
        />
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      @if (w.selectedLocationDetail(); as location) {
        @if (location.status === 'ACTIVE') {
          <button mat-button color="warn" type="button" (click)="deactivate(location)">
            Deactivate
          </button>
        } @else {
          <button mat-button color="primary" type="button" (click)="activate(location)">
            Activate
          </button>
        }
      }
      <button mat-button mat-dialog-close type="button">Cancel</button>
      <button mat-flat-button color="primary" type="button" (click)="save()">Save</button>
    </mat-dialog-actions>
  `,
})
export class LocationDialogComponent {
  readonly w = inject(AdminWorkspace);
  private readonly dialogRef = inject(MatDialogRef<LocationDialogComponent>);

  async save(): Promise<void> {
    if (this.w.selectedLocationDetail()) {
      await this.w.saveLocationDetail();
    } else {
      await this.w.createLocation();
    }
    this.dialogRef.close();
  }

  async deactivate(location: LocationSummary): Promise<void> {
    await this.w.deactivateLocation(location);
    this.dialogRef.close();
  }

  async activate(location: LocationSummary): Promise<void> {
    await this.w.activateLocation(location);
    this.dialogRef.close();
  }
}

@Component({
  selector: 'admin-booth-dialog',
  standalone: true,
  imports: [AdminUiModule],
  template: `
    <h2 mat-dialog-title>Register Booth</h2>
    <mat-dialog-content class="dialog-form">
      <mat-form-field appearance="outline">
        <mat-label>Booth name</mat-label>
        <input matInput [ngModel]="w.boothName()" (ngModelChange)="w.boothName.set($event)" />
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Booth code</mat-label>
        <input matInput [ngModel]="w.boothCode()" (ngModelChange)="w.boothCode.set($event)" />
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Location</mat-label>
        <mat-select [ngModel]="w.boothLocationId()" (ngModelChange)="w.boothLocationId.set($event)">
          @for (location of w.overview()?.locations ?? []; track location.id) {
            <mat-option [value]="location.id">{{ location.name }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>POS staff</mat-label>
        <mat-select
          [ngModel]="w.boothCashierUserId()"
          (ngModelChange)="w.boothCashierUserId.set($event)"
        >
          <mat-option [value]="null">Unassigned</mat-option>
          @for (user of w.availablePosStaff(); track user.id) {
            <mat-option [value]="user.id"
              >{{ user.name }} / {{ w.roleLabel(user.role) }}</mat-option
            >
          }
        </mat-select>
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close type="button">Cancel</button>
      <button mat-flat-button color="primary" type="button" (click)="save()">Register Booth</button>
    </mat-dialog-actions>
  `,
})
export class BoothDialogComponent {
  readonly w = inject(AdminWorkspace);
  private readonly dialogRef = inject(MatDialogRef<BoothDialogComponent>);

  async save(): Promise<void> {
    await this.w.createBooth();
    this.dialogRef.close();
  }
}

@Component({
  selector: 'admin-print-entitlement-dialog',
  standalone: true,
  imports: [AdminUiModule],
  template: `
    <h2 mat-dialog-title>
      {{ w.selectedPrintEntitlement() ? 'Manage Print Entitlement' : 'New Print Entitlement' }}
    </h2>
    <mat-dialog-content class="dialog-form">
      <mat-form-field appearance="outline">
        <mat-label>Print entitlement</mat-label>
        <input
          matInput
          [ngModel]="w.printEntitlementName()"
          (ngModelChange)="w.printEntitlementName.set($event)"
        />
      </mat-form-field>
      @if (w.selectedPrintEntitlement(); as entitlement) {
        <p class="helper-text">{{ w.printEntitlementUsageStatus(entitlement) }}</p>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      @if (w.selectedPrintEntitlement(); as entitlement) {
        <button
          mat-button
          color="warn"
          type="button"
          [disabled]="!w.canDeletePrintEntitlement(entitlement)"
          (click)="delete(entitlement)"
        >
          Delete
        </button>
      }
      <button mat-button mat-dialog-close type="button">Cancel</button>
      <button mat-flat-button color="primary" type="button" (click)="save()">Save</button>
    </mat-dialog-actions>
  `,
})
export class PrintEntitlementDialogComponent {
  readonly w = inject(AdminWorkspace);
  private readonly dialogRef = inject(MatDialogRef<PrintEntitlementDialogComponent>);

  async save(): Promise<void> {
    await this.w.savePrintEntitlement();
    this.dialogRef.close();
  }

  async delete(entitlement: {
    readonly id: string;
    readonly clientAccountId: string;
    readonly name: string;
  }) {
    await this.w.deletePrintEntitlement(entitlement);
    this.dialogRef.close();
  }
}

@Component({
  selector: 'admin-print-entitlements-dialog',
  standalone: true,
  imports: [AdminUiModule, AdminGridComponent],
  template: `
    <h2 mat-dialog-title>Print Entitlements</h2>
    <mat-dialog-content class="dialog-form grid-dialog-content">
      <div class="page-actions">
        <button mat-flat-button color="primary" type="button" (click)="openDetail()">
          New Print Entitlement
        </button>
      </div>
      <admin-grid [rowData]="w.printEntitlements()" [columnDefs]="columns" [pagination]="false" />
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close type="button">Close</button>
    </mat-dialog-actions>
  `,
})
export class PrintEntitlementsDialogComponent {
  readonly w = inject(AdminWorkspace);
  private readonly dialog = inject(MatDialog);
  readonly columns: ColDef<PrintEntitlementSummary>[] = [
    { field: 'name', headerName: 'Print Entitlement', minWidth: 240 },
    {
      headerName: 'Usage',
      valueGetter: (params) =>
        params.data ? this.w.printEntitlementUsageStatus(params.data) : 'Not Used',
      cellRenderer: statusCell<PrintEntitlementSummary>(),
    },
    {
      headerName: 'Actions',
      pinned: 'right',
      width: 120,
      cellRenderer: actionCell<PrintEntitlementSummary>('Manage', (entitlement) =>
        this.openDetail(entitlement),
      ),
    },
  ];

  openDetail(entitlement?: PrintEntitlementSummary): void {
    if (entitlement) {
      this.w.viewPrintEntitlement(entitlement);
    } else {
      this.w.startNewPrintEntitlement();
    }

    this.dialog.open(PrintEntitlementDialogComponent, {
      autoFocus: 'dialog',
      maxWidth: 'calc(100vw - 32px)',
      width: '520px',
    });
  }
}

@Component({
  selector: 'admin-extra-print-dialog',
  standalone: true,
  imports: [AdminUiModule],
  template: `
    <h2 mat-dialog-title>Create Extra Prints</h2>
    <mat-dialog-content class="dialog-form">
      @if (w.extraPrintCandidate(); as candidate) {
        @if (w.transactionActivityFor(candidate); as activity) {
          <div class="readonly-field-grid extra-print-summary-grid">
            <div class="readonly-field">
              <span class="field-label">Session</span>
              <strong>{{ candidate.transactionNumber }}</strong>
              <span>{{ activity.detail }}</span>
            </div>
            <div class="readonly-field">
              <span class="field-label">Original activity</span>
              <strong>{{ activity.title }}</strong>
              <span>{{ activity.auditText }}</span>
            </div>
            <div class="readonly-field">
              <span class="field-label">Included print</span>
              <strong>{{ candidate.includedPrintEntitlement ?? 'None' }}</strong>
            </div>
            <div class="readonly-field">
              <span class="field-label">Add-on unit price</span>
              <strong>{{ w.formatMoney(candidate.extraPrintUnitPriceCents ?? 0) }}</strong>
            </div>
          </div>
        }

        <mat-form-field appearance="outline">
          <mat-label>Copies</mat-label>
          <mat-select
            [ngModel]="w.extraPrintCopies()"
            (ngModelChange)="w.extraPrintCopies.set($event)"
          >
            @for (copy of w.copyOptions(); track copy) {
              <mat-option [value]="copy">{{ copy }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <p class="extra-print-total">Total {{ w.formatMoney(w.extraPrintTotalCents()) }}</p>
      } @else {
        @if (w.extraPrintReferenceTransaction(); as reference) {
          @if (w.transactionActivityFor(reference); as activity) {
            <div class="readonly-field-grid extra-print-summary-grid">
              <div class="readonly-field">
                <span class="field-label">Previous transaction</span>
                <strong>{{ reference.transactionNumber }}</strong>
                <span>{{ activity.detail }}</span>
              </div>
              <div class="readonly-field">
                <span class="field-label">Status</span>
                <strong>{{ reference.status }}</strong>
                <span>This previous transaction is not eligible for extra prints.</span>
              </div>
            </div>
          }
        }
        <p class="empty-state">No eligible transaction for extra print.</p>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close type="button">Cancel</button>
      <button
        mat-flat-button
        color="primary"
        type="button"
        [disabled]="!w.extraPrintCandidate()"
        (click)="save()"
      >
        Create Extra Prints
      </button>
    </mat-dialog-actions>
  `,
})
export class ExtraPrintDialogComponent {
  readonly w = inject(AdminWorkspace);
  private readonly dialogRef = inject(MatDialogRef<ExtraPrintDialogComponent>);

  async save(): Promise<void> {
    const candidate = this.w.extraPrintCandidate();
    if (!candidate) {
      return;
    }

    if (await this.w.createExtraPrintAddOn(candidate.id)) {
      this.dialogRef.close();
    }
  }
}

@Component({
  selector: 'admin-dashboard-page',
  standalone: true,
  imports: [AdminUiModule, AdminGridComponent],
  template: `
    <section class="page-stack">
      <section class="metric-grid">
        @if (w.isApplicationOwner()) {
          <mat-card>
            <span class="metric-label">Active clients</span>
            <strong>{{ w.overview()?.reports?.platform?.activeClients ?? 0 }}</strong>
          </mat-card>
          <mat-card>
            <span class="metric-label">Active booths</span>
            <strong>{{ w.overview()?.reports?.platform?.activeBooths ?? 0 }}</strong>
          </mat-card>
          <mat-card>
            <span class="metric-label">Manual MRR</span>
            <strong>{{
              w.formatMoney(w.overview()?.reports?.platform?.manualMrrCents ?? 0)
            }}</strong>
          </mat-card>
          <mat-card>
            <span class="metric-label">Over allowance</span>
            <strong>{{ w.overview()?.reports?.platform?.clientsOverAllowance ?? 0 }}</strong>
          </mat-card>
        } @else {
          <mat-card>
            <span class="metric-label">Today sales</span>
            <strong>{{
              w.formatMoney(w.overview()?.reports?.sales?.todayGrossSalesCents ?? 0)
            }}</strong>
          </mat-card>
          <mat-card>
            <span class="metric-label">Completed sessions</span>
            <strong>{{ w.overview()?.reports?.sales?.todayCompletedSessions ?? 0 }}</strong>
          </mat-card>
          <mat-card>
            <span class="metric-label">Active booths</span>
            <strong>{{ w.activeBooths().length }}</strong>
          </mat-card>
          <mat-card>
            <span class="metric-label">Pending cash</span>
            <strong>{{ w.pendingTransactions().length }}</strong>
          </mat-card>
        }
      </section>

      <section class="content-grid">
        <mat-card class="dashboard-activity-card">
          <mat-card-header>
            <mat-card-title>Recent Activity</mat-card-title>
          </mat-card-header>
          <mat-card-content>
            <admin-grid
              [rowData]="w.dashboardActivityTransactions()"
              [columnDefs]="activityColumns"
              [paginationPageSize]="5"
              [paginationPageSizeSelector]="[5, 10, 20]"
            />
          </mat-card-content>
        </mat-card>

        <mat-card class="dashboard-booth-status-card">
          <mat-card-header>
            <mat-card-title>Booth Status</mat-card-title>
          </mat-card-header>
          <mat-card-content>
            <div class="activity-list">
              @for (booth of w.overview()?.booths ?? []; track booth.id) {
                <div class="activity-row">
                  <div>
                    <strong>{{ booth.name }}</strong>
                    <span>{{ w.locationNameFor(booth.locationId) }}</span>
                    @if (w.boothPackageStatusFor(booth); as packageStatus) {
                      <span>{{ packageStatus.packageName }} / {{ packageStatus.detail }}</span>
                    }
                  </div>
                  <span [class]="'status-chip ' + w.statusClassFor(booth.currentState)">
                    {{ booth.currentState }}
                  </span>
                </div>
              } @empty {
                <p class="empty-state">No booths yet.</p>
              }
            </div>
          </mat-card-content>
        </mat-card>
      </section>
    </section>
  `,
})
export class DashboardPageComponent extends AdminRoutePage {
  readonly activityColumns = activityGridColumns(this.w);

  constructor() {
    super();
    this.activate('dashboard');
  }
}

@Component({
  selector: 'admin-clients-page',
  standalone: true,
  imports: [AdminUiModule, AdminGridComponent],
  template: `
    <section class="page-stack">
      <div class="page-actions">
        <button mat-flat-button color="primary" type="button" (click)="openDialog(clientDialog)">
          New Client
        </button>
      </div>
      <admin-grid [rowData]="w.platformClients()" [columnDefs]="columns" />
    </section>
  `,
})
export class ClientsPageComponent extends AdminRoutePage {
  readonly clientDialog = ClientDialogComponent;
  readonly columns: ColDef<ClientSummary>[] = [
    { field: 'name', headerName: 'Client' },
    { field: 'status', headerName: 'Status', cellRenderer: statusCell<ClientSummary>() },
    {
      headerName: 'Owner',
      valueGetter: (params) => this.w.ownerForClient(params.data?.id ?? '')?.name ?? 'Unassigned',
    },
    {
      headerName: 'Subscription',
      valueGetter: (params) =>
        this.w.latestSubscriptionFor(params.data?.id ?? '')?.status ?? 'None',
      cellRenderer: statusCell<ClientSummary>('subscription-status-chip'),
    },
    {
      headerName: 'Booths',
      valueGetter: (params) => this.w.boothsForClient(params.data?.id ?? '').length,
      width: 120,
    },
    {
      headerName: 'Actions',
      pinned: 'right',
      width: 120,
      cellRenderer: actionCell<ClientSummary>('Manage', (client) => this.w.viewClient(client)),
    },
  ];

  constructor() {
    super();
    this.activate('clients');
  }
}

@Component({
  selector: 'admin-client-detail-page',
  standalone: true,
  imports: [AdminUiModule],
  template: `
    @if (w.selectedClient(); as client) {
      <section class="page-stack">
        <mat-card class="client-detail-card">
          <mat-card-content class="client-detail-content">
            <div class="client-detail-header">
              <div>
                <h2>{{ client.name }}</h2>
              </div>
              <span
                class="status-chip client-status-chip"
                [class.active]="client.status === 'ACTIVE'"
                [class.suspended]="client.status === 'SUSPENDED'"
                [class.archived]="client.status === 'ARCHIVED'"
              >
                {{ client.status }}
              </span>
            </div>

            <section class="client-detail-grid">
              <section class="detail-section">
                <div class="detail-section-header">
                  <h3>Owner</h3>
                </div>
                <div class="detail-info-stack">
                  <span class="detail-label">Current owner</span>
                  @if (w.ownerForClient(client.id); as owner) {
                    <p>{{ owner.name }}</p>
                    <p class="muted">{{ owner.email }}</p>
                  } @else {
                    <p class="empty-state">No owner assigned.</p>
                  }
                </div>
                <mat-form-field appearance="outline">
                  <mat-label>New owner</mat-label>
                  <mat-select
                    [ngModel]="w.ownerTransferUserId() ?? ''"
                    (ngModelChange)="w.ownerTransferUserId.set($event || null)"
                  >
                    <mat-option value="">No change</mat-option>
                    @for (candidate of w.ownerTransferCandidates(client.id); track candidate.id) {
                      <mat-option [value]="candidate.id"
                        >{{ candidate.name }} / {{ w.roleLabel(candidate.role) }}</mat-option
                      >
                    } @empty {
                      <mat-option disabled>No eligible users</mat-option>
                    }
                  </mat-select>
                </mat-form-field>
                @if (w.ownerTransferTargetFor(client.id); as transferTarget) {
                  <div class="pending-change">
                    <span>Pending owner</span>
                    <strong>{{ transferTarget.name }}</strong>
                    <span>{{ transferTarget.email }}</span>
                  </div>
                }
              </section>

              <section class="detail-section">
                <div class="detail-section-header">
                  <h3>Subscription</h3>
                </div>
                @if (w.latestSubscriptionFor(client.id); as subscription) {
                  <div class="detail-info-stack">
                    <div class="inline-status-row">
                      <p>{{ w.planNameFor(subscription.subscriptionPlanId) }}</p>
                      <span
                        class="status-chip subscription-status-chip"
                        [class.active]="subscription.status === 'ACTIVE'"
                        [class.trial]="subscription.status === 'TRIAL'"
                        [class.suspended]="subscription.status === 'SUSPENDED'"
                        [class.cancelled]="subscription.status === 'CANCELLED'"
                      >
                        {{ subscription.status }}
                      </span>
                    </div>
                    <p class="muted">
                      Allowance {{ subscription.activeBoothAllowance }} active booths
                    </p>
                  </div>
                } @else {
                  <p class="empty-state">No subscription assigned.</p>
                }
                <div class="detail-section-actions">
                  <button
                    mat-flat-button
                    color="primary"
                    class="stretch-action-button"
                    type="button"
                    (click)="openSubscriptionAssignment(client.id)"
                  >
                    Assign Subscription
                  </button>
                </div>
              </section>
            </section>

            <div class="client-detail-footer">
              <div class="detail-action-bar">
                <div class="detail-action-group danger-action-group">
                  @if (client.status === 'SUSPENDED') {
                    <button
                      mat-flat-button
                      color="primary"
                      type="button"
                      (click)="w.updateClientStatus(client, 'ACTIVE')"
                    >
                      Activate Client
                    </button>
                  } @else {
                    <button
                      mat-flat-button
                      class="danger-flat-button"
                      type="button"
                      (click)="w.updateClientStatus(client, 'SUSPENDED')"
                    >
                      Suspend Client
                    </button>
                  }
                  <button
                    mat-button
                    class="danger-link-button"
                    type="button"
                    (click)="w.updateClientStatus(client, 'ARCHIVED')"
                  >
                    Archive
                  </button>
                </div>
                <div class="detail-action-group primary-action-group">
                  <button mat-button type="button" (click)="w.setView('clients')">Back</button>
                  <button
                    mat-flat-button
                    color="primary"
                    type="button"
                    [disabled]="!w.ownerTransferUserId()"
                    (click)="w.transferClientOwner(client.id)"
                  >
                    Save
                  </button>
                </div>
              </div>
            </div>
          </mat-card-content>
        </mat-card>
      </section>
    } @else {
      <p class="empty-state">Select a client from the clients grid.</p>
    }
  `,
})
export class ClientDetailPageComponent extends AdminRoutePage {
  constructor() {
    super();
    this.activate('client-detail');
  }

  openSubscriptionAssignment(clientId: string): void {
    this.w.openSubscriptionModal(clientId);
    this.openDialog(SubscriptionAssignmentDialogComponent);
  }
}

@Component({
  selector: 'admin-subscriptions-page',
  standalone: true,
  imports: [AdminUiModule, AdminGridComponent],
  template: `
    <section class="page-stack">
      <div class="page-actions">
        <button
          mat-flat-button
          color="primary"
          type="button"
          (click)="w.startNewSubscriptionDefinition()"
        >
          Add Subscription
        </button>
      </div>
      <admin-grid [rowData]="w.overview()?.subscriptionPlans ?? []" [columnDefs]="columns" />
    </section>
  `,
})
export class SubscriptionsPageComponent extends AdminRoutePage {
  readonly columns: ColDef<SubscriptionPlanSummary>[] = [
    { field: 'name', headerName: 'Subscription' },
    {
      field: 'pricePerBoothCents',
      headerName: 'Monthly / Booth',
      valueFormatter: (params) => pageCurrency(params.value),
    },
    { field: 'active', headerName: 'Catalog Active' },
    {
      headerName: 'Assigned Clients',
      valueGetter: (params) => this.w.assignedClientCountForSubscription(params.data?.id ?? ''),
    },
    {
      headerName: 'Actions',
      pinned: 'right',
      width: 120,
      cellRenderer: actionCell<SubscriptionPlanSummary>('Manage', (plan) =>
        this.w.viewSubscription(plan),
      ),
    },
  ];

  constructor() {
    super();
    this.activate('subscriptions');
  }
}

@Component({
  selector: 'admin-subscription-detail-page',
  standalone: true,
  imports: [AdminUiModule],
  template: `
    <section class="form-page compact-form-page">
      <mat-card class="form-card">
        <mat-card-header>
          <mat-card-title>Subscription Definition</mat-card-title>
        </mat-card-header>
        <mat-card-content class="form-grid compact-form-grid">
          <mat-form-field appearance="outline">
            <mat-label>Name</mat-label>
            <input matInput [ngModel]="w.planName()" (ngModelChange)="w.planName.set($event)" />
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Monthly price per booth</mat-label>
            <input
              matInput
              type="number"
              [ngModel]="w.planPrice() / 100"
              (ngModelChange)="w.setPlanPriceFromPesos($event)"
            />
          </mat-form-field>
          <mat-slide-toggle
            class="catalog-toggle"
            [checked]="w.subscriptionActive()"
            (change)="w.subscriptionActive.set($event.checked)"
          >
            Active in catalog
          </mat-slide-toggle>
        </mat-card-content>
        <mat-card-actions align="end">
          <button mat-button type="button" (click)="w.setView('subscriptions')">Cancel</button>
          <button
            mat-flat-button
            color="primary"
            type="button"
            (click)="w.saveSubscriptionDefinition()"
          >
            Save
          </button>
        </mat-card-actions>
      </mat-card>
    </section>
  `,
})
export class SubscriptionDetailPageComponent extends AdminRoutePage {
  constructor() {
    super();
    this.activate('subscription-detail');
  }
}

@Component({
  selector: 'admin-users-page',
  standalone: true,
  imports: [AdminUiModule, AdminGridComponent],
  template: `
    <section class="page-stack">
      <div class="page-actions">
        <button mat-flat-button color="primary" type="button" (click)="openDialog(userDialog)">
          Add User
        </button>
      </div>
      <admin-grid [rowData]="w.overview()?.users ?? []" [columnDefs]="columns" />
    </section>
  `,
})
export class UsersPageComponent extends AdminRoutePage {
  readonly userDialog = UserDialogComponent;
  readonly columns: ColDef<UserSummary>[] = [
    { field: 'name', headerName: 'Name' },
    { field: 'email', headerName: 'Email', minWidth: 220 },
    {
      field: 'role',
      headerName: 'Role',
      valueFormatter: (params) => this.w.roleLabel(params.value),
    },
    { field: 'status', headerName: 'Status', cellRenderer: statusCell<UserSummary>() },
    {
      headerName: 'Assigned Booth',
      valueGetter: (params) => this.w.boothNameFor(params.data?.assignedBoothId ?? null),
    },
    {
      headerName: 'Actions',
      pinned: 'right',
      width: 120,
      cellRenderer: actionCell<UserSummary>('Manage', (user) => this.w.viewUser(user)),
    },
  ];

  constructor() {
    super();
    this.activate('users');
  }
}

@Component({
  selector: 'admin-user-detail-page',
  standalone: true,
  imports: [AdminUiModule],
  template: `
    @if (w.selectedUser(); as user) {
      <section class="form-page">
        <mat-card>
          <mat-card-header>
            <mat-card-title>User Details</mat-card-title>
            <mat-card-subtitle>{{ user.email }}</mat-card-subtitle>
          </mat-card-header>
          <mat-card-content class="form-grid">
            <mat-form-field appearance="outline">
              <mat-label>Name</mat-label>
              <input
                matInput
                [ngModel]="w.userDetailName()"
                (ngModelChange)="w.userDetailName.set($event)"
              />
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>Role</mat-label>
              <mat-select
                [disabled]="!w.canEditSelectedUserRole(user)"
                [ngModel]="w.userDetailRole()"
                (ngModelChange)="w.setUserDetailRole($event)"
              >
                <mat-option value="CLIENT_ADMIN">Client Admin</mat-option>
                <mat-option value="CASHIER">Cashier</mat-option>
              </mat-select>
            </mat-form-field>
            @if (w.userDetailRole() === 'CASHIER') {
              <div class="permission-stack">
                @for (permission of w.cashierPermissionRows; track permission.key) {
                  <mat-slide-toggle
                    [checked]="w.userDetailPermissions()[permission.key]"
                    (change)="w.toggleUserDetailPermission(permission.key)"
                  >
                    {{ permission.label }}
                  </mat-slide-toggle>
                }
              </div>
            }
          </mat-card-content>
          <mat-card-actions align="end">
            <button mat-button type="button" (click)="w.setView('users')">Back</button>
            @if (user.status === 'ACTIVE' && w.canDeactivateUser(user)) {
              <button
                mat-button
                color="warn"
                type="button"
                (click)="w.updateUserStatus(user, 'INACTIVE')"
              >
                Deactivate
              </button>
            } @else if (user.status !== 'ACTIVE') {
              <button
                mat-button
                color="primary"
                type="button"
                (click)="w.updateUserStatus(user, 'ACTIVE')"
              >
                Activate
              </button>
            }
            <button mat-flat-button color="primary" type="button" (click)="w.saveUserDetail()">
              Save
            </button>
          </mat-card-actions>
        </mat-card>
      </section>
    } @else {
      <p class="empty-state">Select a user from the users grid.</p>
    }
  `,
})
export class UserDetailPageComponent extends AdminRoutePage {
  constructor() {
    super();
    this.activate('user-detail');
  }
}

@Component({
  selector: 'admin-locations-page',
  standalone: true,
  imports: [AdminUiModule, AdminGridComponent],
  template: `
    <section class="page-stack">
      <div class="page-actions">
        <button mat-flat-button color="primary" type="button" (click)="openLocation()">
          Add Location
        </button>
      </div>
      <admin-grid [rowData]="w.overview()?.locations ?? []" [columnDefs]="columns" />
    </section>
  `,
})
export class LocationsPageComponent extends AdminRoutePage {
  readonly columns: ColDef<LocationSummary>[] = [
    { field: 'name', headerName: 'Location' },
    { field: 'status', headerName: 'Status', cellRenderer: statusCell<LocationSummary>() },
    {
      headerName: 'Booths',
      valueGetter: (params) => this.w.boothCountForLocation(params.data?.id ?? ''),
    },
    {
      headerName: 'Sales',
      valueGetter: (params) => this.w.formatMoney(this.w.locationSalesCents(params.data?.id ?? '')),
    },
    {
      headerName: 'Actions',
      pinned: 'right',
      width: 120,
      cellRenderer: actionCell<LocationSummary>('Manage', (location) =>
        this.openLocation(location),
      ),
    },
  ];

  constructor() {
    super();
    this.activate('locations');
  }

  openLocation(location?: LocationSummary): void {
    this.w.openLocationModal(location);
    this.openDialog(LocationDialogComponent);
  }
}

@Component({
  selector: 'admin-booths-page',
  standalone: true,
  imports: [AdminUiModule, AdminGridComponent],
  template: `
    <section class="page-stack">
      <div class="page-actions">
        <button mat-flat-button color="primary" type="button" (click)="openBooth()">
          Register Booth
        </button>
      </div>
      <admin-grid [rowData]="w.overview()?.booths ?? []" [columnDefs]="columns" />
    </section>
  `,
})
export class BoothsPageComponent extends AdminRoutePage {
  readonly columns: ColDef<BoothSummary>[] = [
    { field: 'name', headerName: 'Booth' },
    { field: 'code', headerName: 'Code', width: 130 },
    {
      headerName: 'Location',
      valueGetter: (params) => this.w.locationNameFor(params.data?.locationId ?? ''),
    },
    { field: 'currentState', headerName: 'Runtime', cellRenderer: statusCell<BoothSummary>() },
    { field: 'status', headerName: 'Lifecycle', cellRenderer: statusCell<BoothSummary>() },
    {
      headerName: 'Package',
      valueGetter: (params) => {
        const status = params.data ? this.w.boothPackageStatusFor(params.data) : null;
        return status
          ? `${status.packageName} / ${status.detail}`
          : this.w.packageStatusLabelFor(params.data?.id ?? '');
      },
      minWidth: 230,
    },
    {
      headerName: 'POS Staff',
      valueGetter: (params) =>
        this.w.assignedPosStaffFor(params.data?.id ?? '')?.name ?? 'Unassigned',
    },
    {
      headerName: 'Actions',
      pinned: 'right',
      width: 120,
      cellRenderer: actionCell<BoothSummary>('Manage', (booth) => this.w.viewBooth(booth)),
    },
  ];

  constructor() {
    super();
    this.activate('booths');
  }

  openBooth(): void {
    this.w.openBoothModal();
    this.openDialog(BoothDialogComponent);
  }
}

@Component({
  selector: 'admin-booth-detail-page',
  standalone: true,
  imports: [AdminUiModule, BoothStageComponent],
  template: `
    @if (w.selectedBoothDetail(); as booth) {
      <section class="page-stack">
        <mat-card class="booth-detail-hero">
          <mat-card-content>
            <div class="booth-detail-header">
              <div>
                <div class="inline-status-row">
                  <h2>{{ booth.name }}</h2>
                  <span
                    class="status-chip"
                    [class.active]="booth.status === 'ACTIVE'"
                    [class.inactive]="booth.status === 'INACTIVE'"
                  >
                    {{ booth.status }}
                  </span>
                </div>
                <div class="booth-meta-row">
                  <span>{{ booth.code }}</span>
                  <span [class]="'status-chip ' + w.statusClassFor(booth.currentState)">
                    {{ booth.currentState }}
                  </span>
                </div>
              </div>
              <div class="detail-action-group primary-action-group">
                <button mat-button type="button" (click)="w.setView('booths')">Back</button>
                @if (booth.status === 'ACTIVE') {
                  <button
                    mat-button
                    class="danger-link-button"
                    type="button"
                    (click)="w.updateBoothStatus(booth, 'INACTIVE')"
                  >
                    Deactivate
                  </button>
                } @else {
                  <button
                    mat-flat-button
                    color="primary"
                    type="button"
                    (click)="w.updateBoothStatus(booth, 'ACTIVE')"
                  >
                    Activate
                  </button>
                }
              </div>
            </div>
          </mat-card-content>
        </mat-card>

        <mat-card class="credential-card">
          <mat-card-header>
            <mat-card-title>One-time credentials</mat-card-title>
            <button mat-button color="primary" type="button" (click)="w.issueBoothCredentials()">
              Re-issue Credentials
            </button>
          </mat-card-header>
          <mat-card-content>
            @if (w.selectedBoothSecret(); as secret) {
              <div class="credential-grid">
                <div class="credential-item">
                  <span>Kiosk token</span>
                  <code>{{ secret.kioskToken }}</code>
                </div>
                <div class="credential-item">
                  <span>Agent credential</span>
                  <code>{{ secret.agentCredential }}</code>
                </div>
              </div>
            } @else {
              <p class="empty-state">Credentials appear here only immediately after issuing.</p>
            }
          </mat-card-content>
        </mat-card>

        <mat-tab-group [selectedIndex]="w.boothDetailTab() === 'details' ? 0 : 1">
          <mat-tab label="Details">
            <section class="content-grid tab-panel">
              <mat-card>
                <mat-card-content class="form-grid">
                  <mat-form-field appearance="outline">
                    <mat-label>Name</mat-label>
                    <input
                      matInput
                      [ngModel]="w.boothDetailName()"
                      (ngModelChange)="w.boothDetailName.set($event)"
                    />
                  </mat-form-field>
                  <mat-form-field appearance="outline">
                    <mat-label>Code</mat-label>
                    <input
                      matInput
                      [ngModel]="w.boothDetailCode()"
                      (ngModelChange)="w.boothDetailCode.set($event)"
                    />
                  </mat-form-field>
                  <mat-form-field appearance="outline">
                    <mat-label>Location</mat-label>
                    <mat-select
                      [ngModel]="w.boothDetailLocationId()"
                      (ngModelChange)="w.boothDetailLocationId.set($event)"
                    >
                      @for (location of w.overview()?.locations ?? []; track location.id) {
                        <mat-option [value]="location.id">{{ location.name }}</mat-option>
                      }
                    </mat-select>
                  </mat-form-field>
                  <mat-form-field appearance="outline">
                    <mat-label>POS staff</mat-label>
                    <mat-select
                      [ngModel]="w.boothDetailCashierUserId()"
                      (ngModelChange)="w.boothDetailCashierUserId.set($event)"
                    >
                      <mat-option [value]="null">Unassigned</mat-option>
                      @for (user of w.boothDetailPosStaffOptions(); track user.id) {
                        <mat-option [value]="user.id">{{ user.name }}</mat-option>
                      }
                    </mat-select>
                  </mat-form-field>
                  <mat-form-field appearance="outline">
                    <mat-label>Active package</mat-label>
                    <mat-select
                      [ngModel]="w.boothDetailOfferId()"
                      (ngModelChange)="w.boothDetailOfferId.set($event)"
                    >
                      @for (offer of w.activeOffers(); track offer.id) {
                        <mat-option [value]="offer.id">{{ offer.name }}</mat-option>
                      }
                    </mat-select>
                  </mat-form-field>
                </mat-card-content>
                <mat-card-actions align="end">
                  <button
                    mat-flat-button
                    color="primary"
                    type="button"
                    (click)="w.saveBoothDetails()"
                  >
                    Save
                  </button>
                </mat-card-actions>
              </mat-card>

              <mat-card>
                <mat-card-header>
                  <mat-card-title>Payment Assignment</mat-card-title>
                </mat-card-header>
                <mat-card-content class="payment-option-list">
                  <div class="payment-option-row">
                    <div>
                      <strong>Cash</strong>
                      <span>{{
                        w.cashAssignmentFor(booth.id)?.runtimeEnabled ? 'Enabled' : 'Disabled'
                      }}</span>
                    </div>
                    <mat-slide-toggle
                      [checked]="w.cashAssignmentFor(booth.id)?.runtimeEnabled ?? false"
                      (change)="w.setCashPaymentEnabled(booth.id, $event.checked)"
                    />
                  </div>
                </mat-card-content>
              </mat-card>
            </section>
          </mat-tab>
          <mat-tab label="Session Setup">
            <section class="session-setup-grid tab-panel">
              <mat-card>
                <mat-card-content class="form-grid">
                  <mat-form-field appearance="outline">
                    <mat-label>Session label</mat-label>
                    <input
                      matInput
                      [ngModel]="w.boothAppearanceSessionLabel()"
                      (ngModelChange)="w.boothAppearanceSessionLabel.set($event)"
                    />
                  </mat-form-field>
                  <mat-form-field appearance="outline">
                    <mat-label>Welcome headline</mat-label>
                    <input
                      matInput
                      [ngModel]="w.boothAppearanceHeadline()"
                      (ngModelChange)="w.boothAppearanceHeadline.set($event)"
                    />
                  </mat-form-field>
                  <mat-form-field appearance="outline">
                    <mat-label>Welcome subtitle</mat-label>
                    <textarea
                      matInput
                      rows="3"
                      [ngModel]="w.boothAppearanceSubtitle()"
                      (ngModelChange)="w.boothAppearanceSubtitle.set($event)"
                    ></textarea>
                  </mat-form-field>
                  <mat-form-field appearance="outline">
                    <mat-label>Completion thank-you message</mat-label>
                    <textarea
                      matInput
                      rows="2"
                      [ngModel]="w.boothAppearanceCompletionMessage()"
                      (ngModelChange)="w.boothAppearanceCompletionMessage.set($event)"
                    ></textarea>
                  </mat-form-field>
                  <mat-form-field appearance="outline">
                    <mat-label>Theme</mat-label>
                    <mat-select
                      [ngModel]="w.boothAppearanceThemePreset()"
                      (ngModelChange)="w.boothAppearanceThemePreset.set($event)"
                    >
                      @for (preset of w.boothThemePresets; track preset.value) {
                        <mat-option [value]="preset.value">{{ preset.label }}</mat-option>
                      }
                    </mat-select>
                  </mat-form-field>
                  <input
                    type="file"
                    accept="image/png,image/jpeg,image/webp"
                    (change)="w.setBoothBackgroundImageFromFile($event)"
                  />
                </mat-card-content>
                <mat-card-actions class="session-setup-actions">
                  <button
                    mat-stroked-button
                    type="button"
                    (click)="w.resetBoothSessionToThemeDefaults()"
                  >
                    Reset to theme defaults
                  </button>
                  <div class="session-primary-actions">
                    <button mat-button type="button" (click)="w.clearBoothBackgroundImage()">
                      Clear Image
                    </button>
                    <button
                      mat-flat-button
                      color="primary"
                      type="button"
                      (click)="w.saveBoothSession()"
                    >
                      Save
                    </button>
                  </div>
                </mat-card-actions>
              </mat-card>

              <mat-card>
                <mat-card-header>
                  <mat-card-title>Booth UI Preview</mat-card-title>
                </mat-card-header>
                <mat-card-content>
                  <section
                    #boothPreviewShell
                    class="booth-preview-shell"
                    [class.booth-preview-shell-fullscreen]="boothPreviewFullscreen()"
                  >
                    <div class="booth-preview-toolbar">
                      <mat-form-field appearance="outline">
                        <mat-label>Preview state</mat-label>
                        <mat-select
                          [ngModel]="w.boothPreviewScreenKey()"
                          (ngModelChange)="w.setBoothPreviewScreen($event)"
                        >
                          @for (screen of w.boothPreviewScreens; track screen.key) {
                            <mat-option [value]="screen.key">{{ screen.label }}</mat-option>
                          }
                        </mat-select>
                      </mat-form-field>
                      <button
                        mat-stroked-button
                        type="button"
                        class="booth-preview-fullscreen-button"
                        (click)="toggleBoothPreviewFullscreen()"
                        [attr.aria-pressed]="boothPreviewFullscreen()"
                      >
                        {{ boothPreviewFullscreen() ? 'Exit full screen' : 'Full screen' }}
                      </button>
                    </div>
                    @if (w.selectedBoothPreviewConfig(); as config) {
                      <div class="booth-preview-frame">
                        <photobiz-booth-stage
                          [config]="config"
                          [screen]="w.selectedBoothPreviewScreen().screen"
                        />
                      </div>
                    }
                  </section>
                </mat-card-content>
              </mat-card>
            </section>
          </mat-tab>
        </mat-tab-group>
      </section>
    } @else {
      <p class="empty-state">Select a booth from the booths grid.</p>
    }
  `,
})
export class BoothDetailPageComponent extends AdminRoutePage implements OnDestroy {
  @ViewChild('boothPreviewShell') private boothPreviewShell?: ElementRef<HTMLElement>;

  protected readonly boothPreviewFullscreen = signal(false);

  constructor() {
    super();
    this.activate('booth-detail');
  }

  @HostListener('document:fullscreenchange')
  protected onFullscreenChange(): void {
    this.setBoothPreviewFullscreen(
      document.fullscreenElement === this.boothPreviewShell?.nativeElement,
    );
  }

  @HostListener('document:keydown.escape')
  protected onPreviewEscape(): void {
    if (
      this.boothPreviewFullscreen() &&
      document.fullscreenElement !== this.boothPreviewShell?.nativeElement
    ) {
      this.setBoothPreviewFullscreen(false);
    }
  }

  ngOnDestroy(): void {
    this.setBoothPreviewFullscreen(false);
  }

  protected async toggleBoothPreviewFullscreen(): Promise<void> {
    const previewShell = this.boothPreviewShell?.nativeElement;

    if (!previewShell) {
      return;
    }

    if (this.boothPreviewFullscreen() && document.fullscreenElement !== previewShell) {
      this.setBoothPreviewFullscreen(false);
      return;
    }

    try {
      if (document.fullscreenElement === previewShell) {
        await document.exitFullscreen();
        this.setBoothPreviewFullscreen(false);
        return;
      }

      if (typeof previewShell.requestFullscreen !== 'function') {
        this.setBoothPreviewFullscreen(true);
        return;
      }

      await previewShell.requestFullscreen();
      this.setBoothPreviewFullscreen(true);
    } catch {
      this.setBoothPreviewFullscreen(true);
    }
  }

  private setBoothPreviewFullscreen(active: boolean): void {
    this.boothPreviewFullscreen.set(active);
    document.body.classList.toggle('booth-preview-fullscreen-active', active);
  }
}

@Component({
  selector: 'admin-packages-page',
  standalone: true,
  imports: [AdminUiModule, AdminGridComponent],
  template: `
    <section class="page-stack">
      <div class="page-actions">
        <button mat-button type="button" (click)="openPrintEntitlements()">
          Print Entitlements
        </button>
        <button mat-flat-button color="primary" type="button" (click)="w.startNewPackage()">
          New Package
        </button>
      </div>
      <admin-grid [rowData]="w.overview()?.offers ?? []" [columnDefs]="columns" />
    </section>
  `,
})
export class PackagesPageComponent extends AdminRoutePage {
  readonly columns: ColDef<OfferSummary>[] = [
    { field: 'name', headerName: 'Package' },
    {
      field: 'offerType',
      headerName: 'Type',
      valueGetter: (params) => packageOfferTypeLabel(params.data?.offerType),
    },
    {
      field: 'priceCents',
      headerName: 'Price',
      valueFormatter: (params) => pageCurrency(params.value),
    },
    { field: 'includedPrintEntitlement', headerName: 'Print Entitlement', minWidth: 220 },
    {
      field: 'active',
      headerName: 'Status',
      valueGetter: (params) => (params.data?.active ? 'ACTIVE' : 'INACTIVE'),
      cellRenderer: statusCell<OfferSummary>(),
    },
    {
      headerName: 'Add-On Print Price',
      valueGetter: (params) =>
        params.data?.extraPrintPriceCents
          ? pageCurrency(params.data.extraPrintPriceCents)
          : 'Not available',
    },
    {
      headerName: 'Actions',
      pinned: 'right',
      width: 120,
      cellRenderer: actionCell<OfferSummary>('Manage', (offer) => this.w.viewPackage(offer)),
    },
  ];

  constructor() {
    super();
    this.activate('packages');
  }

  openPrintEntitlements(): void {
    this.openDialog(PrintEntitlementsDialogComponent, '820px');
  }
}

@Component({
  selector: 'admin-package-detail-page',
  standalone: true,
  imports: [AdminUiModule],
  template: `
    <section class="form-page">
      <mat-card>
        <mat-card-header class="package-detail-header">
          <div class="package-detail-title">
            <mat-card-title>Package Definition</mat-card-title>
          </div>
          @if (w.selectedPackage(); as selectedPackage) {
            <span
              class="status-chip package-detail-status-chip"
              [class.active]="selectedPackage.active"
              [class.inactive]="!selectedPackage.active"
            >
              {{ selectedPackage.active ? 'ACTIVE' : 'INACTIVE' }}
            </span>
          }
        </mat-card-header>
        <mat-card-content class="form-grid">
          <mat-form-field appearance="outline">
            <mat-label>Package name</mat-label>
            <input
              matInput
              name="packageName"
              [ngModel]="w.packageName()"
              (ngModelChange)="w.packageName.set($event)"
            />
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Price</mat-label>
            <span matTextPrefix>{{ w.packageCurrency() }}&nbsp;</span>
            <input
              matInput
              name="packagePrice"
              type="number"
              [ngModel]="w.packagePriceCents() / 100"
              (ngModelChange)="w.setPackagePriceFromPesos($event)"
            />
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Description</mat-label>
            <textarea
              matInput
              rows="3"
              [ngModel]="w.packageDescription()"
              (ngModelChange)="w.packageDescription.set($event)"
            ></textarea>
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Type</mat-label>
            <mat-select
              [ngModel]="w.packageOfferType()"
              (ngModelChange)="w.packageOfferType.set($event)"
            >
              <mat-option value="PER_SESSION">Per Session</mat-option>
              <mat-option value="TIME_UNLIMITED">Time Unlimited</mat-option>
              <mat-option value="SESSION_COUNT">Session Count</mat-option>
            </mat-select>
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Print entitlement</mat-label>
            <mat-select
              [ngModel]="w.packagePrintEntitlement()"
              (ngModelChange)="w.packagePrintEntitlement.set($event)"
            >
              @for (entitlement of w.packagePrintEntitlementOptions(); track entitlement) {
                <mat-option [value]="entitlement">{{ entitlement }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>LumaBooth mode</mat-label>
            <mat-select
              [ngModel]="w.packageLumaBoothMode()"
              (ngModelChange)="w.packageLumaBoothMode.set($event)"
            >
              <mat-option value="PRINT">Print</mat-option>
              <mat-option value="GIF">GIF</mat-option>
              <mat-option value="BOOMERANG">Boomerang</mat-option>
              <mat-option value="VIDEO">Video</mat-option>
            </mat-select>
          </mat-form-field>
          @if (w.packageOfferType() === 'PER_SESSION') {
            <mat-form-field appearance="outline">
              <mat-label>Add-on print price</mat-label>
              <span matTextPrefix>{{ w.packageCurrency() }}&nbsp;</span>
              <input
                matInput
                type="number"
                [ngModel]="w.packageExtraPrintPriceCents() / 100"
                (ngModelChange)="w.setPackageExtraPrintPriceFromPesos($event)"
              />
            </mat-form-field>
          } @else if (w.packageOfferType() === 'TIME_UNLIMITED') {
            <mat-form-field appearance="outline">
              <mat-label>Duration hours</mat-label>
              <input
                matInput
                type="number"
                [ngModel]="w.packageDurationHours()"
                (ngModelChange)="w.packageDurationHours.set(+$event)"
              />
            </mat-form-field>
          } @else {
            <mat-form-field appearance="outline">
              <mat-label>Session allowance</mat-label>
              <input
                matInput
                type="number"
                [ngModel]="w.packageSessionAllowance()"
                (ngModelChange)="w.packageSessionAllowance.set(+$event)"
              />
            </mat-form-field>
          }
        </mat-card-content>
        <mat-card-actions class="detail-action-bar package-detail-actions">
          <div class="detail-action-group danger-action-group">
            @if (w.selectedPackage(); as selectedPackage) {
              <button
                mat-flat-button
                type="button"
                [color]="selectedPackage.active ? null : 'primary'"
                [class.danger-flat-button]="selectedPackage.active"
                (click)="w.updatePackageStatus(selectedPackage, !selectedPackage.active)"
              >
                {{ selectedPackage.active ? 'Deactivate' : 'Activate' }}
              </button>
            }
          </div>
          <div class="detail-action-group primary-action-group">
            <button mat-button type="button" (click)="w.setView('packages')">Cancel</button>
            <button mat-flat-button color="primary" type="button" (click)="w.savePackage()">
              Save
            </button>
          </div>
        </mat-card-actions>
      </mat-card>
    </section>
  `,
})
export class PackageDetailPageComponent extends AdminRoutePage {
  constructor() {
    super();
    this.activate('package-detail');
  }
}

@Component({
  selector: 'admin-transactions-page',
  standalone: true,
  imports: [AdminUiModule, AdminGridComponent],
  template: `
    <section class="page-stack">
      <admin-grid [rowData]="w.overview()?.transactions ?? []" [columnDefs]="columns" />
    </section>
  `,
})
export class TransactionsPageComponent extends AdminRoutePage {
  readonly columns: ColDef<TransactionSummary>[] = [
    { field: 'transactionNumber', headerName: 'Transaction', minWidth: 190 },
    { field: 'status', headerName: 'Status', cellRenderer: statusCell<TransactionSummary>() },
    { field: 'transactionType', headerName: 'Type', minWidth: 180 },
    { field: 'offerName', headerName: 'Package' },
    {
      field: 'amountCents',
      headerName: 'Amount',
      valueFormatter: (params) => pageCurrency(params.value),
    },
    {
      headerName: 'Booth',
      valueGetter: (params) => this.w.boothNameFor(params.data?.boothId ?? null),
    },
    {
      headerName: 'Cancelled By',
      minWidth: 210,
      valueGetter: (params) => (params.data ? this.w.cancellationDetailFor(params.data) : ''),
    },
    {
      field: 'cancelledAt',
      headerName: 'Cancelled',
      valueFormatter: (params) => (params.value ? this.w.formatDate(params.value) : ''),
      minWidth: 170,
    },
    {
      field: 'createdAt',
      headerName: 'Created',
      valueFormatter: (params) => (params.value ? this.w.formatDate(params.value) : ''),
      minWidth: 170,
    },
  ];

  constructor() {
    super();
    this.activate('transactions');
  }
}

@Component({
  selector: 'admin-pos-page',
  standalone: true,
  imports: [AdminUiModule, AdminGridComponent],
  template: `
    <section class="page-stack">
      <section class="content-grid">
        <mat-card>
          <mat-card-header>
            <mat-card-title>Assigned Booth</mat-card-title>
          </mat-card-header>
          <mat-card-content>
            @if (w.assignedBooth(); as booth) {
              <div class="pos-booth-summary">
                <div class="pos-booth-header">
                  <div>
                    <span class="field-label">Booth</span>
                    <h3>{{ booth.name }}</h3>
                  </div>
                  <span [class]="'status-chip ' + w.statusClassFor(booth.currentState)">
                    {{ booth.currentState }}
                  </span>
                </div>

                <div class="readonly-field-grid">
                  <div class="readonly-field">
                    <span class="field-label">Package</span>
                    @if (w.boothPackageStatusFor(booth); as packageStatus) {
                      <strong>{{ packageStatus.packageName }}</strong>
                      <span>{{ packageStatus.detail }}</span>
                    } @else {
                      <strong>None</strong>
                    }
                  </div>
                  <div class="readonly-field">
                    <span class="field-label">Payment Options</span>
                    <strong>{{ w.paymentLabelFor(booth.id) }}</strong>
                  </div>
                </div>

                <mat-divider />

                <div class="pos-booth-actions">
                  <button
                    mat-stroked-button
                    type="button"
                    (click)="w.returnBoothToWelcome(booth.id)"
                  >
                    Return to Welcome
                  </button>
                  <button
                    mat-flat-button
                    color="primary"
                    type="button"
                    (click)="openExtraPrintDialog()"
                  >
                    Extra Print
                  </button>
                </div>
              </div>
            } @else {
              <p class="empty-state">No assigned booth.</p>
            }
          </mat-card-content>
        </mat-card>

        <mat-card>
          <mat-card-header>
            <mat-card-title>Current Payment Request</mat-card-title>
          </mat-card-header>
          <mat-card-content>
            @if (w.cashierTransaction(); as transaction) {
              <p>{{ transaction.transactionNumber }} / {{ transaction.status }}</p>
              <p>{{ w.formatMoney(transaction.amountCents) }}</p>
              <button
                mat-flat-button
                color="primary"
                type="button"
                [disabled]="!w.canApproveCashAction()"
                (click)="w.approveCash(transaction.id)"
              >
                Approve Cash
              </button>
              <button
                mat-button
                color="warn"
                type="button"
                [disabled]="!w.canCancelTransactionAction()"
                (click)="w.cancelTransaction(transaction.id)"
              >
                Cancel Transaction
              </button>
            } @else if (w.pendingPlanActivation(); as activation) {
              <p>Package activation for {{ w.pendingPlanActivationOffer()?.name }}</p>
              @if (w.assignedBooth(); as booth) {
                <button
                  mat-flat-button
                  color="primary"
                  type="button"
                  (click)="w.createPlanActivation(booth.id)"
                >
                  Activate Package
                </button>
              }
            } @else {
              <p class="empty-state">No active payment request.</p>
            }
          </mat-card-content>
        </mat-card>
      </section>

      <mat-card class="pos-activity-card">
        <mat-card-header>
          <mat-card-title>Recent Session Activity</mat-card-title>
        </mat-card-header>
        <mat-card-content>
          <admin-grid
            [rowData]="w.cashierActivityTransactions()"
            [columnDefs]="activityColumns"
            [paginationPageSize]="5"
            [paginationPageSizeSelector]="[5, 10, 20]"
          />
        </mat-card-content>
      </mat-card>
    </section>
  `,
})
export class PosPageComponent extends AdminRoutePage {
  readonly activityColumns = activityGridColumns(this.w);

  constructor() {
    super();
    this.activate('pos');
  }

  openExtraPrintDialog(): void {
    this.w.extraPrintCopies.set(1);
    this.openDialog(ExtraPrintDialogComponent, '620px');
  }
}

@Component({
  selector: 'admin-reports-page',
  standalone: true,
  imports: [AdminUiModule],
  template: `
    <section class="page-stack">
      <section class="metric-grid">
        <mat-card>
          <span class="metric-label">Today sales</span>
          <strong>{{
            w.formatMoney(w.overview()?.reports?.sales?.todayGrossSalesCents ?? 0)
          }}</strong>
        </mat-card>
        <mat-card>
          <span class="metric-label">Cash sales</span>
          <strong>{{
            w.formatMoney(w.overview()?.reports?.sales?.todayCashSalesCents ?? 0)
          }}</strong>
        </mat-card>
        <mat-card>
          <span class="metric-label">Completed sessions</span>
          <strong>{{ w.overview()?.reports?.sales?.todayCompletedSessions ?? 0 }}</strong>
        </mat-card>
        <mat-card>
          <span class="metric-label">Failed or expired</span>
          <strong>{{ w.overview()?.reports?.sales?.failedOrExpiredCount ?? 0 }}</strong>
        </mat-card>
      </section>
      <section class="content-grid">
        <mat-card>
          <mat-card-header><mat-card-title>Booth Sales</mat-card-title></mat-card-header>
          <mat-card-content>
            <div class="activity-list">
              @for (row of w.overview()?.reports?.boothSales ?? []; track row.boothId) {
                <div class="activity-row">
                  <span>{{ row.boothName }}</span>
                  <strong>{{ w.formatMoney(row.grossSalesCents) }}</strong>
                </div>
              } @empty {
                <p class="empty-state">No booth sales yet.</p>
              }
            </div>
          </mat-card-content>
        </mat-card>
        <mat-card>
          <mat-card-header><mat-card-title>Package Sales</mat-card-title></mat-card-header>
          <mat-card-content>
            <div class="activity-list">
              @for (row of w.overview()?.reports?.offerSales ?? []; track row.offerId) {
                <div class="activity-row">
                  <span>{{ row.offerName }}</span>
                  <strong>{{ row.completedSessions }} sessions</strong>
                </div>
              } @empty {
                <p class="empty-state">No package sales yet.</p>
              }
            </div>
          </mat-card-content>
        </mat-card>
      </section>
    </section>
  `,
})
export class ReportsPageComponent extends AdminRoutePage {
  constructor() {
    super();
    this.activate('reports');
  }
}

@Component({
  selector: 'admin-settings-page',
  standalone: true,
  imports: [AdminUiModule],
  template: `
    <section class="page-stack">
      @if (w.currentClient(); as client) {
        <mat-card class="tenant-info-card">
          <mat-card-content class="client-detail-content">
            <div class="client-detail-header">
              <div>
                <h2>{{ client.name }}</h2>
              </div>
              <span
                class="status-chip client-status-chip"
                [class.active]="client.status === 'ACTIVE'"
                [class.suspended]="client.status === 'SUSPENDED'"
                [class.archived]="client.status === 'ARCHIVED'"
              >
                {{ client.status }}
              </span>
            </div>

            <section class="client-detail-grid settings-tenant-grid">
              <section class="detail-section">
                <div class="detail-section-header">
                  <h3>Owner</h3>
                </div>
                @if (w.currentClientOwner(); as owner) {
                  <div class="account-readonly-form tenant-owner-form">
                    <mat-form-field appearance="outline">
                      <mat-label>Name</mat-label>
                      <input matInput readonly [value]="owner.name" />
                    </mat-form-field>
                    <mat-form-field appearance="outline">
                      <mat-label>Email</mat-label>
                      <input matInput readonly [value]="owner.email" />
                    </mat-form-field>
                  </div>
                } @else {
                  <p class="empty-state">No owner assigned.</p>
                }
              </section>

              <section class="detail-section">
                <div class="detail-section-header">
                  <h3>Subscription</h3>
                </div>
                @if (w.currentClientSubscription(); as subscription) {
                  <div class="detail-info-stack">
                    <div class="inline-status-row">
                      <p>{{ w.currentClientSubscriptionPlan()?.name ?? 'Unknown subscription' }}</p>
                      <span
                        class="status-chip subscription-status-chip"
                        [class.active]="subscription.status === 'ACTIVE'"
                        [class.trial]="subscription.status === 'TRIAL'"
                        [class.suspended]="subscription.status === 'SUSPENDED'"
                        [class.cancelled]="subscription.status === 'CANCELLED'"
                      >
                        {{ subscription.status }}
                      </span>
                    </div>
                    <p class="muted">
                      {{ w.currentClientActiveBoothCount() }} of
                      {{ subscription.activeBoothAllowance }} active booths used
                    </p>
                  </div>
                } @else {
                  <p class="empty-state">No subscription assigned.</p>
                }
              </section>
            </section>
          </mat-card-content>
        </mat-card>
      }

      <mat-card>
        <mat-card-header>
          <mat-card-title>Payment Resources</mat-card-title>
          <mat-card-subtitle
            >Cash is always enabled. Maya resources are staged for a later integration
            pass.</mat-card-subtitle
          >
        </mat-card-header>
        <mat-card-content>
          <div class="payment-resource-list">
            @for (resource of w.tenantPaymentResources(); track resource.method) {
              <div class="payment-resource-row">
                <div class="payment-resource-summary">
                  <span class="payment-resource-icon" aria-hidden="true">{{ resource.icon }}</span>
                  <div>
                    <div class="inline-status-row">
                      <strong>{{ resource.label }}</strong>
                      <span
                        class="status-chip"
                        [class.active]="resource.enabled"
                        [class.not-used]="!resource.enabled"
                      >
                        {{ resource.statusLabel }}
                      </span>
                    </div>
                    <span>{{ resource.description }}</span>
                  </div>
                </div>
                <mat-slide-toggle
                  [checked]="resource.enabled"
                  [disabled]="resource.locked"
                  [matTooltip]="resource.locked ? lockedReason(resource) : ''"
                  (change)="w.setPaymentResourceEnabled(resource.method, $event.checked)"
                >
                  {{ resource.enabled ? 'Enabled' : 'Disabled' }}
                </mat-slide-toggle>
              </div>
            }
          </div>
        </mat-card-content>
      </mat-card>
    </section>
  `,
})
export class SettingsPageComponent extends AdminRoutePage {
  constructor() {
    super();
    this.activate('settings');
  }

  lockedReason(resource: { readonly method: string }): string {
    return resource.method === 'CASH' ? 'Cash is always enabled.' : '';
  }
}

@Component({
  selector: 'admin-change-password-dialog',
  standalone: true,
  imports: [AdminUiModule],
  template: `
    <h2 mat-dialog-title>Change Password</h2>
    <mat-dialog-content class="dialog-form">
      <mat-form-field appearance="outline">
        <mat-label>Current password</mat-label>
        <input
          matInput
          type="password"
          [ngModel]="w.changePasswordCurrent()"
          (ngModelChange)="w.changePasswordCurrent.set($event)"
        />
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>New password</mat-label>
        <input
          matInput
          type="password"
          [ngModel]="w.changePasswordNew()"
          (ngModelChange)="w.changePasswordNew.set($event)"
        />
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Confirm new password</mat-label>
        <input
          matInput
          type="password"
          [ngModel]="w.changePasswordConfirm()"
          (ngModelChange)="w.changePasswordConfirm.set($event)"
        />
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" (click)="close()">Cancel</button>
      <button mat-flat-button color="primary" type="button" (click)="changePassword()">
        Change Password
      </button>
    </mat-dialog-actions>
  `,
})
export class ChangePasswordDialogComponent {
  protected readonly w = inject(AdminWorkspace);
  private readonly dialogRef = inject(MatDialogRef<ChangePasswordDialogComponent>);

  async changePassword(): Promise<void> {
    const changed = await this.w.changeOwnPassword();

    if (changed && !this.w.session()?.mustChangePassword) {
      this.dialogRef.close();
    }
  }

  close(): void {
    this.w.closeChangePasswordModal();
    this.dialogRef.close();
  }
}

@Component({
  selector: 'admin-account-page',
  standalone: true,
  imports: [AdminUiModule],
  template: `
    <section class="content-grid">
      <mat-card>
        <mat-card-header>
          <mat-card-title>Account Information</mat-card-title>
        </mat-card-header>
        <mat-card-content class="account-readonly-form">
          <mat-form-field appearance="outline">
            <mat-label>Name</mat-label>
            <input matInput readonly [value]="w.session()?.name ?? ''" />
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Email</mat-label>
            <input matInput readonly [value]="w.session()?.email ?? ''" />
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Role</mat-label>
            <input matInput readonly [value]="w.roleLabel()" />
          </mat-form-field>
        </mat-card-content>
      </mat-card>
      <mat-card>
        <mat-card-header>
          <mat-card-title>Account Actions</mat-card-title>
        </mat-card-header>
        <mat-card-content class="detail-info-stack">
          <p>Change the password for the signed-in account.</p>
          <button
            mat-flat-button
            color="primary"
            class="account-action-button"
            type="button"
            (click)="openChangePassword()"
          >
            Change Password
          </button>
        </mat-card-content>
      </mat-card>
    </section>
  `,
})
export class AccountPageComponent extends AdminRoutePage {
  constructor() {
    super();
    this.activate('account');
  }

  openChangePassword(): void {
    this.w.openChangePasswordModal();
    this.openDialog(ChangePasswordDialogComponent, '520px');
  }
}

@Component({
  selector: 'admin-audit-page',
  standalone: true,
  imports: [AdminUiModule, AdminGridComponent],
  template: `
    <section class="page-stack">
      <admin-grid [rowData]="w.overview()?.auditLogs ?? []" [columnDefs]="columns" />
    </section>
  `,
})
export class AuditPageComponent extends AdminRoutePage {
  readonly columns: ColDef<AuditLogSummary>[] = [
    { field: 'action', headerName: 'Action', minWidth: 220 },
    {
      headerName: 'Details',
      flex: 2,
      minWidth: 320,
      valueGetter: (params) => (params.data ? this.w.auditDetailFor(params.data) : ''),
    },
    { field: 'entityType', headerName: 'Entity' },
    { field: 'entityId', headerName: 'Entity ID' },
    {
      field: 'createdAt',
      headerName: 'Created',
      valueFormatter: (params) => (params.value ? this.w.formatDate(params.value) : ''),
      minWidth: 170,
    },
  ];

  constructor() {
    super();
    this.activate('audit');
  }
}
