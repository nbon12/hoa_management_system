import { Component, inject } from '@angular/core';
import { BoardNavigationService } from '../../../core/services/board-navigation.service';
import { MetricsPanelComponent } from './metrics-panel.component';

// 025 US4: a dedicated metric surface for the `board/metrics` route. Registry-driven —
// ships empty until spec 2 registers descriptors; renders the explicit empty state then.
@Component({
  selector: 'app-board-metrics-page',
  standalone: true,
  imports: [MetricsPanelComponent],
  template: `
    <div class="page-header">
      <h1 class="page-title">Community <span class="hand">metrics</span></h1>
    </div>
    <div class="card">
      <app-metrics-panel [communityId]="communityId()" surface="community" />
    </div>
  `
})
export class BoardMetricsPageComponent {
  private nav = inject(BoardNavigationService);

  readonly communityId = this.nav.effectiveCommunityId;
}
