import { Component, EventEmitter, Input, OnInit, Output, ViewChild, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CommunityPerson, CommunitySettingsService } from '../../core/services/community-settings.service';
import { DialogComponent } from '../../shared/dialog.component';
import { ComboPickerComponent, PickerOption } from '../../shared/combo-picker.component';
import { formatPlainDate } from '../../shared/date-format';
import {
  RATE_LIMITED_MESSAGE, RefusalAudience, SAVE_FAILED_MESSAGE, refusalCodeOf, refusalMessage, roleLabel, serverMessageOf, withArticle,
} from '../../shared/refusal-messages';

export type AppointKind = 'manager' | 'board';

export interface AppointDone {
  /** The area whose heading takes focus: the members table, or Invitations for an email. */
  area: 'managers' | 'board-members-and-accountants' | 'invitations';
  message: string;
}

const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

/** Today as an ISO date in UTC: memberships start on the UTC date (FR-022). */
export function todayIso(): string {
  return new Date().toISOString().slice(0, 10);
}

/**
 * 030 US2 / FR-018–FR-021 / brief 05 §5–§6: appoint a Community Manager, or a Board Member or
 * Accountant, either directly from the community's people list (effective now, no invitation) or by
 * email (an invitation valid 72 hours). The email path shows only the email: nothing looks up who
 * owns it (FR-018).
 */
@Component({
  selector: 'app-appoint-dialog',
  standalone: true,
  imports: [FormsModule, DialogComponent, ComboPickerComponent],
  template: `
    <app-dialog [heading]="kind === 'manager' ? 'Appoint a Community Manager' : 'Appoint a board member or accountant'"
                [subheading]="communityName" [fallbackFocus]="fallbackFocus" (dismiss)="cancelled.emit()">
      @if (topError()) {
        <div class="alert alert--error dlg-refusal" id="ap-top" role="alert" tabindex="-1">{{ topError() }}</div>
      }
      <form class="dlg-form" (ngSubmit)="submit()" novalidate>
        @if (kind === 'board') {
          <fieldset class="dlg-fieldset">
            <legend>Role (required)</legend>
            <label class="dlg-radio"><input type="radio" name="role" [checked]="role() === 'BoardMember'" (change)="role.set('BoardMember')" /> Board Member</label>
            <label class="dlg-radio"><input type="radio" name="role" [checked]="role() === 'Accountant'" (change)="role.set('Accountant')" /> Accountant</label>
          </fieldset>
        }
        <fieldset class="dlg-fieldset">
          <legend>How do you want to appoint them?</legend>
          <label class="dlg-radio">
            <input type="radio" name="path" [checked]="path() === 'direct'" [disabled]="noPeople()" (change)="setPath('direct')" aria-describedby="ap-direct-help" />
            <span>Choose someone already in {{ communityName }}</span>
          </label>
          <p class="field-help" id="ap-direct-help">{{ noPeople()
            ? 'No one is in ' + communityName + ' yet, so you can only invite by email.'
            : 'For people who already have a role or a home in ' + communityName + '. It takes effect now. No invitation is sent.' }}</p>
          <label class="dlg-radio">
            <input type="radio" name="path" [checked]="path() === 'email'" (change)="setPath('email')" aria-describedby="ap-email-path-help" />
            <span>Invite someone by email</span>
          </label>
          <p class="field-help" id="ap-email-path-help">For anyone else. We email an invitation that's valid for 72 hours. {{ kind === 'manager' ? 'They become a Community Manager only if they accept.' : 'They get the role only if they accept.' }}</p>
        </fieldset>

        @if (path() === 'direct') {
          <app-combo-picker label="Person (required)" placeholder="Search by name" [noun]="['person', 'people']"
                            [options]="personOptions()" [value]="personId()" (valueChange)="choosePerson($event)" (queryChange)="search($event)"
                            [helper]="personNote()"
                            [error]="errors()['person'] ?? null" [noMatchMessage]="noPersonMatch" />
        } @else {
          <div>
            <label class="field-label" for="ap-email">Email address (required)</label>
            <input class="field" id="ap-email" name="email" type="email" inputmode="email" autocomplete="off" [(ngModel)]="email"
                   [attr.aria-invalid]="errors()['email'] ? 'true' : null"
                   [attr.aria-describedby]="errors()['email'] ? 'ap-email-help ap-email-error' : 'ap-email-help'" />
            <p class="field-help" id="ap-email-help">Until they accept, the invitation shows only this email address.</p>
            @if (errors()['email']; as m) {
              <p class="field-error" id="ap-email-error" role="alert">{{ m }}</p>
              @if (pendingInvitationId()) {
                <button type="button" class="btn" (click)="resendPending()">Resend invitation</button>
              }
            }
          </div>
        }

        @if (endDateAllowed()) {
          <div>
            <label class="field-label" for="ap-end">{{ kind === 'manager' ? 'End date (optional)' : 'Term end date (optional)' }}</label>
            <input class="field" id="ap-end" name="endDate" type="date" [(ngModel)]="endDate"
                   [attr.aria-invalid]="errors()['endDate'] ? 'true' : null"
                   [attr.aria-describedby]="errors()['endDate'] ? 'ap-end-help ap-end-error' : 'ap-end-help'" />
            <p class="field-help" id="ap-end-help">{{ endDateHelp() }}</p>
            @if (errors()['endDate']; as m) { <p class="field-error" id="ap-end-error">{{ m }}</p> }
          </div>
        } @else {
          <p class="muted">No end date. {{ communityName }} has no other Community Manager without an end date, so this one can't have one.</p>
        }
        @if (communityStatus === 'Onboarding') {
          <p class="field-help">{{ communityName }} is Onboarding. Role invitations are still emailed now. Only resident invitations wait until it's marked Active.</p>
        }
        @if (summary()) { <p class="ap__summary">{{ summary() }}</p> }

        <div class="dlg-actions">
          <button type="submit" class="btn btn--primary" [disabled]="busy()">{{ primaryLabel() }}</button>
          <button type="button" class="btn btn--ghost" (click)="cancelled.emit()">Cancel</button>
          @if (rateLimited()) { <p class="field-error" role="alert">{{ rateLimitedMessage }}</p> }
        </div>
      </form>
    </app-dialog>
  `,
  styles: [`.ap__summary { font-weight: 500; }`],
})
export class AppointDialogComponent implements OnInit {
  @Input({ required: true }) kind: AppointKind = 'manager';
  @Input({ required: true }) communityId = '';
  @Input({ required: true }) communityName = '';
  @Input() communityStatus = 'Active';
  /** FR-020: a new manager may have an end date only while another open-ended manager exists. */
  @Input() hasOpenEndedManager = true;
  @Input() audience: RefusalAudience = 'staff';
  @Input() fallbackFocus: HTMLElement | null = null;
  @Output() readonly done = new EventEmitter<AppointDone>();
  @Output() readonly cancelled = new EventEmitter<void>();

  @ViewChild(DialogComponent) private dialog?: DialogComponent;

  private settings = inject(CommunitySettingsService);

  readonly rateLimitedMessage = RATE_LIMITED_MESSAGE;
  readonly role = signal<'CommunityManager' | 'BoardMember' | 'Accountant'>('BoardMember');
  readonly path = signal<'direct' | 'email'>('direct');
  readonly people = signal<CommunityPerson[]>([]);
  readonly noPeople = signal(false);
  readonly personId = signal<string | null>(null);
  readonly errors = signal<Partial<Record<string, string>>>({});
  readonly topError = signal<string | null>(null);
  readonly pendingInvitationId = signal<string | null>(null);
  readonly rateLimited = signal(false);
  readonly busy = signal(false);
  email = '';
  endDate = '';
  private searchSeq = 0;

  readonly personOptions = computed<PickerOption[]>(() => this.people()
    .filter(p => !p.rolesHere.includes(this.role()))
    .map(p => ({
      id: p.userId, label: p.name,
      detail: p.rolesHere.length ? p.rolesHere.map(roleLabel).join(' · ') : (p.residentOf.length ? `Resident of ${p.residentOf.join(', ')}` : null),
    })));

  readonly chosen = signal<CommunityPerson | null>(null);

  readonly personNote = computed(() => this.kind === 'manager'
    ? "You, and people who are already Community Managers, aren't listed."
    : "You, and people who already hold this role, aren't listed.");

  readonly endDateAllowed = computed(() => this.kind !== 'manager' || this.hasOpenEndedManager);

  readonly noPersonMatch = (q: string) => `No one in ${this.communityName} matches “${q}”.`;

  ngOnInit(): void {
    if (this.kind === 'manager') this.role.set('CommunityManager');
    void this.loadPeople('', true);
  }

  endDateHelp(): string {
    if (this.kind === 'manager') {
      return 'MM/DD/YYYY. Leave empty for no end date. A Community Manager can have an end date only while another Community Manager has none.';
    }
    return this.path() === 'direct'
      ? 'MM/DD/YYYY. Leave empty for no end date. The membership starts today.'
      : 'MM/DD/YYYY. Leave empty for no end date. The membership starts when they accept.';
  }

  summary(): string | null {
    const role = roleLabel(this.role());
    const until = this.endDate ? `until ${formatPlainDate(this.endDate)}` : 'with no end date';
    if (this.path() === 'direct') {
      const person = this.chosen();
      if (!person) return null;
      return `${person.name} becomes ${withArticle(role)} of ${this.communityName} now, ${until}. No invitation is sent.`;
    }
    const email = this.email.trim();
    if (!EMAIL.test(email)) return null;
    const term = this.kind === 'board' && this.endDate ? `, with a term ending ${formatPlainDate(this.endDate)}` : '';
    return `We'll email ${email} an invitation to be ${withArticle(role)} of ${this.communityName}${term}. It's valid for 72 hours.`;
  }

  primaryLabel(): string {
    if (this.path() === 'email') return this.busy() ? 'Sending…' : 'Send invitation';
    if (this.busy()) return 'Saving…';
    const person = this.chosen();
    return person ? `Appoint ${person.name}` : 'Appoint';
  }

  setPath(path: 'direct' | 'email'): void {
    this.path.set(path);
    this.errors.set({});
    this.topError.set(null);
  }

  choosePerson(id: string | null): void {
    this.personId.set(id);
    this.chosen.set(this.people().find(p => p.userId === id) ?? null);
    this.errors.update(e => ({ ...e, person: '' }));
  }

  search(query: string): void {
    void this.loadPeople(query, false);
  }

  async submit(): Promise<void> {
    if (this.busy()) return;
    this.topError.set(null);
    this.rateLimited.set(false);
    this.pendingInvitationId.set(null);
    const problems: Record<string, string> = {};
    const email = this.email.trim();
    if (this.path() === 'direct' && !this.personId()) problems['person'] = 'Choose a person.';
    if (this.path() === 'email' && !EMAIL.test(email)) problems['email'] = 'Enter a valid email address.';
    if (this.endDateAllowed() && this.endDate && this.endDate <= todayIso()) {
      problems['endDate'] = 'Choose a date after today. To end a membership today, use End membership.';
    }
    this.errors.set(problems);
    if (Object.keys(problems).length) return;

    const role = this.role();
    const endDate = this.endDateAllowed() && this.endDate ? this.endDate : null;
    this.busy.set(true);
    try {
      if (this.path() === 'direct') {
        const person = this.chosen()!;
        await this.settings.appoint(this.communityId, { role, userId: person.userId, startDate: todayIso(), endDate });
        this.done.emit({
          area: role === 'CommunityManager' ? 'managers' : 'board-members-and-accountants',
          message: `${person.name} is now ${withArticle(roleLabel(role))} of ${this.communityName}.`,
        });
      } else {
        await this.settings.appoint(this.communityId, { role, email, endDate });
        // FR-018: the same whether or not the email has an account.
        this.done.emit({ area: 'invitations', message: `Invitation sent to ${email}.` });
      }
    } catch (err) {
      this.onError(err, email);
    } finally {
      this.busy.set(false);
    }
  }

  async resendPending(): Promise<void> {
    const id = this.pendingInvitationId();
    if (!id) return;
    try {
      await this.settings.resendInvitation(this.communityId, id);
      this.done.emit({ area: 'invitations', message: `Invitation sent again to ${this.email.trim()}. The earlier link no longer works.` });
    } catch (err) {
      const code = refusalCodeOf(err);
      if (code === 'RATE_LIMITED') this.rateLimited.set(true);
      else this.showTop(refusalMessage(code, { audience: this.audience, communityName: this.communityName, variant: 'resend' }));
    }
  }

  private onError(err: unknown, email: string): void {
    const code = refusalCodeOf(err);
    const ctx = { email, role: this.role(), communityName: this.communityName, audience: this.audience };
    const field = this.path() === 'email' ? 'email' : 'person';
    switch (code) {
      case 'SELF_GRANT_NOT_ALLOWED':
      case 'ALREADY_MEMBER':
        this.errors.set({ [field]: refusalMessage(code, ctx) });
        return;
      case 'INVITATION_PENDING': {
        const id = (err as { error?: { invitationId?: string } })?.error?.invitationId ?? null;
        this.pendingInvitationId.set(id);
        this.errors.set({ email: refusalMessage(code, ctx) });
        return;
      }
      case 'VALIDATION_ERROR': {
        const list = (err as { error?: { errors?: { field?: string; message?: string }[] } })?.error?.errors ?? [];
        const endDate = list.find(e => (e.field ?? '').toLowerCase() === 'enddate');
        if (endDate?.message) this.errors.set({ endDate: endDate.message });
        else this.errors.set({ [field]: serverMessageOf(err) ?? refusalMessage(code) });
        return;
      }
      case 'RATE_LIMITED':
        this.rateLimited.set(true);
        return;
      case 'LAST_MANAGER':
        this.showTop(refusalMessage(code, { ...ctx, variant: 'endDate', personName: 'This Community Manager' }));
        return;
      case 'FORBIDDEN':
      case 'COMMUNITY_ARCHIVED':
        this.showTop(refusalMessage(code, ctx));
        return;
      default:
        this.showTop(SAVE_FAILED_MESSAGE);
    }
  }

  private showTop(message: string): void {
    this.topError.set(message);
    this.dialog?.focus('#ap-top');
  }

  private async loadPeople(search: string, first: boolean): Promise<void> {
    const seq = ++this.searchSeq;
    try {
      const page = await this.settings.getPeople(this.communityId, { search, limit: 25 });
      if (seq !== this.searchSeq) return;
      this.people.set(page.items);
      if (first && page.total === 0) {
        this.noPeople.set(true);
        this.path.set('email');
      }
    } catch {
      if (first) {
        this.noPeople.set(true);
        this.path.set('email');
      }
    }
  }
}
