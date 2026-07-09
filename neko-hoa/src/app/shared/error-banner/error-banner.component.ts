import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * Shared error presentation (015 US6, FR-020): the one way screens render a recoverable error.
 * Accessible by construction — `role="alert"` announces the message to assistive tech when it
 * appears; visible text carries the full message (WCAG 2.1 AA).
 */
@Component({
  selector: 'app-error-banner',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (message()) {
      <div class="error-banner" role="alert">{{ message() }}</div>
    }
  `,
  styles: [`
    .error-banner {
      background: #fdecec;
      border: 1px solid #e5484d;
      border-radius: 8px;
      color: #b3261e;
      font-size: 14px;
      margin: 12px 0;
      padding: 10px 14px;
    }
  `],
})
export class ErrorBannerComponent {
  /** The user-presentable error text; the banner hides itself when null/empty. */
  message = input<string | null>(null);
}
