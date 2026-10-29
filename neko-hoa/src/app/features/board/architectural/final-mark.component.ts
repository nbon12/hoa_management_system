import { Component, computed, input } from '@angular/core';
import { ArcDecision } from '../../../core/services/architectural.service';
import { finalIcon, finalResultLabel } from './arc-format';

const GLYPHS = { approved: '✓', denied: '✕', revisions: '↻', withdrawn: '–' } as const;

/** 031 (item 8): ✓ approved, ✕ denied, yellow ↻ revisions requested — labeled, never color alone. */
@Component({
  selector: 'app-arc-final-mark',
  standalone: true,
  template: `
    @if (icon(); as i) {
      <span class="final-mark final-mark--{{ i }}" role="img" [attr.aria-label]="label()" [attr.title]="label()">
        <span aria-hidden="true">{{ glyphs[i] }}</span>
      </span>
    }
  `
})
export class FinalMarkComponent {
  readonly decision = input<ArcDecision | null | undefined>(null);
  readonly icon = computed(() => finalIcon(this.decision()));
  readonly label = computed(() => `Final decision: ${finalResultLabel(this.decision())}`);
  readonly glyphs = GLYPHS;
}
