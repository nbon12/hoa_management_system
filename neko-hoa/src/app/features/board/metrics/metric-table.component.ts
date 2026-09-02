import { Component, EventEmitter, Input, Output } from '@angular/core';
import { MetricDescriptor } from '../../../core/services/board.service';

// 025 FR-030/FR-032/FR-036: one table for every metric surface, driven entirely by
// descriptors. The help affordance is the right-most column of every row (FR-032). An
// empty descriptor list renders an explicit empty state, not a headers-only table; a
// descriptor whose status is 'Unavailable' renders an explicit per-row unavailable state
// without blanking its siblings (FR-036). Status and emphasis are compared against the
// backend enum names exactly as serialized (`MetricStatus` / `MetricEmphasis`).
@Component({
  selector: 'app-metric-table',
  standalone: true,
  template: `
    @if (rows.length === 0) {
      <div class="metric-empty" data-testid="metric-empty">
        <div class="metric-empty__mark">◦</div>
        <div class="metric-empty__title">No metrics yet</div>
        <p class="muted">No metrics are configured for this community yet.</p>
      </div>
    } @else {
      <table class="data-table metric-table">
        <thead>
          <tr>
            <th>{{ metricHead }}</th>
            @if (showStatus) { <th style="width:84px;">Status</th> }
            <th class="num" style="width:160px;">{{ valueHead }}</th>
            <th style="width:64px;text-align:center;">Help</th>
          </tr>
        </thead>
        <tbody>
          @for (m of rows; track m.id) {
            <tr [attr.data-metric-id]="m.id" [class.metric-row--unavailable]="isUnavailable(m)">
              <td>{{ m.label }}</td>
              @if (showStatus) {
                <td>
                  @if (isUnavailable(m)) {
                    <span class="pill pill--warn">unavailable</span>
                  } @else if (m.status === 'Ok') {
                    <span style="color:var(--ok);font-weight:600;">✓</span>
                  } @else if (m.status === 'Watch') {
                    <span class="pill pill--warn">watch</span>
                  } @else {
                    <span style="color:var(--ink-mute);">—</span>
                  }
                </td>
              }
              <td class="num">
                @if (isUnavailable(m)) {
                  <span style="color:var(--ink-mute);">unavailable</span>
                } @else {
                  <span [style.color]="m.emphasis === 'Highlight' ? 'var(--rose)' : 'var(--ink)'"
                        [style.font-weight]="m.emphasis === 'Highlight' ? 600 : 400">{{ m.value }}</span>
                  @if (m.detail) { <div class="muted" style="font-size:10.5px;">{{ m.detail }}</div> }
                }
              </td>
              <td style="text-align:center;">
                <button type="button" class="metric-help"
                        [attr.aria-label]="'What does ' + m.label + ' mean?'"
                        (click)="help.emit(m.id)">?</button>
              </td>
            </tr>
          }
        </tbody>
      </table>
    }
  `,
  styles: [`
    .metric-table { width: 100%; }
    .metric-row--unavailable td { opacity: .7; }
    .metric-help {
      width: 22px; height: 22px; border-radius: 999px; cursor: pointer;
      border: 1.5px solid var(--line); background: var(--paper); color: var(--ink-soft);
      font-family: inherit; font-size: 12px; font-weight: 600; line-height: 1;
    }
    .metric-help:hover { border-color: var(--violet); color: var(--ink); }
    .metric-help:focus-visible { outline: 2px solid var(--violet); outline-offset: 2px; }
    .metric-empty {
      display: flex; flex-direction: column; align-items: center; gap: 6px;
      padding: 48px 20px; text-align: center;
    }
    .metric-empty__mark {
      width: 52px; height: 52px; border-radius: 50%; border: 1.5px dashed var(--line);
      display: flex; align-items: center; justify-content: center; font-size: 22px; color: var(--ink-mute);
    }
    .metric-empty__title { font-weight: 600; font-size: 15px; }
  `]
})
export class MetricTableComponent {
  @Input() rows: MetricDescriptor[] = [];
  @Input() metricHead = 'Metric';
  @Input() valueHead = 'Value';
  @Input() showStatus = true;
  /** Emits the descriptor id whose help affordance was activated (FR-033). */
  @Output() help = new EventEmitter<string>();

  isUnavailable(m: MetricDescriptor): boolean {
    return m.status === 'Unavailable';
  }
}
