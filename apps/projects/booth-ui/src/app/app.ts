import { Component, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';

type BoothStep = {
  readonly label: string;
  readonly value: string;
};

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected readonly welcomeHeadline = signal('Step Into The Memory Box');
  protected readonly boothStatus = signal('WELCOME');

  protected readonly steps: readonly BoothStep[] = [
    { label: 'Package', value: 'Choose print package' },
    { label: 'Payment', value: 'Cash approval only' },
    { label: 'Session', value: 'Backend commands agent' },
  ];
}
