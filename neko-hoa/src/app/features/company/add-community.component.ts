import { AfterViewChecked, Component, ElementRef, Input, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { CompanyService, CreateCommunityRequest } from '../../core/services/company.service';
import { BoardService } from '../../core/services/board.service';
import { AuthService } from '../../core/services/auth.service';
import { BoardNavigationService } from '../../core/services/board-navigation.service';
import { ToastService } from '../../core/services/toast.service';
import {
  RATE_LIMITED_MESSAGE, SAVE_FAILED_MESSAGE, refusalCodeOf, refusalMessage, serverMessageOf,
} from '../../shared/refusal-messages';
import { ComboPickerComponent, PickerOption } from '../../shared/combo-picker.component';
import { loadParentOptions, toPickerOptions } from './community-options';

type Field = 'legalName' | 'communityName' | 'county' | 'formationDate' | 'managementStartDate' | 'description' | 'parentCommunityId';

/** Summary labels: the field labels without "(required)" / "(optional)" (brief 03 §5). */
const FIELD_LABELS: Record<Field, string> = {
  legalName: 'Legal name',
  communityName: 'Community name',
  county: 'County',
  formationDate: 'Formation date',
  managementStartDate: 'Management start date',
  description: 'Description',
  parentCommunityId: 'Parent association',
};
const FIELD_ORDER = Object.keys(FIELD_LABELS) as Field[];
const FIELD_IDS: Record<Field, string> = {
  legalName: 'ac-legal-name', communityName: 'ac-community-name', county: 'ac-county',
  formationDate: 'ac-formation-date', managementStartDate: 'ac-management-start-date',
  description: 'ac-description', parentCommunityId: '',
};

/**
 * 030 US1 / FR-006–FR-009, FR-016 / brief 03: add a community the company has taken on.
 * - `company` variant (`/app/company/communities/new`): a Company Administrator; it starts with no manager.
 * - `board` variant (`/app/board/communities/new`): a Community Manager, who becomes its first,
 *   open-ended Community Manager (FR-008).
 * It always starts Onboarding. The server checks the name (FR-007) and the parent; the client
 * checks required fields, lengths and dates first.
 */
@Component({
  selector: 'app-add-community',
  standalone: true,
  imports: [FormsModule, RouterLink, ComboPickerComponent],
  template: `
    @if (variant === 'company') {
      <nav aria-label="Breadcrumb">
        <ol class="ac__crumbs">
          <li><a class="link" routerLink="/app/company/portfolio">Portfolio</a></li>
          <li><span aria-hidden="true">›</span> <span aria-current="page">Add community</span></li>
        </ol>
      </nav>
    }
    <h1 class="page-title" tabindex="-1">Add community</h1>
    @if (variant === 'company') {
      <p class="muted">Add a community that the company has taken on. It starts as Onboarding: residents can't see it until a Company Administrator marks it Active. It has no Community Manager until one is appointed.</p>
    } @else {
      <p class="muted">Add a community that the company has taken on.</p>
      <div class="card card--lav">You'll be its first Community Manager, with no end date. It starts as Onboarding: residents can't see it until a Company Administrator marks it Active. When you add it, we'll email every Company Administrator that it's waiting.</div>
    }

    @if (banner()) {
      <div class="alert alert--error" id="ac-banner" role="alert" tabindex="-1">{{ banner() }}</div>
    }
    @if (summary().length) {
      <div class="error-summary" id="ac-summary" role="alert" aria-labelledby="ac-summary-title" tabindex="-1">
        <h2 class="section-title" id="ac-summary-title">The community wasn't added</h2>
        <ul>
          @for (e of summary(); track e.field) {
            <li><a [href]="'#' + fieldId(e.field)" (click)="focusField($event, e.field)">{{ e.label }}: {{ e.message }}</a></li>
          }
        </ul>
      </div>
    }

    <form class="card ac__form" (ngSubmit)="save()" novalidate>
      <div>
        <label class="field-label" for="ac-legal-name">Legal name (required)</label>
        <input class="field" id="ac-legal-name" name="legalName" [(ngModel)]="form.legalName"
               [attr.aria-invalid]="invalid('legalName')" [attr.aria-describedby]="describedBy('legalName', 'ac-legal-name-help')" />
        <p class="field-help" id="ac-legal-name-help">Up to 200 characters.</p>
        @if (errors().legalName; as m) { <p class="field-error" id="ac-legal-name-error">{{ m }}</p> }
      </div>
      <div>
        <label class="field-label" for="ac-community-name">Community name (required)</label>
        <input class="field" id="ac-community-name" name="communityName" [(ngModel)]="form.communityName"
               [attr.aria-invalid]="invalid('communityName')" [attr.aria-describedby]="describedBy('communityName', 'ac-community-name-help')" />
        <p class="field-help" id="ac-community-name-help">The name people see in the app. 2 to 80 characters. No other community can have this name, even with different capital letters.</p>
        @if (errors().communityName; as m) { <p class="field-error" id="ac-community-name-error">{{ m }}</p> }
      </div>
      <div>
        <label class="field-label" for="ac-county">County (required)</label>
        <input class="field" id="ac-county" name="county" [(ngModel)]="form.county"
               [attr.aria-invalid]="invalid('county')" [attr.aria-describedby]="describedBy('county', null)" />
        @if (errors().county; as m) { <p class="field-error" id="ac-county-error">{{ m }}</p> }
      </div>
      <div class="ac__dates">
        <div>
          <label class="field-label" for="ac-formation-date">Formation date (required)</label>
          <input class="field" id="ac-formation-date" name="formationDate" type="date" [(ngModel)]="form.formationDate"
                 [attr.aria-invalid]="invalid('formationDate')" [attr.aria-describedby]="describedBy('formationDate', 'ac-formation-date-help')" />
          <p class="field-help" id="ac-formation-date-help">MM/DD/YYYY. Can't be in the future.</p>
          @if (errors().formationDate; as m) { <p class="field-error" id="ac-formation-date-error">{{ m }}</p> }
        </div>
        <div>
          <label class="field-label" for="ac-management-start-date">Management start date (required)</label>
          <input class="field" id="ac-management-start-date" name="managementStartDate" type="date" [(ngModel)]="form.managementStartDate"
                 [attr.aria-invalid]="invalid('managementStartDate')" [attr.aria-describedby]="describedBy('managementStartDate', 'ac-management-start-date-help')" />
          <p class="field-help" id="ac-management-start-date-help">MM/DD/YYYY. When the company starts managing it. This can be a future date.</p>
          @if (errors().managementStartDate; as m) { <p class="field-error" id="ac-management-start-date-error">{{ m }}</p> }
        </div>
      </div>
      <div>
        <label class="field-label" for="ac-description">Description (optional)</label>
        <textarea class="field" id="ac-description" name="description" rows="4" [(ngModel)]="form.description"
                  [attr.aria-invalid]="invalid('description')" [attr.aria-describedby]="describedBy('description', 'ac-description-help ac-description-count')"></textarea>
        <div class="ac__counted">
          <p class="field-help" id="ac-description-help">Up to 2,000 characters.</p>
          <p class="field-help" id="ac-description-count"><span aria-hidden="true">{{ count() }} / 2,000</span><span class="sr-only">{{ count() }} of 2,000 characters used</span></p>
        </div>
        @if (errors().description; as m) { <p class="field-error" id="ac-description-error">{{ m }}</p> }
      </div>
      <app-combo-picker label="Parent association (optional)" placeholder="None" noneLabel="None — not a sub-association"
                        [noun]="['community', 'communities']" [helper]="parentHelper()" [options]="parents()"
                        [value]="form.parentCommunityId" (valueChange)="form.parentCommunityId = $event"
                        [error]="errors().parentCommunityId ?? null" [noMatchMessage]="noMatch" />
      <hr class="divider" />
      <p class="muted">Architectural review settings are copied from the company defaults when you add the community. Later changes to the company defaults don't change them.</p>
      <div class="ac__buttons">
        <button type="submit" class="btn btn--primary" [disabled]="busy()">{{ busy() ? 'Saving…' : 'Add community' }}</button>
        <button type="button" class="btn btn--ghost" (click)="cancel()">Cancel</button>
        @if (rateLimited()) { <p class="field-error" role="alert">{{ rateLimitedMessage }}</p> }
      </div>
    </form>
  `,
  styles: [`
    :host { display: flex; flex-direction: column; gap: 12px; max-width: 760px; }
    .ac__crumbs { list-style: none; display: flex; gap: 6px; font-size: 12px; color: var(--ink-soft); }
    .ac__form { display: flex; flex-direction: column; gap: 14px; }
    .ac__dates { display: grid; grid-template-columns: 1fr 1fr; gap: 14px; }
    .ac__counted { display: flex; justify-content: space-between; gap: 8px; }
    .ac__buttons { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
    .ac__buttons .btn { min-height: 40px; }
    @media (max-width: 599.98px) { .ac__dates { grid-template-columns: 1fr; } }
  `],
})
export class AddCommunityComponent implements OnInit, AfterViewChecked {
  /** From the route's data: the company area or board mode. */
  @Input() set variant(v: 'company' | 'board' | undefined) { this._variant = v === 'board' ? 'board' : 'company'; }
  get variant(): 'company' | 'board' { return this._variant; }
  private _variant: 'company' | 'board' = 'company';

  private company = inject(CompanyService);
  private board = inject(BoardService);
  private auth = inject(AuthService);
  private nav = inject(BoardNavigationService);
  private router = inject(Router);
  private toasts = inject(ToastService);
  private host = inject<ElementRef<HTMLElement>>(ElementRef);

  readonly rateLimitedMessage = RATE_LIMITED_MESSAGE;

  form = {
    legalName: '', communityName: '', county: '', formationDate: '', managementStartDate: '', description: '',
    parentCommunityId: null as string | null,
  };

  readonly parents = signal<PickerOption[]>([]);
  readonly errors = signal<Partial<Record<Field, string>>>({});
  readonly banner = signal<string | null>(null);
  readonly rateLimited = signal(false);
  readonly busy = signal(false);
  private pendingFocus: string | null = null;

  readonly summary = computed(() => FIELD_ORDER
    .filter(f => !!this.errors()[f])
    .map(f => ({ field: f, label: FIELD_LABELS[f], message: this.errors()[f]! })));

  private readonly asCompanyAdministrator = computed(() =>
    this.variant === 'company' || this.auth.user()?.isCompanyAdministrator === true);

  readonly parentHelper = computed(() => this.asCompanyAdministrator()
    ? "Choose one if this community is a sub-association of a master association. Archived communities aren't listed. A parent doesn't give anyone access to this community."
    : "Choose one if this community is a sub-association of a master association. Only communities you manage are listed. A parent doesn't give anyone access to this community.");

  readonly noMatch = (q: string): string => this.asCompanyAdministrator()
    ? `No community matches “${q}”. Archived communities can't be parents.`
    : `None of your communities match “${q}”.`;

  async ngOnInit(): Promise<void> {
    try {
      this.parents.set(toPickerOptions(await loadParentOptions(this.asCompanyAdministrator(), this.company, this.board)));
    } catch {
      this.parents.set([]);
    }
  }

  ngAfterViewChecked(): void {
    if (!this.pendingFocus) return;
    const el = this.host.nativeElement.querySelector<HTMLElement>(`#${this.pendingFocus}`);
    if (el) {
      this.pendingFocus = null;
      setTimeout(() => el.focus());
    }
  }

  count(): string {
    return this.form.description.length.toLocaleString('en-US');
  }

  fieldId(field: Field): string {
    return FIELD_IDS[field] || 'parent';
  }

  invalid(field: Field): 'true' | null {
    return this.errors()[field] ? 'true' : null;
  }

  describedBy(field: Field, help: string | null): string | null {
    const ids = [help, this.errors()[field] ? `${FIELD_IDS[field]}-error` : null].filter(Boolean);
    return ids.length ? ids.join(' ') : null;
  }

  focusField(event: Event, field: Field): void {
    event.preventDefault();
    const target = field === 'parentCommunityId'
      ? this.host.nativeElement.querySelector<HTMLElement>('[role="combobox"]')
      : this.host.nativeElement.querySelector<HTMLElement>(`#${FIELD_IDS[field]}`);
    target?.focus();
  }

  async save(): Promise<void> {
    if (this.busy()) return;
    this.banner.set(null);
    this.rateLimited.set(false);
    const problems = this.validate();
    this.errors.set(problems);
    if (Object.keys(problems).length) {
      this.pendingFocus = 'ac-summary';
      return;
    }
    const body: CreateCommunityRequest = {
      legalName: this.form.legalName.trim(),
      communityName: this.form.communityName.trim(),
      county: this.form.county.trim(),
      formationDate: this.form.formationDate,
      managementStartDate: this.form.managementStartDate,
      description: this.form.description.trim() || null,
      parentCommunityId: this.form.parentCommunityId,
    };
    this.busy.set(true);
    try {
      const created = await this.company.createCommunity(body);
      const asManager = !created.settingsPath.startsWith('/app/company');
      if (asManager) {
        // FR-008: the caller now manages it. Refresh the session's memberships and act within it.
        try { await this.auth.refreshCurrentUser(); } catch { /* the next request refreshes it */ }
        this.nav.setActiveCommunity(created.id);
        this.toasts.show(`${created.communityName} was added. You're its Community Manager, with no end date.`);
      } else {
        this.toasts.show(`${created.communityName} was added as Onboarding. Residents can't see it until it's marked Active.`);
      }
      await this.router.navigate([created.settingsPath]);
    } catch (err) {
      this.onError(err);
    } finally {
      this.busy.set(false);
    }
  }

  cancel(): void {
    void this.router.navigate([this.variant === 'company' ? '/app/company/portfolio' : '/app/board/home']);
  }

  private validate(): Partial<Record<Field, string>> {
    const f = this.form;
    const e: Partial<Record<Field, string>> = {};
    const legal = f.legalName.trim();
    const name = f.communityName.trim();
    if (!legal) e.legalName = 'Enter the legal name.';
    else if (legal.length > 200) e.legalName = 'Legal name can be at most 200 characters.';
    if (!name) e.communityName = 'Enter a community name.';
    else if (name.length < 2 || name.length > 80) e.communityName = 'Community name must be 2 to 80 characters.';
    if (!f.county.trim()) e.county = 'Enter the county.';
    if (!f.formationDate) e.formationDate = 'Enter the formation date.';
    else if (!isIsoDate(f.formationDate)) e.formationDate = 'Enter a real date as MM/DD/YYYY.';
    else if (f.formationDate > new Date().toISOString().slice(0, 10)) e.formationDate = "Formation date can't be in the future.";
    if (!f.managementStartDate) e.managementStartDate = 'Enter the management start date.';
    else if (!isIsoDate(f.managementStartDate)) e.managementStartDate = 'Enter a real date as MM/DD/YYYY.';
    if (f.description.trim().length > 2000) e.description = 'Description can be at most 2,000 characters.';
    return e;
  }

  private onError(err: unknown): void {
    const code = refusalCodeOf(err);
    const audience = this.asCompanyAdministrator() ? 'companyAdmin' : 'staff';
    if (code === 'COMMUNITY_NAME_TAKEN') {
      this.errors.set({ communityName: refusalMessage(code, { audience, serverMessage: serverMessageOf(err) }) });
      this.pendingFocus = 'ac-summary';
    } else if (code === 'PARENT_ARCHIVED') {
      this.errors.set({ parentCommunityId: serverMessageOf(err) ?? refusalMessage(code) });
      this.pendingFocus = 'ac-summary';
    } else if (code === 'VALIDATION_ERROR' && this.applyFieldErrors(err)) {
      this.pendingFocus = 'ac-summary';
    } else if (code === 'FORBIDDEN') {
      this.showBanner(refusalMessage('FORBIDDEN'));
    } else if (code === 'RATE_LIMITED') {
      this.rateLimited.set(true);
    } else {
      this.showBanner(SAVE_FAILED_MESSAGE);
    }
  }

  private applyFieldErrors(err: unknown): boolean {
    const list = (err as { error?: { errors?: { field?: string; message?: string }[] } })?.error?.errors;
    const mapped: Partial<Record<Field, string>> = {};
    for (const e of Array.isArray(list) ? list : []) {
      const field = FIELD_ORDER.find(f => f.toLowerCase() === (e?.field ?? '').toLowerCase());
      if (field && e.message) mapped[field] = e.message;
    }
    if (!Object.keys(mapped).length) return false;
    this.errors.set(mapped);
    return true;
  }

  private showBanner(message: string): void {
    this.banner.set(message);
    this.pendingFocus = 'ac-banner';
  }
}

function isIsoDate(value: string): boolean {
  return /^\d{4}-\d{2}-\d{2}$/.test(value) && !Number.isNaN(new Date(`${value}T00:00:00Z`).getTime());
}
