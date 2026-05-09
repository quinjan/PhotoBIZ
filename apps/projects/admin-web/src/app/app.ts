import { Component, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';

type DashboardMetric = {
  readonly label: string;
  readonly value: string;
  readonly note: string;
};

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected readonly productName = signal('PhotoBIZ Admin');

  protected readonly metrics: readonly DashboardMetric[] = [
    {
      label: 'Active clients',
      value: '0',
      note: 'Ready for Phase 2 client setup',
    },
    {
      label: 'Active booths',
      value: '0',
      note: 'Subscription allowance will gate activation',
    },
    {
      label: 'Pending cash',
      value: '0',
      note: 'Cashier POS arrives in Phase 3',
    },
  ];
}
