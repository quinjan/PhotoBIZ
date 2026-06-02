import { Component, inject, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { AdminShellUiModule } from './admin-shell-ui.module';
import { AdminWorkspace } from './admin-workspace.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, AdminShellUiModule],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected readonly workspace = inject(AdminWorkspace);
  protected readonly navigationCollapsed = signal(false);

  protected toggleNavigation(): void {
    this.navigationCollapsed.update((collapsed) => !collapsed);
  }
}
