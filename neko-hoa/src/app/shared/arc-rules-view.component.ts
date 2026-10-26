import { Component, Input } from '@angular/core';
import { ArcRuleFields } from '../core/services/company.service';
import { ARC_RULE_FIELDS, ARC_RULE_LABELS, ARC_RULE_SETTING_IDS, ArcRuleField, arcRuleValueLabel } from './arc-rules';

/**
 * 030 briefs 07 and 08 (read view): the six architectural review rules as a description list, in
 * spec 027's order, with the statement at full width in quotes. Given `differs`, a differing field
 * shows "Differs from defaults" after its value and "Company default: …" under it.
 */
@Component({
  selector: 'app-arc-rules-view',
  standalone: true,
  template: `
    <dl class="arv">
      @for (f of fields; track f) {
        <div class="arv__pair" [class.arv__pair--wide]="f === 'formalDisapprovalStatement'">
          <dt>{{ labels[f] }}</dt>
          <dd tabindex="-1" [attr.data-setting-id]="settingIds[f]">
            <span class="arv__value">{{ display(f, value) }}</span>
            @if (isDifferent(f)) {
              <span class="sr-only">, </span><span class="pill pill--warn arv__chip">Differs from defaults</span>
              @if (defaults) { <span class="muted arv__default">Company default: {{ display(f, defaults) }}</span> }
            }
          </dd>
        </div>
      }
    </dl>
  `,
  styles: [`
    .arv { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 14px 20px; }
    .arv__pair { min-width: 0; }
    .arv__pair--wide { grid-column: 1 / -1; }
    .arv__pair dt { font-size: 11px; color: var(--ink-soft); font-weight: 600; }
    .arv__pair dd { margin: 2px 0 0; font-size: 13px; color: var(--ink); display: flex; flex-wrap: wrap; align-items: center; gap: 4px 8px; }
    .arv__chip { font-size: 10.5px; }
    .arv__default { flex-basis: 100%; }
    @media (max-width: 767.98px) { .arv { grid-template-columns: minmax(0, 1fr); } }
  `],
})
export class ArcRulesViewComponent {
  @Input({ required: true }) value!: ArcRuleFields;
  /** The company defaults, for the "Company default: …" lines. */
  @Input() defaults: ArcRuleFields | null = null;
  /** Fields that differ from the defaults (the server's `differsFromDefaults`). */
  @Input() differs: readonly string[] = [];

  readonly fields = ARC_RULE_FIELDS;
  readonly labels = ARC_RULE_LABELS;
  readonly settingIds = ARC_RULE_SETTING_IDS;

  isDifferent(f: ArcRuleField): boolean {
    return this.differs.includes(f);
  }

  display(f: ArcRuleField, source: ArcRuleFields): string {
    const text = arcRuleValueLabel(f, source[f]);
    return f === 'formalDisapprovalStatement' ? `“${text}”` : text;
  }
}
