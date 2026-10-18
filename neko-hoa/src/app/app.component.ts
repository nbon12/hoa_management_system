import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { BUILD_ID } from '../environments/build-id';
import { ToastHostComponent } from './shared/toast-host.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, ToastHostComponent],
  template: `
    <router-outlet />
    <!-- 030: one polite live region for every toast; it outlives the navigation that follows one. -->
    <app-toast-host />
    <div class="build-id">{{ buildId }}</div>
  `,
  styles: [`
    .build-id {
      position: fixed;
      left: 10px;
      bottom: 8px;
      z-index: 9999;
      font-size: 10px;
      color: var(--ink-mute);
      opacity: 0.75;
      pointer-events: none;
      user-select: none;
    }
  `],
})
export class AppComponent {
  buildId = BUILD_ID;
}
