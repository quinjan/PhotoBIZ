import { CommonModule } from '@angular/common';
import { Component, computed, input, output } from '@angular/core';

import { CleanModernBoothStageComponent } from './clean-modern-booth-stage.component';
import { BoothStageAction, BoothStageConfig, BoothStageScreenState } from './booth-stage.models';
import { normalizeStagePreset } from './booth-stage.helpers';
import { PopBoothStageComponent } from './pop-booth-stage.component';
import { VintageBoothStageComponent } from './vintage-booth-stage.component';

@Component({
  selector: 'photobiz-booth-stage',
  imports: [
    CommonModule,
    CleanModernBoothStageComponent,
    PopBoothStageComponent,
    VintageBoothStageComponent,
  ],
  templateUrl: './booth-stage.component.html',
  styleUrl: './booth-stage.component.scss',
})
export class BoothStageComponent {
  readonly config = input<BoothStageConfig | null>(null);
  readonly screen = input<BoothStageScreenState>('connect');
  readonly loading = input(false);
  readonly action = output<BoothStageAction>();

  protected readonly preset = computed(() => normalizeStagePreset(this.config()?.theme?.preset));
  protected readonly primaryColor = computed(() => this.config()?.theme?.primaryColor ?? '#4f2d1d');

  protected emitAction(action: BoothStageAction): void {
    this.action.emit(action);
  }
}
