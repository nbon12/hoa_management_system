import { Component, Input, OnChanges, computed, inject, signal } from '@angular/core';
import { BoardService, MetricDescriptor } from '../../../core/services/board.service';
import { MetricTableComponent } from './metric-table.component';
import { GlossaryPanelComponent } from './glossary-panel.component';

// 025 US4: composes the registry-driven table with its glossary for one surface.
// Both read the same descriptor list (FR-034); clicking a row's help opens the panel
// at that term (FR-033) and closing returns focus to the trigger.
@Component({
  selector: 'app-metrics-panel',
  standalone: true,
  imports: [MetricTableComponent, GlossaryPanelComponent],
  template: `
    <div class="metrics-panel">
      <div class="metrics-panel__main">
        <app-metric-table
          [rows]="rows()"
          [metricHead]="metricHead"
          [valueHead]="valueHead"
          [showStatus]="showStatus"
          (help)="openGlossary($event)" />
      </div>
      @if (glossaryOpen()) {
        <app-glossary-panel
          [descriptors]="rows()"
          [target]="target()"
          (close)="closeGlossary()" />
      }
    </div>
  `,
  styles: [`
    .metrics-panel { display: flex; gap: 0; align-items: stretch; min-height: 0; }
    .metrics-panel__main { flex: 1; min-width: 0; }
  `]
})
export class MetricsPanelComponent implements OnChanges {
  private board = inject(BoardService);

  @Input() communityId: string | null = null;
  @Input() surface = 'community';
  @Input() metricHead = 'Metric';
  @Input() valueHead = 'Value';
  @Input() showStatus = true;

  readonly rows = signal<MetricDescriptor[]>([]);
  readonly glossaryOpen = signal(false);
  readonly target = signal<string | null>(null);

  private lastTrigger: HTMLElement | null = null;

  async ngOnChanges(): Promise<void> {
    if (!this.communityId) { this.rows.set([]); return; }
    try {
      const page = await this.board.getMetrics(this.communityId, this.surface);
      this.rows.set(page.items);
    } catch {
      // A metric-surface fetch failure renders the empty state rather than crashing the page.
      this.rows.set([]);
    }
  }

  openGlossary(id: string): void {
    // Capture the trigger so focus can be returned to it on close (FR-033).
    this.lastTrigger = (document.activeElement as HTMLElement) ?? null;
    this.target.set(id);
    this.glossaryOpen.set(true);
  }

  closeGlossary(): void {
    this.glossaryOpen.set(false);
    this.target.set(null);
    this.lastTrigger?.focus();
    this.lastTrigger = null;
  }
}
