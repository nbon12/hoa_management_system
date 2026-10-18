import { ChangeDetectorRef, Component, ElementRef, Input, OnInit, ViewChild, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { ArcRuleFields, CompanyDefaults, CompanyService } from '../../core/services/company.service';
import { AuthService } from '../../core/services/auth.service';
import { BoardNavigationService } from '../../core/services/board-navigation.service';
import { ToastService } from '../../core/services/toast.service';
import { landingTargetFor } from '../../core/services/landing';
import { DialogComponent } from '../../shared/dialog.component';
import { ArcRulesViewComponent } from '../../shared/arc-rules-view.component';
import { ArcRulesFormComponent } from '../../shared/arc-rules-form.component';
import { ARC_RULE_LABELS, ArcRuleField, arcRuleServerErrors, arcRuleValueLabel, changedArcRules, pickArcRules } from '../../shared/arc-rules';
import { RATE_LIMITED_MESSAGE, SAVE_FAILED_MESSAGE, refusalCodeOf, refusalMessage } from '../../shared/refusal-messages';

interface ChangeRow {
  label: string;
  from: string;
  to: string;
}

/**
 * 030 US4 / FR-025–FR-028 / brief 08: the Company defaults page, one section ("Architectural
 * review", D3). New communities start with these rules; a change never reaches existing
 * communities or applications (FR-026), and the page never offers to push it there.
 *
 * - `/app/company/defaults` (`mode: 'company'`): a Company Administrator reads the rules, opens one
 *   form ("Edit company defaults", D1), and saves after a confirmation listing the changes.
 * - `/app/board/company-defaults` (`mode: 'board'`): a Community Manager reads them, with a link
 *   back to the active community's Architectural review section (D2). No controls at all.
 */
@Component({
  selector: 'app-company-defaults',
  standalone: true,
  imports: [RouterLink, DialogComponent, ArcRulesViewComponent, ArcRulesFormComponent],
  template: `
    @if (mode === 'board') {
      <a class="link cd__back" routerLink="/app/board/settings/architectural-review" [attr.aria-label]="backLabel()">Back to architectural review</a>
    }
    <div class="page-header">
      <h1 class="page-title" id="cd-title" tabindex="-1">Company defaults</h1>
      <p class="muted">Every community added from now on starts with these settings. Each community can then change its own.</p>
    </div>

    <h2 class="section-title">Architectural review</h2>

    <section class="card card--lav cd__note" aria-labelledby="cd-note-title">
      <h3 class="section-title" id="cd-note-title">Changes apply only to new communities</h3>
      @if (editable()) {
        <p>A change here applies only to communities added after you save it. Existing communities keep their own settings, including communities that never changed them. Applications already received keep their rules. A change can't be applied to existing communities. To change one community, edit its Architectural review settings in Community settings.</p>
      } @else {
        <p>A change here applies only to communities added afterwards. Existing communities, including yours, keep their own settings, and applications already received keep their rules.</p>
      }
    </section>

    <section class="card cd__card" aria-labelledby="cd-rules-title">
      <div class="cd__head">
        <h3 class="section-title" id="cd-rules-title">Review rules</h3>
        @if (state() === 'ready' && !editing()) {
          @if (editable()) {
            <button type="button" class="btn cd__edit" #editButton (click)="startEdit()">Edit company defaults</button>
          } @else {
            <span class="pill">Read-only</span>
          }
        }
      </div>
      @if (!editable()) { <p class="muted">You can read the company defaults. Only a Company Administrator can change them.</p> }
      @if (banner()) { <p class="field-error" role="alert">{{ banner() }}</p> }

      @switch (state()) {
        @case ('loading') {
          <p class="sr-only" role="status">Loading company defaults…</p>
          <div class="ph cd__ph" aria-hidden="true"></div>
        }
        @case ('error') {
          <div role="alert" class="cd__error">
            <p>Couldn't load the company defaults.</p>
            <button type="button" class="btn" (click)="load()">Try again</button>
          </div>
        }
        @default {
          @if (saved(); as v) {
            @if (editing()) {
              <app-arc-rules-form [value]="draft() ?? v" variant="defaults" idPrefix="cd" summaryHeading="The company defaults weren't saved"
                                  saveLabel="Save company defaults" (submitted)="review($event)" (cancelled)="cancelEdit()" />
            } @else {
              <app-arc-rules-view [value]="v" />
            }
          }
        }
      }
    </section>

    @if (pending(); as p) {
      <app-dialog heading="Save the company defaults?" (dismiss)="closeDialog()">
        @if (dialogError()) { <p class="dlg-refusal field-error" role="alert" tabindex="-1">{{ dialogError() }}</p> }
        <div class="dlg-section">
          <h3 id="cd-changes">These settings change:</h3>
          <ul aria-labelledby="cd-changes">
            @for (c of changes(); track c.label) {
              <li>{{ c.label }}: <span aria-hidden="true">{{ c.from }} → {{ c.to }}</span><span class="sr-only"> from {{ c.from }} to {{ c.to }}</span></li>
            }
          </ul>
        </div>
        <div class="dlg-section">
          <h3>What happens</h3>
          <ul>
            <li>Communities added from now on start with these settings.</li>
            <li>Existing communities whose settings now differ will show “Differs from defaults” in the portfolio.</li>
          </ul>
        </div>
        <div class="dlg-section">
          <h3>What doesn't change</h3>
          <ul>
            <li>Existing communities keep their own architectural review settings, including communities that never changed them.</li>
            <li>Applications already received keep their rules.</li>
          </ul>
        </div>
        <div class="dlg-actions">
          @if (dialogClose()) {
            <button type="button" class="btn btn--primary" (click)="closeAfterRefusal()">Close</button>
          } @else {
            <button type="button" class="btn btn--primary" [disabled]="busy()" (click)="confirm(p)">{{ busy() ? 'Saving…' : 'Save company defaults' }}</button>
            <button type="button" class="btn" [disabled]="busy()" (click)="closeDialog()">Cancel</button>
          }
        </div>
      </app-dialog>
    }
  `,
  styles: [`
    :host { display: flex; flex-direction: column; gap: 14px; }
    .cd__back { align-self: flex-start; font-size: 12px; }
    .cd__note p { margin-top: 4px; }
    .cd__card { display: flex; flex-direction: column; gap: 10px; }
    .cd__head { display: flex; flex-wrap: wrap; align-items: center; gap: 10px; }
    .cd__head > :last-child:not(:first-child) { margin-left: auto; }
    .cd__ph { height: 160px; }
    .cd__error { display: flex; flex-direction: column; gap: 8px; align-items: flex-start; }
  `],
})
export class CompanyDefaultsComponent implements OnInit {
  /** From the route: `company` (the company area) or `board` (a Community Manager, read-only). */
  @Input() mode: 'company' | 'board' = 'company';

  @ViewChild('editButton') private editButton?: ElementRef<HTMLButtonElement>;
  @ViewChild(ArcRulesFormComponent) private form?: ArcRulesFormComponent;

  private company = inject(CompanyService);
  private auth = inject(AuthService);
  private nav = inject(BoardNavigationService);
  private toasts = inject(ToastService);
  private router = inject(Router);
  private cdr = inject(ChangeDetectorRef);

  readonly state = signal<'loading' | 'ready' | 'error'>('loading');
  readonly defaults = signal<CompanyDefaults | null>(null);
  readonly saved = computed(() => {
    const d = this.defaults();
    return d ? pickArcRules(d) : null;
  });
  readonly editing = signal(false);
  /** What the form holds after a server refusal, so the person's input is kept. */
  readonly draft = signal<ArcRuleFields | null>(null);
  readonly pending = signal<ArcRuleFields | null>(null);
  readonly busy = signal(false);
  readonly dialogError = signal<string | null>(null);
  readonly dialogClose = signal(false);
  readonly banner = signal<string | null>(null);

  /** `canEdit` is a UX hint; the board-mode page never edits (FR-027). */
  readonly editable = computed(() => this.mode === 'company' && this.defaults()?.canEdit === true);

  readonly changes = computed<ChangeRow[]>(() => {
    const before = this.saved();
    const after = this.pending();
    if (!before || !after) return [];
    const show = (f: ArcRuleField, v: ArcRuleFields) =>
      f === 'formalDisapprovalStatement' ? `“${v[f]}”` : arcRuleValueLabel(f, v[f]);
    return changedArcRules(before, after).map(f => ({ label: ARC_RULE_LABELS[f], from: show(f, before), to: show(f, after) }));
  });

  readonly backLabel = computed(() => {
    const id = this.nav.activeCommunityId();
    const name = this.auth.user()?.memberships.find(m => m.communityId === id)?.communityName;
    return name ? `Back to ${name}'s architectural review` : 'Back to architectural review';
  });

  private serverErrors: Partial<Record<ArcRuleField, string>> = {};

  ngOnInit(): void {
    void this.load();
    setTimeout(() => document.getElementById('cd-title')?.focus());
  }

  async load(): Promise<void> {
    if (!this.defaults()) this.state.set('loading');
    try {
      this.defaults.set(await this.company.getDefaults());
      this.state.set('ready');
    } catch (err) {
      if (refusalCodeOf(err) === 'FORBIDDEN') {
        this.toasts.show(refusalMessage('FORBIDDEN', { surface: 'page' }));
        const target = landingTargetFor(this.auth.user());
        this.nav.setActiveCommunity(target.activeCommunityId);
        void this.router.navigate(target.commands);
        return;
      }
      this.state.set('error');
    }
  }

  startEdit(): void {
    this.banner.set(null);
    this.draft.set(null);
    this.editing.set(true);
  }

  cancelEdit(): void {
    this.editing.set(false);
    this.draft.set(null);
    this.focusEdit();
  }

  /** The client checks passed: confirm the changes, or close the form if nothing changed. */
  review(values: ArcRuleFields): void {
    const before = this.saved();
    if (!before) return;
    if (!changedArcRules(before, values).length) {
      this.cancelEdit();
      return;
    }
    this.draft.set(values);
    this.dialogError.set(null);
    this.dialogClose.set(false);
    this.serverErrors = {};
    this.pending.set(values);
  }

  closeDialog(): void {
    if (this.busy()) return;
    this.pending.set(null);
  }

  async confirm(values: ArcRuleFields): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    this.dialogError.set(null);
    try {
      const result = await this.company.putDefaults(values);
      this.defaults.set(result);
      this.pending.set(null);
      this.editing.set(false);
      this.draft.set(null);
      this.toasts.show('Company defaults saved.');
      this.focusEdit();
    } catch (err) {
      this.onSaveError(err);
    } finally {
      this.busy.set(false);
    }
  }

  /** After a refusal in the dialog: back to the form with its messages, or a re-read for FORBIDDEN. */
  closeAfterRefusal(): void {
    this.pending.set(null);
    const errors = this.serverErrors;
    if (Object.keys(errors).length) {
      setTimeout(() => this.form?.showErrors(errors));
      return;
    }
    this.editing.set(false);
    this.draft.set(null);
    void this.load();
  }

  private onSaveError(err: unknown): void {
    const code = refusalCodeOf(err);
    if (code === null) {
      this.dialogError.set(SAVE_FAILED_MESSAGE);
    } else if (code === 'RATE_LIMITED') {
      this.dialogError.set(RATE_LIMITED_MESSAGE);
    } else if (code === 'VALIDATION_ERROR') {
      this.serverErrors = arcRuleServerErrors(err);
      const first = Object.values(this.serverErrors)[0];
      this.dialogError.set(first ?? refusalMessage(code));
      this.dialogClose.set(true);
    } else {
      this.dialogError.set(refusalMessage(code, { surface: 'action', audience: 'companyAdmin' }));
      this.dialogClose.set(true);
    }
  }

  /** Renders first, so a closing dialog's own focus return runs before this one. */
  private focusEdit(): void {
    this.cdr.detectChanges();
    setTimeout(() => this.editButton?.nativeElement.focus());
  }
}
