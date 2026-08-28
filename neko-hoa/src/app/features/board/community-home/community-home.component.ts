import { Component, computed, inject } from '@angular/core';
import { AuthService } from '../../../core/services/auth.service';
import { BoardNavigationService } from '../../../core/services/board-navigation.service';
import { MetricsPanelComponent } from '../metrics/metrics-panel.component';

// 025 FR-026: the single-community landing page. Real content ships in spec 2 (Community
// Overview & Metrics); this placeholder establishes the route and renders the registry-driven
// metric surfaces (empty until spec 2 registers concrete descriptors).
@Component({
  selector: 'app-community-home',
  standalone: true,
  imports: [MetricsPanelComponent],
  template: `
    <div class="page-header">
      <h1 class="page-title">{{ communityName() }} <span class="hand">at a glance</span></h1>
    </div>

    <div class="card">
      <div class="field-label">Work Processed — last 30 days</div>
      <app-metrics-panel
        [communityId]="communityId()"
        surface="work"
        metricHead="Work area"
        valueHead="Count"
        [showStatus]="false" />
    </div>

    <div class="card">
      <div class="field-label">Community metrics</div>
      <app-metrics-panel
        [communityId]="communityId()"
        surface="community"
        metricHead="Metric"
        valueHead="Value" />
    </div>
  `
})
export class CommunityHomeComponent {
  private auth = inject(AuthService);
  private nav = inject(BoardNavigationService);

  readonly communityId = this.nav.effectiveCommunityId;

  readonly communityName = computed(() => {
    const id = this.communityId();
    const m = (this.auth.user()?.memberships ?? []).find(x => x.communityId === id);
    return m?.communityName ?? 'Community';
  });
}
