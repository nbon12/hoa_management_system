import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { BUILD_ID } from '../environments/build-id';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet],
  template: `
    <router-outlet />
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
