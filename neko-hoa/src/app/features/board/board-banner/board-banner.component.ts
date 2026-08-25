import { Component } from '@angular/core';

// 025 FR-021: a visually distinct banner marks board mode and states that
// association-wide data is shown. It carries NO control — the toggle lives in the
// top bar. Binding contrast: dark ink (`--ink`) on the violet fill (`--violet`);
// white-on-violet measured 2.51:1 and fails WCAG 2.1 AA (FR-038).
@Component({
  selector: 'app-board-banner',
  standalone: true,
  template: `
    <div class="board-banner" role="status">
      <b>Board mode</b>
      <span class="board-banner__note">· you are seeing association-wide data, not just your home</span>
    </div>
  `,
  styles: [`
    .board-banner {
      display: flex; align-items: center; gap: 8px;
      padding: 9px 22px; flex-shrink: 0;
      background: var(--violet); color: var(--ink);
      border-bottom: 1.5px solid var(--ink);
      font-size: 12.5px;
    }
    .board-banner b { font-weight: 700; }
    .board-banner__note { color: var(--ink); opacity: .85; }
  `]
})
export class BoardBannerComponent {}
