import { Component, computed, input } from '@angular/core';
import { ArcTally } from '../../../core/services/architectural.service';
import { tallyLabel } from './arc-format';

// 027 FR-007 / wireframe `Tally`: one dot per eligible vote (approve · revisions needed · deny · not voted)
// plus "approved/eligible" text. Counts are exposed as text through the label, never by color alone.
@Component({
  selector: 'app-arc-tally',
  standalone: true,
  template: `
    <span class="tally" role="img" [attr.aria-label]="label()" [attr.title]="label()">
      @for (d of dots(); track $index) { <i [class]="'tally__dot tally__dot--' + d" aria-hidden="true"></i> }
      <span class="tally__text" aria-hidden="true">{{ tally().approve }}/{{ tally().eligible }}</span>
    </span>
  `,
  styles: [`
    .tally { display: inline-flex; align-items: center; gap: 3px; }
    .tally__dot { width: 9px; height: 9px; border-radius: 50%; border: 1.5px solid var(--ink-soft); display: inline-block; }
    .tally__dot--approve { background: var(--ok); }
    .tally__dot--revisions { background: var(--lav-2); border-style: dashed; }
    .tally__dot--deny { background: var(--warn); }
    .tally__dot--none { background: var(--paper); }
    .tally__text { font-size: 11px; color: var(--ink-soft); margin-left: 4px; }
  `]
})
export class TallyComponent {
  readonly tally = input.required<ArcTally>();
  readonly label = computed(() => tallyLabel(this.tally()));
  readonly dots = computed(() => {
    const t = this.tally();
    return [
      ...Array(t.approve).fill('approve'),
      ...Array(t.revisionsNeeded).fill('revisions'),
      ...Array(t.deny).fill('deny'),
      ...Array(Math.max(0, t.notVoted)).fill('none'),
    ];
  });
}
