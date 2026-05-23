import { Component, inject } from '@angular/core';
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
}
