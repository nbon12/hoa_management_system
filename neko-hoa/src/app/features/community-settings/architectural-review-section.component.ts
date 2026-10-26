import { ChangeDetectorRef, Component, ElementRef, ViewChild, computed, effect, inject, signal, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CommunitySettingsService, SettingsArcSettings } from '../../core/services/community-settings.service';
import { ArcRuleFields } from '../../core/services/company.service';
import { ToastService } from '../../core/services/toast.service';
import { DialogComponent } from '../../shared/dialog.component';
import { ArcRulesViewComponent } from '../../shared/arc-rules-view.component';
import { ArcRulesFormComponent } from '../../shared/arc-rules-form.component';
import {
  ARC_RULE_FIELDS, ARC_RULE_LABELS, ARC_RULE_SHORT_LABELS, ArcRuleField, arcRuleServerErrors, arcRuleValueLabel, changedArcRules, pickArcRules,
} from '../../shared/arc-rules';
import { RATE_LIMITED_MESSAGE, SAVE_FAILED_MESSAGE, refusalCodeOf, refusalMessage } from '../../shared/refusal-messages';
import { CommunitySettingsContext } from './community-settings-context';

/** "A", "A and B", "A, B and C" (brief 07 §5 writes no serial comma). */
function joinAnd(items: string[]): string {
  return items.length <= 1 ? (items[0] ?? '') : `${items.slice(0, -1).join(', ')} and ${items[items.length - 1]}`;
}

interface ChangeRow {
  label: string;
  from: string;
  to: string;
}

/**
 * 030 US5 / FR-030, FR-031 / brief 07: spec 027's ARC settings as the Architectural review section
 * of Community settings. A read view flags each field that differs from the company defaults, with
 * the default under it; "Edit architectural review" opens one form with one Save (D1); "Reset
 * architectural review to company defaults" confirms what changes first and never touches
 * application numbering. The "Company defaults" link opens that page, read-only for a Community
 * Manager (FR-027). In an Archived community the section is plain text.
 */
@Component({
  selector: 'app-architectural-review-section',
  standalone: true,
  imports: [RouterLink, DialogComponent, ArcRulesViewComponent, ArcRulesFormComponent],
  template: `
    <h2 class="section-title ar__title" id="ar-title" tabindex="-1" data-section-heading>Architectural review</h2>
    <p class="muted">Set these to match your governing documents. Changes apply to applications received from now on.</p>

    @switch (state()) {
      @case ('loading') {
        <p class="sr-only" role="status">Loading architectural review settings…</p>
        <div class="ph ar__ph" aria-hidden="true"></div>
      }
      @case ('error') {
        <div class="card ar__error" role="alert">
          <p>Couldn't load the architectural review settings.</p>
          <button type="button" class="btn" (click)="retry()">Try again</button>
        </div>
      }
      @default {
        @if (arc(); as a) {
          <div class="ar__bar">
            <p>{{ summary() }}</p>
            @if (!readOnly()) {
              <p class="ar__link">
                <a class="link" [routerLink]="defaultsPath()">Company defaults</a>
                <span class="muted">{{ linkLine() }}</span>
              </p>
            }
          </div>

          <section class="card ar__card" aria-labelledby="ar-rules-title">
            <div class="ar__head">
              <h3 class="section-title" id="ar-rules-title">Review rules</h3>
              @if (editable() && !editing()) {
                <button type="button" class="btn ar__edit" #editButton (click)="startEdit()">Edit architectural review</button>
              }
            </div>
            @if (banner()) { <p class="field-error" role="alert">{{ banner() }}</p> }
            @if (editing()) {
              <app-arc-rules-form [value]="a" [defaults]="a.companyDefaults" variant="community" idPrefix="ar" [busy]="busy()"
                                  summaryHeading="The architectural review settings weren't saved" saveLabel="Save architectural review"
                                  (submitted)="save($event)" (cancelled)="cancelEdit()" />
            } @else {
              <app-arc-rules-view [value]="a" [defaults]="a.companyDefaults" [differs]="a.differsFromDefaults" />
            }
          </section>

          @if (editable() && !editing()) {
            <section class="card ar__reset" aria-label="Reset to company defaults">
              <button type="button" class="btn" #resetButton [attr.aria-disabled]="matches() ? 'true' : null"
                      [attr.aria-describedby]="matches() ? 'ar-reset-reason ar-reset-help' : 'ar-reset-help'"
                      (click)="openReset()">Reset architectural review to company defaults</button>
              @if (matches()) { <p class="muted" id="ar-reset-reason">These settings already match the company defaults.</p> }
              <p class="field-help" id="ar-reset-help">Sets every rule above to the current company defaults. Applications already received keep their rules, and application numbering doesn't change.</p>
            </section>
          }
        }
      }
    }

    @if (resetOpen()) {
      <app-dialog [heading]="'Reset ' + name() + '\\'s architectural review to the company defaults?'" (dismiss)="closeReset()">
        @if (resetError()) { <p class="dlg-refusal field-error" role="alert" tabindex="-1">{{ resetError() }}</p> }
        @switch (resetState()) {
          @case ('loading') { <p class="sr-only" role="status">Loading what this will change…</p><div class="ph ar__ph-sm" aria-hidden="true"></div> }
          @case ('error') {
            <div class="dlg-refusal" role="alert">
              <p class="field-error">Couldn't load what this will change.</p>
              <button type="button" class="btn" (click)="loadReset()">Try again</button>
            </div>
          }
          @default {
            @if (resetChanges().length) {
              <div class="dlg-section">
                <h3 id="ar-reset-changes">These settings change:</h3>
                <ul aria-labelledby="ar-reset-changes">
                  @for (c of resetChanges(); track c.label) {
                    <li>{{ c.label }}: <span aria-hidden="true">{{ c.from }} → {{ c.to }}</span><span class="sr-only"> from {{ c.from }} to {{ c.to }}</span></li>
                  }
                </ul>
              </div>
            } @else {
              <p class="dlg-section">These settings already match the company defaults.</p>
            }
            @if (resetUnchanged()) { <p class="dlg-section">These already match and don't change: {{ resetUnchanged() }}.</p> }
            <p class="dlg-section">The new settings apply to applications received from now on.</p>
            <div class="dlg-section">
              <h3>What doesn't change</h3>
              <ul>
                <li>Applications already received keep their rules. Their due dates, lapse rule and decision rule don't change.</li>
                <li>Application numbering doesn't change. The next application gets the number it would have had.</li>
              </ul>
            </div>
          }
        }
        <div class="dlg-actions">
          @if (resetRefused()) {
            <button type="button" class="btn btn--primary" (click)="closeAfterRefusal()">Close</button>
          } @else {
            <button type="button" class="btn btn--primary" [disabled]="busy() || resetState() !== 'ready'" (click)="confirmReset()">{{ busy() ? 'Resetting…' : 'Reset architectural review' }}</button>
            <button type="button" class="btn" [disabled]="busy()" (click)="closeReset()">Cancel</button>
          }
        </div>
      </app-dialog>
    }
  `,
  styles: [`
    :host { display: flex; flex-direction: column; gap: 12px; }
    .ar__title { margin: 0; }
    .ar__ph { height: 200px; }
    .ar__ph-sm { height: 80px; margin-top: 10px; }
    .ar__error { display: flex; flex-direction: column; gap: 10px; align-items: flex-start; }
    .ar__bar { display: flex; flex-wrap: wrap; justify-content: space-between; gap: 6px 16px; font-size: 13px; }
    .ar__link { display: flex; flex-direction: column; gap: 2px; max-width: 420px; }
    .ar__card { display: flex; flex-direction: column; gap: 10px; }
    .ar__head { display: flex; flex-wrap: wrap; align-items: center; gap: 10px; }
    .ar__edit { margin-left: auto; }
    .ar__reset { border-style: dashed; display: flex; flex-direction: column; gap: 6px; align-items: flex-start; }
  `],
})
export class ArchitecturalReviewSectionComponent {
  private ctx = inject(CommunitySettingsContext);
  private svc = inject(CommunitySettingsService);
  private toasts = inject(ToastService);
  private cdr = inject(ChangeDetectorRef);

  @ViewChild('editButton') private editButton?: ElementRef<HTMLButtonElement>;
  @ViewChild(ArcRulesFormComponent) private form?: ArcRulesFormComponent;

  readonly state = signal<'loading' | 'ready' | 'error'>('loading');
  readonly arc = signal<SettingsArcSettings | null>(null);
  readonly editing = signal(false);
  readonly busy = signal(false);
  readonly banner = signal<string | null>(null);

  readonly name = computed(() => this.ctx.settings()?.communityName ?? '');
  readonly readOnly = computed(() => this.ctx.readOnly());
  readonly editable = computed(() => !this.readOnly() && this.arc()?.canEdit === true);
  readonly matches = computed(() => (this.arc()?.differsFromDefaults.length ?? 0) === 0);
  readonly defaultsPath = computed(() => (this.ctx.mode() === 'company' ? '/app/company/defaults' : '/app/board/company-defaults'));
  readonly linkLine = computed(() => this.ctx.mode() === 'company'
    ? "New communities start with these. Changing them never changes this community's settings or applications already received."
    : "New communities start with these. You can read them; only a Company Administrator can change them. Changing them never changes this community's settings.");

  /** D2: the summary line. */
  readonly summary = computed(() => {
    const differs = ARC_RULE_FIELDS.filter(f => this.arc()?.differsFromDefaults.includes(f));
    if (!differs.length) return 'These settings match the company defaults.';
    const names = joinAnd(differs.map(f => ARC_RULE_SHORT_LABELS[f]));
    return differs.length === 1
      ? `1 setting differs from the company defaults: ${names}.`
      : `${differs.length} settings differ from the company defaults: ${names}.`;
  });

  // Reset dialog
  readonly resetOpen = signal(false);
  readonly resetState = signal<'loading' | 'ready' | 'error'>('loading');
  readonly resetFresh = signal<SettingsArcSettings | null>(null);
  readonly resetError = signal<string | null>(null);
  readonly resetRefused = signal(false);

  readonly resetChanges = computed<ChangeRow[]>(() => {
    const a = this.resetFresh();
    if (!a) return [];
    const show = (f: ArcRuleField, v: ArcRuleFields) => (f === 'formalDisapprovalStatement' ? `“${v[f]}”` : arcRuleValueLabel(f, v[f]));
    return changedArcRules(pickArcRules(a), a.companyDefaults).map(f => ({ label: ARC_RULE_LABELS[f], from: show(f, a), to: show(f, a.companyDefaults) }));
  });

  readonly resetUnchanged = computed(() => {
    const a = this.resetFresh();
    if (!a) return '';
    const changed = changedArcRules(pickArcRules(a), a.companyDefaults);
    return joinAnd(ARC_RULE_FIELDS.filter(f => !changed.includes(f)).map(f => ARC_RULE_SHORT_LABELS[f]));
  });

  private loadSeq = 0;

  constructor() {
    effect(() => {
      const id = this.ctx.communityId();
      untracked(() => { if (id) void this.load(id); });
    }, { allowSignalWrites: true });
  }

  retry(): void {
    const id = this.ctx.communityId();
    if (id) void this.load(id);
  }

  startEdit(): void {
    this.banner.set(null);
    this.editing.set(true);
  }

  cancelEdit(): void {
    this.editing.set(false);
    this.banner.set(null);
    this.focusAfterRender(() => this.editButton?.nativeElement);
  }

  async save(values: ArcRuleFields): Promise<void> {
    const id = this.ctx.communityId();
    const current = this.arc();
    if (!id || !current || this.busy()) return;
    if (!changedArcRules(pickArcRules(current), values).length) {
      this.cancelEdit();
      return;
    }
    this.busy.set(true);
    this.banner.set(null);
    try {
      this.arc.set(await this.svc.putArcSettings(id, values));
      this.editing.set(false);
      this.toasts.show('Architectural review saved.');
      this.focusAfterRender(() => this.editButton?.nativeElement);
    } catch (err) {
      this.onWriteError(err, true);
    } finally {
      this.busy.set(false);
    }
  }

  openReset(): void {
    if (this.matches()) return;
    this.resetError.set(null);
    this.resetRefused.set(false);
    this.resetOpen.set(true);
    void this.loadReset();
  }

  /** The dialog asks the server again, so it lists the defaults current when it opens (Q2). */
  async loadReset(): Promise<void> {
    const id = this.ctx.communityId();
    if (!id) return;
    this.resetState.set('loading');
    try {
      this.resetFresh.set(await this.svc.getArcSettings(id));
      this.resetState.set('ready');
    } catch {
      this.resetState.set('error');
    }
  }

  closeReset(): void {
    if (!this.busy()) this.resetOpen.set(false);
  }

  closeAfterRefusal(): void {
    this.resetOpen.set(false);
    void this.ctx.reload();
  }

  async confirmReset(): Promise<void> {
    const id = this.ctx.communityId();
    if (!id || this.busy()) return;
    this.busy.set(true);
    this.resetError.set(null);
    try {
      this.arc.set(await this.svc.resetArc(id));
      this.resetOpen.set(false);
      this.toasts.show('Architectural review reset to the company defaults.');
      // The reset button is now disabled (D4), so focus goes to the section heading.
      this.focusAfterRender(() => document.getElementById('ar-title'));
    } catch (err) {
      this.onWriteError(err, false);
    } finally {
      this.busy.set(false);
    }
  }

  private onWriteError(err: unknown, fromForm: boolean): void {
    const code = refusalCodeOf(err);
    const say = (message: string) => (fromForm ? this.banner.set(message) : this.resetError.set(message));
    if (code === 'VALIDATION_ERROR' && fromForm) {
      const errors = arcRuleServerErrors(err);
      if (Object.keys(errors).length) this.form?.showErrors(errors);
      else say(refusalMessage(code));
      return;
    }
    if (code === null) {
      say(fromForm ? SAVE_FAILED_MESSAGE : "Couldn't reset the architectural review. Check your connection and try again.");
      return;
    }
    if (code === 'RATE_LIMITED') {
      say(RATE_LIMITED_MESSAGE);
      return;
    }
    say(refusalMessage(code, { communityName: this.name(), audience: this.ctx.audience(), surface: 'action' }));
    if (!fromForm) this.resetRefused.set(true);
    // Archived in another tab: the page reloads read-only.
    if (code === 'COMMUNITY_ARCHIVED' && fromForm) void this.ctx.reload();
  }

  private async load(communityId: string): Promise<void> {
    const seq = ++this.loadSeq;
    if (!this.arc()) this.state.set('loading');
    try {
      const result = await this.svc.getArcSettings(communityId);
      if (seq !== this.loadSeq) return;
      this.arc.set(result);
      this.state.set('ready');
    } catch {
      if (seq === this.loadSeq) this.state.set('error');
    }
  }

  /** Renders first, so a closing dialog's own focus return runs before this one. */
  private focusAfterRender(target: () => HTMLElement | null | undefined): void {
    this.cdr.detectChanges();
    setTimeout(() => target()?.focus());
  }
}
