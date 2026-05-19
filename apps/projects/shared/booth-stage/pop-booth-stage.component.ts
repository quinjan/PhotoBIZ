import { CommonModule } from '@angular/common';
import { Component, computed, input, output } from '@angular/core';

import {
  formatStageMoney,
  stageBackgroundImage,
  stageCashOption,
  stageEyebrow,
  stageMessage,
  stageTitle,
} from './booth-stage.helpers';
import { BoothStageAction, BoothStageConfig, BoothStageScreenState } from './booth-stage.models';

@Component({
  selector: 'photobiz-pop-booth-stage',
  imports: [CommonModule],
  templateUrl: './pop-booth-stage.component.html',
  styleUrl: './pop-booth-stage.component.scss',
})
export class PopBoothStageComponent {
  readonly config = input<BoothStageConfig | null>(null);
  readonly screen = input<BoothStageScreenState>('connect');
  readonly loading = input(false);
  readonly action = output<BoothStageAction>();

  protected readonly title = computed(() => stageTitle(this.config(), this.screen()));
  protected readonly message = computed(() => stageMessage(this.config(), this.screen()));
  protected readonly eyebrow = computed(() => stageEyebrow(this.config()));
  protected readonly cashOption = computed(() => stageCashOption(this.config()));
  protected readonly backgroundImage = computed(() => stageBackgroundImage(this.config()));

  protected readonly formatMoney = formatStageMoney;
}
