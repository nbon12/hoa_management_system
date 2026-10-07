import { Component, computed, inject } from '@angular/core';
import { AuthService } from '../../../core/services/auth.service';
import { BoardNavigationService } from '../../../core/services/board-navigation.service';
import { MetricsPanelComponent } from '../metrics/metrics-panel.component';
import { NeedsYourVoteCardComponent } from '../architectural/needs-your-vote-card.component';

// 025 FR-026: the single-community landing page. Real content ships in spec 2 (Community
// Overview & Metrics); this placeholder establishes the route and renders the registry-driven
// metric surfaces (empty until spec 2 registers concrete descriptors).
@Component({
  selector: 'app-community-home',
  standalone: true,
  imports: [MetricsPanelComponent, NeedsYourVoteCardComponent],
  template: `
    <div class="page-header">
      <h1 class="page-title">{{ communityName() }} <span class="hand">at a glance</span></h1>
    </div>

    <!-- 027 US5: its own section; spec 2 owns the rest of this page. -->
    @if (isBoardMember()) {
      <app-arc-needs-your-vote-card />
    }

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

  readonly communityId = computed(() => {
    const memberships = this.auth.user()?.memberships ?? [];
    const active = this.nav.activeCommunityId();
    if (active) return active;
    return memberships.length ? memberships[0].communityId : null;
  });

  /** 027 FR-032: the Needs-your-vote card is for board members of the active community. */
  readonly isBoardMember = computed(() => {
    const id = this.communityId();
    return (this.auth.user()?.memberships ?? []).some(m => m.communityId === id && m.role === 'BoardMember');
  });

  readonly communityName = computed(() => {
    const id = this.communityId();
    const m = (this.auth.user()?.memberships ?? []).find(x => x.communityId === id);
    return m?.communityName ?? 'Community';
  });
}
