import { Component, computed, effect, inject, signal, untracked } from '@angular/core';
import {
  CommunityProfile, CommunitySettingsService, ProfileField, ProfilePatch,
} from '../../core/services/community-settings.service';
import { CompanyService } from '../../core/services/company.service';
import { ToastService } from '../../core/services/toast.service';
import { CommunityStatus } from '../../core/models';
import { AutosaveFieldDirective } from '../../shared/autosave-field.directive';
import { formatPlainDate } from '../../shared/date-format';
import {
  RATE_LIMITED_MESSAGE, SAVE_FAILED_MESSAGE, refusalCodeOf, refusalMessage, serverMessageOf,
} from '../../shared/refusal-messages';
import { CommunitySettingsContext } from './community-settings-context';
import { ChangeParentDialogComponent } from './change-parent-dialog.component';
import { todayIso } from './appoint-dialog.component';

interface FieldDef {
  id: ProfileField;
  /** The settings registry's id, for settings search (`data-setting-id`). */
  settingId: string;
  label: string;
  required: boolean;
  kind: 'text' | 'date' | 'textarea';
  helper: string | null;
  max: number | null;
}

const FIELDS: readonly FieldDef[] = [
  { id: 'legalName', settingId: 'legal-name', label: 'Legal name', required: true, kind: 'text', helper: 'Up to 200 characters.', max: 200 },
  {
    id: 'communityName', settingId: 'community-name', label: 'Community name', required: true, kind: 'text', max: 80,
    helper: "2 to 80 characters. It must differ from every other community's name, including archived ones. Capital letters and spaces at the start or end don't make a name different.",
  },
  { id: 'county', settingId: 'county', label: 'County', required: true, kind: 'text', helper: null, max: null },
  { id: 'formationDate', settingId: 'formation-date', label: 'Formation date', required: true, kind: 'date', helper: "MM/DD/YYYY. Can't be in the future.", max: null },
  { id: 'managementStartDate', settingId: 'management-start-date', label: 'Management start date', required: true, kind: 'date', helper: 'MM/DD/YYYY.', max: null },
  { id: 'description', settingId: 'description', label: 'Description', required: false, kind: 'textarea', helper: 'Up to 2,000 characters.', max: 2000 },
];

const STATUS_SENTENCES: Record<CommunityStatus, string> = {
  Onboarding: 'Being set up. Hidden from residents. Resident invitations are scheduled and sent when it is marked Active.',
  Active: 'Live. Residents can use their homes.',
  Archived: 'No longer managed by the management company. Read-only.',
};

const MISSING_HELP = "Still empty. You can save other changes without it. Once it has a value, it can't be cleared.";

interface SubAssociation {
  id: string;
  communityName: string;
  status: CommunityStatus;
}

/** "A", "A and B", "A, B and C" (brief 04 §5.1 writes no serial comma). */
function joinAnd(items: string[]): string {
  return items.length <= 1 ? (items[0] ?? '') : `${items.slice(0, -1).join(', ')} and ${items[items.length - 1]}`;
}

/** A rejection shaped like the server's `VALIDATION_ERROR`, so the field shows it with no "Try again". */
function invalid(field: ProfileField, message: string): Promise<never> {
  return Promise.reject({ status: 400, error: { code: 'VALIDATION_ERROR', message, errors: [{ field, message }] } });
}

/**
 * 030 US3 / FR-017 / brief 04 §4.2: the Profile section of Community settings.
 *
 * - **Details**: the six profile fields. For anyone who may edit, each is an input that saves on its
 *   own when the person finishes it (`AutosaveFieldDirective`: "Saving…", "✓ Saved", the error under
 *   the field). A required field that has a value can't be cleared; one still empty from the 025
 *   backfill may stay empty, and is flagged here and in the notice at the top (D8).
 * - **Status and parent**: the status in words, the parent with "Change parent" (Company
 *   Administrators, not while archived), and the sub-associations (Company Administrators).
 *
 * The status actions (Mark Active, Archive, Restore) sit in the page header (brief 04 §4.1).
 * An Archived community shows its values as plain text.
 */
@Component({
  selector: 'app-profile-section',
  standalone: true,
  imports: [AutosaveFieldDirective, ChangeParentDialogComponent],
  template: `
    <h2 class="section-title pf__title" tabindex="-1" data-section-heading>Profile</h2>

    @if (banner()) { <div class="card card--lav" role="alert">{{ banner() }}</div> }

    @switch (state()) {
      @case ('loading') {
        <p class="sr-only" role="status">Loading the profile…</p>
        <div class="ph pf__ph" aria-hidden="true">Profile</div>
      }
      @case ('error') {
        <div class="card pf__error" role="alert">
          <p>Couldn't load the profile.</p>
          <button type="button" class="btn" (click)="retry()">Try again</button>
        </div>
      }
      @default {
        @if (profile(); as p) {
          @if (missingNotice()) { <div class="card card--lav">{{ missingNotice() }}</div> }

          <section class="card pf__card" aria-labelledby="pf-details-title">
            <h3 class="section-title" id="pf-details-title">Details</h3>
            @if (editable()) {
              <div class="pf__fields">
                @for (f of fields; track f.id) {
                  <div class="pf__field" [class.pf__field--wide]="f.kind === 'textarea'">
                    <div class="pf__label-row">
                      <label class="field-label" #lbl [for]="'pf-' + f.id">{{ f.label }} {{ f.required ? '(required)' : '(optional)' }}</label>
                      @if (isMissing(f.id)) { <span class="pill pill--warn pf__missing">Missing — required</span> }
                    </div>
                    @if (f.helper) { <p class="field-help" [id]="'pf-' + f.id + '-help'">{{ f.helper }}</p> }
                    @if (isMissing(f.id)) { <p class="field-help" [id]="'pf-' + f.id + '-missing'">{{ missingHelp }}</p> }
                    @if (f.kind === 'textarea') {
                      <textarea class="field" rows="4" [id]="'pf-' + f.id" [attr.data-setting-id]="f.settingId"
                                [attr.aria-describedby]="describedBy(f)" [value]="valueOf(f.id)"
                                (input)="descriptionLength.set($any($event.target).value.length)"
                                [appAutosaveField]="savers[f.id]" [autosaveBaseline]="valueOf(f.id)" [autosaveLabel]="f.label"
                                [autosaveStatusHost]="lbl" [autosaveErrorMessage]="errorMessage"
                                (autosaveSaved)="onSaved(f.id, $event)" (autosaveFailed)="onFailed($event)"></textarea>
                      <p class="field-help"><span aria-hidden="true">{{ descriptionLength() }} / 2,000</span><span class="sr-only">{{ descriptionLength() }} of 2,000 characters used</span></p>
                    } @else {
                      <input class="field" [type]="f.kind" [id]="'pf-' + f.id" [attr.data-setting-id]="f.settingId"
                             [attr.aria-describedby]="describedBy(f)" [attr.maxlength]="f.max" [value]="valueOf(f.id)"
                             [attr.max]="f.id === 'formationDate' ? today : null"
                             [appAutosaveField]="savers[f.id]" [autosaveBaseline]="valueOf(f.id)" [autosaveLabel]="f.label"
                             [autosaveStatusHost]="lbl" [autosaveErrorMessage]="errorMessage"
                             (autosaveSaved)="onSaved(f.id, $event)" (autosaveFailed)="onFailed($event)" />
                    }
                  </div>
                }
              </div>
            } @else {
              <dl class="pf__dl">
                @for (f of fields; track f.id) {
                  <div class="pf__pair" [class.pf__field--wide]="f.kind === 'textarea'">
                    <dt>{{ f.label }}</dt>
                    <dd tabindex="-1" [attr.data-setting-id]="f.settingId">
                      @if (valueOf(f.id)) { <span>{{ display(f) }}</span> }
                      @else if (f.required) { <span aria-hidden="true">Missing — required</span><span class="sr-only">missing, required</span> }
                      @else { No description }
                    </dd>
                  </div>
                }
              </dl>
            }
          </section>
        }
      }
    }

    @if (settings(); as s) {
      <section class="card pf__card" aria-labelledby="pf-status-title">
        <h3 class="section-title" id="pf-status-title">Status and parent</h3>
        <dl class="pf__dl">
          <div class="pf__pair pf__field--wide">
            <dt>Status</dt>
            <dd tabindex="-1" data-setting-id="status"><span class="pill">{{ s.status }}</span> <span>{{ statusSentence() }}</span></dd>
          </div>
          <div class="pf__pair pf__field--wide">
            <dt>Parent association</dt>
            <dd tabindex="-1" data-setting-id="parent" class="pf__parent">
              <span>{{ s.parent?.communityName ?? 'None' }}</span>
              @if (canChangeParent()) {
                <button type="button" class="btn" [attr.aria-label]="'Change parent of ' + s.communityName"
                        (click)="parentOpen.set(true)">Change parent</button>
              }
            </dd>
          </div>
          @if (mode() === 'company') {
            <div class="pf__pair pf__field--wide">
              <dt id="pf-subs-label">Sub-associations</dt>
              <dd tabindex="-1" data-setting-id="sub-associations">
                @switch (subsState()) {
                  @case ('loading') { <span class="muted">Loading…</span> }
                  @case ('error') { <span class="muted">Couldn't load the sub-associations.</span> }
                  @default {
                    @if (subs().length) {
                      <ul class="pf__subs" aria-label="Sub-associations">
                        @for (c of subs(); track c.id) {
                          <li>{{ c.communityName }} · <span class="pill">{{ c.status }}</span></li>
                        }
                      </ul>
                    } @else { None }
                  }
                }
              </dd>
            </div>
          }
        </dl>
        @if (mode() === 'board') {
          <p class="muted">Only a Company Administrator can change the status or the parent association.</p>
        }
      </section>

      @if (parentOpen()) {
        <app-change-parent-dialog [communityId]="s.id" [communityName]="s.communityName" [currentParent]="s.parent"
                                  (done)="onParentChanged($event)" (cancelled)="parentOpen.set(false)" />
      }
    }
  `,
  styles: [`
    :host { display: flex; flex-direction: column; gap: 12px; }
    .pf__title { margin: 0; }
    .pf__ph { height: 220px; }
    .pf__error { display: flex; flex-direction: column; gap: 10px; align-items: flex-start; }
    .pf__card { display: flex; flex-direction: column; gap: 10px; }
    .pf__fields, .pf__dl { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 14px 20px; }
    .pf__field { display: flex; flex-direction: column; gap: 4px; min-width: 0; }
    .pf__field--wide { grid-column: 1 / -1; }
    .pf__label-row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
    .pf__missing { font-size: 10.5px; }
    .pf__pair dt { font-size: 11px; color: var(--ink-soft); font-weight: 600; }
    .pf__pair dd { margin: 2px 0 0; font-size: 13px; color: var(--ink); white-space: pre-line; }
    .pf__parent { display: flex; flex-wrap: wrap; align-items: center; gap: 10px; }
    .pf__subs { list-style: none; display: flex; flex-direction: column; gap: 4px; }
    @media (max-width: 767.98px) { .pf__fields, .pf__dl { grid-template-columns: minmax(0, 1fr); } }
  `],
})
export class ProfileSectionComponent {
  private ctx = inject(CommunitySettingsContext);
  private svc = inject(CommunitySettingsService);
  private company = inject(CompanyService);
  private toasts = inject(ToastService);

  readonly fields = FIELDS;
  readonly missingHelp = MISSING_HELP;
  readonly today = todayIso();

  readonly settings = this.ctx.settings;
  readonly mode = this.ctx.mode;
  readonly state = signal<'loading' | 'ready' | 'error'>('loading');
  readonly profile = signal<CommunityProfile | null>(null);
  readonly banner = signal<string | null>(null);
  readonly descriptionLength = signal(0);
  readonly parentOpen = signal(false);
  readonly subs = signal<SubAssociation[]>([]);
  readonly subsState = signal<'loading' | 'ready' | 'error'>('loading');

  readonly editable = computed(() => {
    const s = this.settings();
    return !!s && s.can.editProfile && s.status !== 'Archived';
  });

  readonly canChangeParent = computed(() => {
    const s = this.settings();
    return !!s && this.mode() === 'company' && s.can.changeParent && s.status !== 'Archived';
  });

  readonly statusSentence = computed(() => {
    const s = this.settings();
    return s ? STATUS_SENTENCES[s.status] ?? '' : '';
  });

  private readonly missing = computed(() => new Set(this.profile()?.missingRequiredFields ?? []));

  /** D8: "4 required fields are empty: Legal name, County, … Add them when you have them." */
  readonly missingNotice = computed(() => {
    const labels = FIELDS.filter(f => f.required && this.missing().has(f.id)).map(f => f.label);
    if (!labels.length) return null;
    return labels.length === 1
      ? `1 required field is empty: ${labels[0]}. Add it when you have it.`
      : `${labels.length} required fields are empty: ${joinAnd(labels)}. Add them when you have them.`;
  });

  /** One save function per field, made once so the directive's input stays the same. */
  readonly savers = Object.fromEntries(FIELDS.map(f => [f.id, (value: string) => this.save(f, value)])) as Record<ProfileField, (value: string) => Promise<unknown>>;

  readonly errorMessage = (err: unknown): string => {
    const fieldErrors = (err as { error?: { errors?: { message?: string }[] } })?.error?.errors;
    const first = Array.isArray(fieldErrors) ? fieldErrors.find(e => !!e?.message)?.message : undefined;
    if (first) return first;
    const code = refusalCodeOf(err);
    if (code === null) return SAVE_FAILED_MESSAGE;
    if (code === 'RATE_LIMITED') return RATE_LIMITED_MESSAGE;
    return refusalMessage(code, {
      audience: this.ctx.audience(), surface: 'action', communityName: this.settings()?.communityName,
      serverMessage: code === 'COMMUNITY_NAME_TAKEN' || code === 'VALIDATION_ERROR' ? serverMessageOf(err) : null,
    });
  };

  private loadSeq = 0;

  constructor() {
    effect(() => {
      const id = this.ctx.communityId();
      untracked(() => {
        if (!id) return;
        void this.load(id);
        if (this.mode() === 'company') void this.loadSubs(id);
      });
    }, { allowSignalWrites: true });
  }

  isMissing(field: ProfileField): boolean {
    return this.missing().has(field);
  }

  valueOf(field: ProfileField): string {
    return this.profile()?.[field] ?? '';
  }

  display(f: FieldDef): string {
    const v = this.valueOf(f.id);
    return f.kind === 'date' ? formatPlainDate(v) : v;
  }

  describedBy(f: FieldDef): string | null {
    const ids = [f.helper ? `pf-${f.id}-help` : null, this.isMissing(f.id) ? `pf-${f.id}-missing` : null].filter(Boolean);
    return ids.length ? ids.join(' ') : null;
  }

  retry(): void {
    const id = this.ctx.communityId();
    if (id) void this.load(id);
  }

  onSaved(field: ProfileField, result: unknown): void {
    const before = this.profile();
    const next = result as CommunityProfile;
    this.profile.set(next);
    this.banner.set(null);
    const missingChanged = (before?.missingRequiredFields ?? []).join() !== (next.missingRequiredFields ?? []).join();
    // The header shows the name and the legal name, and flags a missing legal name.
    if (field === 'communityName' || field === 'legalName' || missingChanged) void this.ctx.reload();
  }

  onFailed(err: unknown): void {
    const code = refusalCodeOf(err);
    if (code === 'COMMUNITY_ARCHIVED') {
      this.banner.set(refusalMessage(code, { audience: this.ctx.audience(), communityName: this.settings()?.communityName }));
      void this.ctx.reload();
    } else if (code === 'FORBIDDEN') {
      this.banner.set(refusalMessage(code, { surface: 'action' }));
    }
  }

  onParentChanged(message: string): void {
    this.parentOpen.set(false);
    this.toasts.show(message);
    void this.ctx.reload();
    const id = this.ctx.communityId();
    if (id) void this.loadSubs(id);
  }

  private save(f: FieldDef, raw: string): Promise<unknown> {
    const p = this.profile();
    const id = this.ctx.communityId();
    if (!p || !id) return Promise.reject(null);
    const value = f.kind === 'date' ? raw : raw.trim();
    if (f.required && !value) {
      // FR-017: a field that has a value can't be cleared; one still empty stays as it is.
      return p[f.id] ? invalid(f.id, `${f.label} is required and can't be left empty.`) : Promise.resolve(p);
    }
    if (f.id === 'communityName' && (value.length < 2 || value.length > 80)) return invalid(f.id, 'Community name must be 2 to 80 characters.');
    if (f.id === 'legalName' && value.length > 200) return invalid(f.id, 'Legal name can be at most 200 characters.');
    if (f.id === 'description' && value.length > 2000) return invalid(f.id, 'Description can be at most 2,000 characters.');
    if (f.id === 'formationDate' && value > todayIso()) return invalid(f.id, "Formation date can't be in the future.");
    const patch: ProfilePatch = { [f.id]: value || null };
    return this.svc.patchProfile(id, patch);
  }

  private async load(communityId: string): Promise<void> {
    const seq = ++this.loadSeq;
    if (!this.profile()) this.state.set('loading');
    try {
      const result = await this.svc.getProfile(communityId);
      if (seq !== this.loadSeq) return;
      this.profile.set(result);
      this.descriptionLength.set(result.description?.length ?? 0);
      this.state.set('ready');
    } catch {
      if (seq !== this.loadSeq) return;
      this.state.set('error');
    }
  }

  /** Company Administrators only: the communities whose parent is this one, from the portfolio. */
  private async loadSubs(communityId: string): Promise<void> {
    this.subsState.set('loading');
    try {
      const found: SubAssociation[] = [];
      for (let page = 0, offset = 0; page < 20; page++) {
        const res = await this.company.getCommunities({ limit: 100, offset, sort: 'name' });
        for (const c of res.items) {
          if (c.parent?.id === communityId) found.push({ id: c.id, communityName: c.communityName, status: c.status });
        }
        offset += res.items.length;
        if (!res.items.length || offset >= res.total) break;
      }
      this.subs.set(found.sort((a, b) => a.communityName.localeCompare(b.communityName, 'en', { sensitivity: 'base' })));
      this.subsState.set('ready');
    } catch {
      this.subsState.set('error');
    }
  }
}
