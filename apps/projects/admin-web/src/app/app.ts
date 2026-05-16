import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterOutlet } from '@angular/router';
import { firstValueFrom, interval } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

type Session = {
  readonly userId: string;
  readonly name: string;
  readonly email: string;
  readonly role: string;
  readonly clientAccountId: string | null;
  readonly assignedBoothId: string | null;
};

type Overview = {
  readonly session: Session;
  readonly clients: readonly { id: string; name: string; status: string }[];
  readonly subscriptionPlans: readonly { id: string; name: string; pricePerBoothCents: number; currency: string; active: boolean }[];
  readonly subscriptions: readonly { id: string; clientAccountId: string; subscriptionPlanId: string; status: string; activeBoothAllowance: number }[];
  readonly users: readonly { id: string; clientAccountId: string | null; name: string; email: string; role: string; assignedBoothId: string | null }[];
  readonly locations: readonly { id: string; clientAccountId: string; name: string; address: string | null }[];
  readonly booths: readonly { id: string; clientAccountId: string; locationId: string; name: string; code: string; status: string; currentState: string; lastHeartbeatAt: string | null }[];
  readonly offers: readonly { id: string; clientAccountId: string; name: string; offerType: string; priceCents: number; currency: string; allowsExtraPrintAddOn: boolean; active: boolean }[];
  readonly activations: readonly { id: string; boothId: string; boothOfferId: string; status: string }[];
  readonly paymentAssignments: readonly { id: string; boothId: string; paymentMethod: string; runtimeEnabled: boolean; status: string }[];
  readonly transactions: readonly { id: string; boothId: string; transactionNumber: string; status: string; paymentMethod: string; amountCents: number; createdAt: string; paidAt: string | null; completedAt: string | null }[];
};

type BoothSecret = {
  readonly boothName: string;
  readonly boothCode: string;
  readonly kioskToken: string;
  readonly agentCredential: string;
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

  protected readonly session = signal<Session | null>(null);
  protected readonly overview = signal<Overview | null>(null);
  protected readonly message = signal('Use the bootstrap owner account to start wiring the platform.');
  protected readonly error = signal('');
  protected readonly loading = signal(false);
  protected readonly boothSecret = signal<BoothSecret | null>(null);

  protected readonly loginEmail = signal('owner@photobiz.local');
  protected readonly loginPassword = signal('PhotoBIZ!123');
  protected readonly clientName = signal('The Memory Box');
  protected readonly planName = signal('Starter Booth');
  protected readonly planPrice = signal(150000);
  protected readonly locationName = signal('SM Manila');
  protected readonly boothName = signal('Booth A');
  protected readonly boothCode = signal('SMA-001');
  protected readonly ownerName = signal('Client Owner');
  protected readonly ownerEmail = signal('owner@memorybox.local');
  protected readonly ownerPassword = signal('PhotoBIZ!123');
  protected readonly offerName = signal('Per Session');
  protected readonly offerPrice = signal(25000);
  protected readonly offerMode = signal('SESSION_STANDARD');
  protected readonly selectedClientId = computed(() => this.overview()?.clients[0]?.id ?? null);
  protected readonly selectedPlanId = computed(() => this.overview()?.subscriptionPlans[0]?.id ?? null);
  protected readonly selectedLocationId = computed(() => this.overview()?.locations[0]?.id ?? null);
  protected readonly selectedBoothId = computed(() => this.overview()?.booths[0]?.id ?? null);
  protected readonly selectedOfferId = computed(() => this.overview()?.offers[0]?.id ?? null);

  constructor() {
    this.restoreSession();

    interval(5000)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        if (this.session()) {
          this.loadOverview();
        }
      });
  }

  protected async login(): Promise<void> {
    await this.run(async () => {
      const session = await firstValueFrom(
        this.http.post<Session>(
          `${App.apiBaseUrl}/api/auth/login`,
          {
            email: this.loginEmail(),
            password: this.loginPassword(),
          },
          { withCredentials: true },
        ),
      );

      if (session) {
        this.session.set(session);
        await this.loadOverview();
      }
    });
  }

  protected async logout(): Promise<void> {
    await firstValueFrom(this.http.post(`${App.apiBaseUrl}/api/auth/logout`, {}, { withCredentials: true }));
    this.session.set(null);
    this.overview.set(null);
    this.message.set('Signed out.');
  }

  protected async createClient(): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(this.http.post(`${App.apiBaseUrl}/api/admin/clients`, { name: this.clientName() }, { withCredentials: true }));
      await this.loadOverview();
      this.message.set('Client account created.');
    });
  }

  protected async createPlan(): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(this.http
        .post(
          `${App.apiBaseUrl}/api/admin/subscription-plans`,
          { name: this.planName(), pricePerBoothCents: this.planPrice(), currency: 'PHP' },
          { withCredentials: true },
        )
      );
      await this.loadOverview();
      this.message.set('Subscription plan created.');
    });
  }

  protected async assignSubscription(): Promise<void> {
    if (!this.selectedClientId() || !this.selectedPlanId()) {
      return;
    }

    await this.run(async () => {
      await firstValueFrom(this.http
        .post(
          `${App.apiBaseUrl}/api/admin/subscriptions`,
          {
            clientAccountId: this.selectedClientId(),
            subscriptionPlanId: this.selectedPlanId(),
            status: 'ACTIVE',
            activeBoothAllowance: 2,
            notes: 'Bootstrap MVP subscription',
          },
          { withCredentials: true },
        )
      );
      await this.loadOverview();
      this.message.set('Client subscription assigned.');
    });
  }

  protected async createOwner(): Promise<void> {
    if (!this.selectedClientId()) {
      return;
    }

    await this.run(async () => {
      await firstValueFrom(this.http
        .post(
          `${App.apiBaseUrl}/api/admin/users`,
          {
            clientAccountId: this.selectedClientId(),
            assignedBoothId: null,
            name: this.ownerName(),
            email: this.ownerEmail(),
            password: this.ownerPassword(),
            role: 'CLIENT_OWNER',
          },
          { withCredentials: true },
        )
      );
      await this.loadOverview();
      this.message.set('Client owner created.');
    });
  }

  protected async createLocation(): Promise<void> {
    if (!this.selectedClientId()) {
      return;
    }

    await this.run(async () => {
      await firstValueFrom(this.http
        .post(
          `${App.apiBaseUrl}/api/admin/locations`,
          {
            clientAccountId: this.selectedClientId(),
            name: this.locationName(),
            address: 'Customer-facing mall location',
          },
          { withCredentials: true },
        )
      );
      await this.loadOverview();
      this.message.set('Location created.');
    });
  }

  protected async createBooth(): Promise<void> {
    if (!this.selectedClientId() || !this.selectedLocationId()) {
      return;
    }

    await this.run(async () => {
      const response = await firstValueFrom(this.http
        .post<{ booth: Overview['booths'][number]; kioskToken: string; agentCredential: string }>(
          `${App.apiBaseUrl}/api/admin/booths`,
          {
            clientAccountId: this.selectedClientId(),
            locationId: this.selectedLocationId(),
            name: this.boothName(),
            code: this.boothCode(),
          },
          { withCredentials: true },
        )
      );

      if (response) {
        this.boothSecret.set({
          boothName: response.booth.name,
          boothCode: response.booth.code,
          kioskToken: response.kioskToken,
          agentCredential: response.agentCredential,
        });
      }

      await this.loadOverview();
      this.message.set('Booth created and credentials issued.');
    });
  }

  protected async createOffer(): Promise<void> {
    if (!this.selectedClientId()) {
      return;
    }

    await this.run(async () => {
      await firstValueFrom(this.http
        .post(
          `${App.apiBaseUrl}/api/admin/offers`,
          {
            clientAccountId: this.selectedClientId(),
            name: this.offerName(),
            description: 'Standard booth session',
            offerType: 'PER_SESSION',
            priceCents: this.offerPrice(),
            currency: 'PHP',
            includedPrintEntitlement: '2 pcs 6x2 or 1 pc 6x4',
            durationHours: null,
            sessionAllowance: null,
            allowsExtraPrintAddOn: true,
            extraPrintPriceCents: 5000,
            lumaboothSessionMode: this.offerMode(),
          },
          { withCredentials: true },
        )
      );
      await this.loadOverview();
      this.message.set('Offer created.');
    });
  }

  protected async activateOffer(): Promise<void> {
    if (!this.selectedBoothId() || !this.selectedOfferId()) {
      return;
    }

    await this.run(async () => {
      await firstValueFrom(this.http
        .post(
          `${App.apiBaseUrl}/api/admin/booths/${this.selectedBoothId()}/activate-offer`,
          { boothOfferId: this.selectedOfferId() },
          { withCredentials: true },
        )
      );
      await this.loadOverview();
      this.message.set('Offer activated for booth.');
    });
  }

  protected async approveCash(transactionId: string): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(this.http.post(`${App.apiBaseUrl}/api/cashier/transactions/${transactionId}/approve-cash`, {}, { withCredentials: true }));
      await this.loadOverview();
      this.message.set('Cash approved.');
    });
  }

  private async restoreSession(): Promise<void> {
    try {
      const session = await firstValueFrom(this.http.get<Session>(`${App.apiBaseUrl}/api/auth/session`, { withCredentials: true }));
      if (session) {
        this.session.set(session);
        await this.loadOverview();
      }
    } catch {
      this.session.set(null);
    }
  }

  private async loadOverview(): Promise<void> {
    const overview = await firstValueFrom(this.http.get<Overview>(`${App.apiBaseUrl}/api/admin/overview`, { withCredentials: true }));
    if (overview) {
      this.overview.set(overview);
      this.session.set(overview.session);
    }
  }

  private async run(operation: () => Promise<void>): Promise<void> {
    this.loading.set(true);
    this.error.set('');

    try {
      await operation();
    } catch (error) {
      this.error.set(error instanceof Error ? error.message : 'Request failed.');
    } finally {
      this.loading.set(false);
    }
  }
}
