import { Component, computed, inject } from '@angular/core';
import { AuthService } from '../../../core/services/auth.service';
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
  private auth = inject(AuthService);
  private nav = inject(BoardNavigationService);

  readonly communityId = computed(() => {
    const memberships = this.auth.user()?.memberships ?? [];
    const active = this.nav.activeCommunityId();
    if (active) return active;
    return memberships.length ? memberships[0].communityId : null;
  });
}
