import { AfterViewInit, Component, ElementRef, EventEmitter, Input, OnInit, Output, computed, inject, signal } from '@angular/core';
import { ArcRuleFields } from '../core/services/company.service';
import { ComboPickerComponent, PickerOption } from './combo-picker.component';
import { timeZoneOptions } from './time-zones';
import {
  ARC_RULE_FIELDS, ARC_RULE_LABELS, ARC_RULE_SETTING_IDS, ArcRuleField, DECISION_RULE_OPTIONS, LAPSE_RULE_OPTIONS,
  STATEMENT_MAX, arcRuleValueLabel, pickArcRules, validateArcRules,
} from './arc-rules';

type Errors = Partial<Record<ArcRuleField, string>>;

/**
 * 030 briefs 07 and 08 (form view, D1): the six architectural review rules as one form with one
 * Save. "All fields are required." Each field has its helper (and, given `defaults`, its company
 * default). Client checks run first: on failure an error summary takes focus, each link moves focus
 * to its field, and each message also shows under its field. `showErrors` does the same for a
 * server's `VALIDATION_ERROR`. The statement area accepts more than 1,000 characters (D5).
 * The time zone is a searchable dropdown of IANA names, so it can't be mistyped.
 */
@Component({
  selector: 'app-arc-rules-form',
  standalone: true,
  imports: [ComboPickerComponent],
  template: `
    <form class="arf" (submit)="submit($event)" novalidate>
      @if (summary().length) {
        <div class="error-summary" [id]="idPrefix + '-summary'" role="alert" [attr.aria-labelledby]="idPrefix + '-summary-title'" tabindex="-1">
          <h3 class="section-title" [id]="idPrefix + '-summary-title'">{{ summaryHeading }}</h3>
          <ul>
            @for (e of summary(); track e.field) {
              <li><a [href]="'#' + fieldId(e.field)" (click)="focusField($event, e.field)">{{ e.message }}</a></li>
            }
          </ul>
        </div>
      }
      <p class="muted">All fields are required.</p>
      <div class="arf__grid">
        @for (f of fields; track f) {
          <div class="arf__field" [class.arf__field--wide]="f === 'formalDisapprovalStatement'">
            @if (f === 'timeZoneId') {
              <app-combo-picker [label]="labels[f]" [inputId]="fieldId(f)" [settingId]="settingIds[f]" [helper]="helper(f) ?? ''"
                                [helperFirst]="true" [dropdown]="true" placeholder="Search or choose a time zone" [noun]="timeZoneNoun"
                                [options]="timeZones" [value]="draft().timeZoneId || null" (valueChange)="set(f, $event ?? '')"
                                [error]="errors()[f] ?? null" [disabled]="busy" [noMatchMessage]="noTimeZoneMatch" />
            } @else {
              <label class="field-label" [for]="fieldId(f)">{{ labels[f] }}</label>
              @if (helper(f); as h) { <p class="field-help" [id]="fieldId(f) + '-help'">{{ h }}</p> }
              @switch (f) {
                @case ('lapseRule') {
                  <select class="field" [id]="fieldId(f)" [attr.data-setting-id]="settingIds[f]" [attr.aria-describedby]="describedBy(f)"
                          [attr.aria-invalid]="errors()[f] ? 'true' : null" [disabled]="busy" (change)="set(f, $any($event.target).value)">
                    @for (o of lapseOptions; track o.value) { <option [value]="o.value" [selected]="draft().lapseRule === o.value">{{ o.label }}</option> }
                  </select>
                }
                @case ('decisionRule') {
                  <select class="field" [id]="fieldId(f)" [attr.data-setting-id]="settingIds[f]" [attr.aria-describedby]="describedBy(f)"
                          [attr.aria-invalid]="errors()[f] ? 'true' : null" [disabled]="busy" (change)="set(f, $any($event.target).value)">
                    @for (o of decisionOptions; track o.value) { <option [value]="o.value" [selected]="draft().decisionRule === o.value">{{ o.label }}</option> }
                  </select>
                }
                @case ('formalDisapprovalStatement') {
                  <textarea class="field" rows="4" [id]="fieldId(f)" [attr.data-setting-id]="settingIds[f]" [attr.aria-describedby]="describedBy(f)"
                            [attr.aria-invalid]="errors()[f] ? 'true' : null" [disabled]="busy" [value]="draft().formalDisapprovalStatement"
                            (input)="set(f, $any($event.target).value)"></textarea>
                  <p class="field-help" [id]="fieldId(f) + '-count'"><span aria-hidden="true">{{ countText() }} / 1,000</span><span class="sr-only">{{ countText() }} of 1,000 characters used</span></p>
                  <p class="sr-only" role="status" aria-live="polite">{{ overLimit() ? 'The statement is over 1,000 characters.' : '' }}</p>
                }
                @default {
                  <input class="field" type="number" inputmode="numeric" [id]="fieldId(f)" [attr.data-setting-id]="settingIds[f]"
                         [attr.aria-describedby]="describedBy(f)" [attr.aria-invalid]="errors()[f] ? 'true' : null" [disabled]="busy"
                         [value]="draft()[f]" (input)="set(f, $any($event.target).value)" />
                }
              }
              @if (errors()[f]) { <p class="field-error" [id]="fieldId(f) + '-error'">{{ errors()[f] }}</p> }
            }
          </div>
        }
      </div>
      <div class="arf__actions">
        <button type="submit" class="btn btn--primary" [disabled]="busy">{{ busy ? 'Saving…' : saveLabel }}</button>
        <button type="button" class="btn" [disabled]="busy" (click)="cancelled.emit()">Cancel</button>
      </div>
    </form>
  `,
  styles: [`
    .arf { display: flex; flex-direction: column; gap: 12px; }
    .arf__grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 14px 20px; }
    .arf__field { display: flex; flex-direction: column; gap: 4px; min-width: 0; }
    .arf__field--wide { grid-column: 1 / -1; }
    .arf__actions { display: flex; flex-wrap: wrap; gap: 8px; }
    @media (max-width: 767.98px) { .arf__grid { grid-template-columns: minmax(0, 1fr); } }
  `],
})
export class ArcRulesFormComponent implements OnInit, AfterViewInit {
  @Input({ required: true }) value!: ArcRuleFields;
  /** The company defaults, shown in each helper (Architectural review section only). */
  @Input() defaults: ArcRuleFields | null = null;
  /** `defaults`: the Company defaults page's helpers; `community`: a community's section's. */
  @Input() variant: 'community' | 'defaults' = 'community';
  @Input() summaryHeading = '';
  @Input() saveLabel = 'Save';
  @Input() busy = false;
  @Input() idPrefix = 'arf';
  /** The values, after the client checks pass. */
  @Output() readonly submitted = new EventEmitter<ArcRuleFields>();
  @Output() readonly cancelled = new EventEmitter<void>();

  private host = inject<ElementRef<HTMLElement>>(ElementRef);

  readonly fields = ARC_RULE_FIELDS;
  readonly labels = ARC_RULE_LABELS;
  readonly settingIds = ARC_RULE_SETTING_IDS;
  readonly lapseOptions = LAPSE_RULE_OPTIONS;
  readonly decisionOptions = DECISION_RULE_OPTIONS;
  readonly timeZoneNoun: [string, string] = ['time zone', 'time zones'];
  readonly noTimeZoneMatch = (q: string): string => `No time zone matches “${q}”. Try a city, such as Chicago, or a name, such as Eastern or EST.`;
  /** Set once the saved value is known, so a zone the browser doesn't list still shows. */
  timeZones: PickerOption[] = [];

  readonly draft = signal<ArcRuleFields>({
    reviewPeriodDays: 0, lapseRule: 'FlagOverdueOnly', decisionRule: 'MajorityOfMembers', reminderDays: 0, timeZoneId: '', formalDisapprovalStatement: '',
  });
  readonly errors = signal<Errors>({});
  readonly summary = computed(() => ARC_RULE_FIELDS.filter(f => this.errors()[f]).map(f => ({ field: f, message: this.errors()[f]! })));
  readonly count = computed(() => this.draft().formalDisapprovalStatement.length);
  readonly countText = computed(() => this.count().toLocaleString('en-US'));
  readonly overLimit = computed(() => this.count() > STATEMENT_MAX);

  ngOnInit(): void {
    this.draft.set(pickArcRules(this.value));
    this.timeZones = timeZoneOptions(this.value.timeZoneId);
  }

  ngAfterViewInit(): void {
    // Brief 07/08 §6: opening the form moves focus to Review period.
    setTimeout(() => this.fieldElement('reviewPeriodDays')?.focus());
  }

  fieldId(f: ArcRuleField): string {
    return `${this.idPrefix}-${this.settingIds[f]}`;
  }

  helper(f: ArcRuleField): string | null {
    const d = this.defaults;
    const suffix = d ? ` Company default: ${arcRuleValueLabel(f, d[f])}.` : '';
    switch (f) {
      case 'reviewPeriodDays': return `1 to 365. An application is due this many days after it's received.${suffix}`;
      case 'reminderDays': return `0 to 30. 0 turns reminders off.${suffix}`;
      case 'lapseRule':
        return "If the board hasn't decided by the due date, Auto approve approves the application, Auto reject denies it, and No "
          + 'automatic action keeps it open and marks it overdue. Under “Majority of votes cast (with a quorum)”, a side that leads '
          + `with a quorum at the due date decides instead. A Community Manager then records the decision, which emails the owner.${suffix}`;
      case 'decisionRule': return `With a quorum, more than half of the eligible board members must vote.${suffix}`;
      case 'timeZoneId':
        return this.variant === 'defaults'
          ? 'Search by city or name, such as Dallas, Eastern or EST. New communities start with it. Company Administrator invitations show their expiry time in it.'
          : `Search by city or name, such as Dallas, Eastern or EST. Used for due dates and for the expiry times on this community's invitations.${suffix}`;
      case 'formalDisapprovalStatement':
        return this.variant === 'defaults'
          ? 'Up to 1,000 characters.'
          : `Up to 1,000 characters. You can cite your own documents, for example “§12.3 of the Declaration”.${d ? ` Company default: “${d.formalDisapprovalStatement}”` : ''}`;
    }
  }

  describedBy(f: ArcRuleField): string {
    const ids = [this.helper(f) ? `${this.fieldId(f)}-help` : null, f === 'formalDisapprovalStatement' ? `${this.fieldId(f)}-count` : null,
      this.errors()[f] ? `${this.fieldId(f)}-error` : null];
    return ids.filter(Boolean).join(' ');
  }

  set(f: ArcRuleField, raw: string): void {
    const value = f === 'reviewPeriodDays' || f === 'reminderDays' ? (raw.trim() === '' ? Number.NaN : Number(raw)) : raw;
    this.draft.update(d => ({ ...d, [f]: value }));
    if (this.errors()[f]) this.errors.update(e => ({ ...e, [f]: undefined }));
  }

  submit(event: Event): void {
    event.preventDefault();
    if (this.busy) return;
    const values: ArcRuleFields = {
      ...this.draft(),
      timeZoneId: this.draft().timeZoneId.trim(),
      formalDisapprovalStatement: this.draft().formalDisapprovalStatement.trim(),
    };
    const problems = validateArcRules({ ...values, formalDisapprovalStatement: this.draft().formalDisapprovalStatement });
    if (Object.keys(problems).length) {
      this.showErrors(problems);
      return;
    }
    this.errors.set({});
    this.submitted.emit(values);
  }

  /** Shows these messages in the summary (which takes focus) and under their fields. */
  showErrors(problems: Errors): void {
    this.errors.set({ ...problems });
    setTimeout(() => this.host.nativeElement.querySelector<HTMLElement>(`#${this.idPrefix}-summary`)?.focus());
  }

  focusField(event: Event, f: ArcRuleField): void {
    event.preventDefault();
    this.fieldElement(f)?.focus();
  }

  private fieldElement(f: ArcRuleField): HTMLElement | null {
    return this.host.nativeElement.querySelector<HTMLElement>(`#${this.fieldId(f)}`);
  }
}
