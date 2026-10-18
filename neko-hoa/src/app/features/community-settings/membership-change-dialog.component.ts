import { Component, EventEmitter, Input, OnInit, Output, ViewChild, computed, inject, signal } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  CommunityInvitation, CommunityPerson, CommunitySettingsService, SettingsMembership,
} from '../../core/services/community-settings.service';
import { DialogComponent } from '../../shared/dialog.component';
import { ComboPickerComponent, PickerOption } from '../../shared/combo-picker.component';
import { formatPlainDate, formatTodayUtc } from '../../shared/date-format';
import {
  RATE_LIMITED_MESSAGE, RefusalAudience, SAVE_FAILED_MESSAGE, refusalCodeOf, refusalMessage, roleLabel, withArticle,
} from '../../shared/refusal-messages';
import { todayIso } from './appoint-dialog.component';

export type ChangeRequest =
  | { kind: 'end' | 'downgrade' | 'endDate' | 'replace'; membership: SettingsMembership }
  | { kind: 'revoke'; invitation: CommunityInvitation };

export interface ChangeDone {
  message: string;
  /** The signed-in person ended their own manager membership (brief 05 8.05.t). */
  selfEnded?: boolean;
  area: 'managers' | 'board-members-and-accountants' | 'invitations';
}

/**
 * 030 US2 / FR-019–FR-022 / brief 05 §5: the confirmation and form dialogs for a membership row
 * (End membership, Downgrade to Board Member, Change end date, Replace manager) and for revoking a
 * role invitation. Each says what happens and what doesn't change; a refusal after confirming stays
 * at the top of the open dialog, and when it can't be fixed there the primary becomes "Close".
 * The membership endpoints offer no preview, so the invitations a change revokes aren't listed.
 */
@Component({
  selector: 'app-membership-change-dialog',
  standalone: true,
  imports: [FormsModule, NgTemplateOutlet, DialogComponent, ComboPickerComponent],
  template: `
    <app-dialog [heading]="heading()" [subheading]="subheading()" [fallbackFocus]="fallbackFocus" (dismiss)="cancelled.emit()">
      @if (refusal()) {
        <div class="alert alert--error dlg-refusal" id="mc-refusal" role="alert" tabindex="-1">{{ refusal() }}</div>
      }
      @if (request.kind === 'endDate') {
        <form class="dlg-form" (ngSubmit)="confirm()" novalidate>
          <p>{{ currentEndLine() }}</p>
          <div>
            <label class="field-label" for="mc-end">End date (optional)</label>
            <input class="field" id="mc-end" name="endDate" type="date" [(ngModel)]="endDate"
                   [attr.aria-invalid]="fieldError() ? 'true' : null"
                   [attr.aria-describedby]="fieldError() ? 'mc-end-help mc-end-error' : 'mc-end-help'" />
            <p class="field-help" id="mc-end-help">{{ isManager() ? 'MM/DD/YYYY. Leave empty for no end date. A Community Manager can have an end date only while another Community Manager has none.' : 'MM/DD/YYYY. Leave empty for no end date.' }}</p>
            @if (fieldError()) { <p class="field-error" id="mc-end-error">{{ fieldError() }}</p> }
          </div>
          <ng-container *ngTemplateOutlet="actions" />
        </form>
      } @else {
        @if (request.kind === 'replace') {
          <div class="dlg-form">
            <app-combo-picker label="Replacement (required)" placeholder="Search by name" [noun]="['person', 'people']"
                              [options]="replacementOptions()" [value]="replacementId()" (valueChange)="chooseReplacement($event)"
                              (queryChange)="loadPeople($event)" [error]="fieldError()"
                              [helper]="replaceHelp()"
                              [noMatchMessage]="noMatch" />
          </div>
        }
        @if (happens().length) {
          <div class="dlg-section"><h3>What happens</h3><ul>@for (line of happens(); track line) { <li>{{ line }}</li> }</ul></div>
        }
        @if (unchanged().length) {
          <div class="dlg-section"><h3>What doesn't change</h3><ul>@for (line of unchanged(); track line) { <li>{{ line }}</li> }</ul></div>
        }
        <ng-container *ngTemplateOutlet="actions" />
      }
      <ng-template #actions>
        <div class="dlg-actions">
          @if (closed()) {
            <button type="button" class="btn btn--primary" (click)="closeAfterRefusal.emit()">Close</button>
          } @else {
            <button type="button" class="btn btn--primary" [disabled]="busy()" (click)="confirm()">{{ busy() ? 'Saving…' : primaryLabel() }}</button>
            <button type="button" class="btn btn--ghost" (click)="cancelled.emit()">Cancel</button>
          }
          @if (rateLimited()) { <p class="field-error" role="alert">{{ rateLimitedMessage }}</p> }
        </div>
      </ng-template>
    </app-dialog>
  `,
})
export class MembershipChangeDialogComponent implements OnInit {
  @Input({ required: true }) request!: ChangeRequest;
  @Input({ required: true }) communityId = '';
  @Input({ required: true }) communityName = '';
  @Input() archived = false;
  @Input() currentUserId: string | null = null;
  /** Every membership in the community, for "what doesn't change". */
  @Input() memberships: SettingsMembership[] = [];
  @Input() audience: RefusalAudience = 'staff';
  @Input() fallbackFocus: HTMLElement | null = null;
  @Output() readonly done = new EventEmitter<ChangeDone>();
  @Output() readonly cancelled = new EventEmitter<void>();
  @Output() readonly closeAfterRefusal = new EventEmitter<void>();

  @ViewChild(DialogComponent) private dialog?: DialogComponent;
  private settings = inject(CommunitySettingsService);

  readonly rateLimitedMessage = RATE_LIMITED_MESSAGE;
  readonly refusal = signal<string | null>(null);
  readonly closed = signal(false);
  readonly busy = signal(false);
  readonly rateLimited = signal(false);
  readonly fieldError = signal<string | null>(null);
  readonly people = signal<CommunityPerson[]>([]);
  readonly replacementId = signal<string | null>(null);
  private readonly replacement = signal<CommunityPerson | null>(null);
  endDate = '';

  readonly noMatch = (q: string) => `No one in ${this.communityName} matches “${q}”.`;

  ngOnInit(): void {
    if (this.request.kind === 'endDate') this.endDate = this.request.membership.endDate ?? '';
    if (this.request.kind === 'replace') void this.loadPeople('');
  }

  private get member(): SettingsMembership | null {
    return this.request.kind === 'revoke' ? null : this.request.membership;
  }

  isManager(): boolean {
    return this.member?.role === 'CommunityManager';
  }

  private isSelf(): boolean {
    return !!this.member && this.member.userId === this.currentUserId;
  }

  private selfManagerEnd(): boolean {
    return this.request.kind === 'end' && this.isSelf() && this.isManager();
  }

  /** "Jordan Blake (you)", for the open-ended manager who remains. */
  private otherOpenEndedManager(): string | null {
    const other = this.memberships.find(m => m.role === 'CommunityManager' && m.effective && m.openEnded && m.id !== this.member?.id);
    if (!other) return null;
    return other.userId === this.currentUserId ? `${other.userDisplayName} (you)` : other.userDisplayName;
  }

  private boardRowOf(userId: string): SettingsMembership | undefined {
    return this.memberships.find(m => m.userId === userId && m.role === 'BoardMember' && m.effective);
  }

  readonly replacementOptions = computed<PickerOption[]>(() => this.people()
    .filter(p => !p.rolesHere.includes('CommunityManager'))
    .map(p => ({ id: p.userId, label: p.name, detail: p.rolesHere.map(roleLabel).join(' · ') || null })));

  heading(): string {
    const r = this.request;
    if (r.kind === 'revoke') return `Revoke the invitation to ${r.invitation.email}?`;
    const name = r.membership.userDisplayName;
    const role = roleLabel(r.membership.role);
    switch (r.kind) {
      case 'end': return this.selfManagerEnd() ? `End your ${role} membership in ${this.communityName}?` : `End ${name}'s ${role} membership?`;
      case 'downgrade': return `Downgrade ${name} to Board Member?`;
      case 'endDate': return 'Change end date';
      case 'replace': return `Replace ${name} as Community Manager of ${this.communityName}?`;
    }
  }

  subheading(): string {
    const m = this.member;
    return this.request.kind === 'endDate' && m ? `${m.userDisplayName} · ${roleLabel(m.role)} · ${this.communityName}` : '';
  }

  replaceHelp(): string {
    return `Only people already in ${this.communityName} can be chosen. An invitation can't replace a manager until it's accepted. People who are already Community Managers aren't listed.`;
  }

  currentEndLine(): string {
    const end = this.member?.endDate;
    return end ? `Now: ends ${formatPlainDate(end)}` : 'Now: no end date';
  }

  primaryLabel(): string {
    switch (this.request.kind) {
      case 'end': return this.selfManagerEnd() ? 'End my membership' : 'End membership';
      case 'downgrade': return 'Downgrade to Board Member';
      case 'endDate': return 'Save end date';
      case 'replace': return 'Replace manager';
      case 'revoke': return 'Revoke invitation';
    }
  }

  happens(): string[] {
    const r = this.request;
    const today = formatTodayUtc();
    const c = this.communityName;
    if (r.kind === 'revoke') {
      const role = roleLabel(r.invitation.role);
      return ['The link in the invitation email stops working at once.', `${r.invitation.email} can't use it to become ${withArticle(role)} of ${c}.`];
    }
    const m = r.membership;
    const name = m.userDisplayName;
    const role = roleLabel(m.role);
    switch (r.kind) {
      case 'end': {
        const was = m.endDate ? ` It was set to end ${formatPlainDate(m.endDate)}.` : '';
        if (this.selfManagerEnd()) {
          const otherRole = this.memberships.some(x => x.userId === m.userId && x.id !== m.id && x.effective);
          return [
            `It ends today, ${today}.`,
            `You lose access to ${possessive(c)} Community settings at once.`,
            ...(otherRole ? [] : [`${c} leaves your community switcher and My Communities, because you have no other role there.`]),
          ];
        }
        const lines = [`It ends today, ${today}.${was}`];
        if (this.archived && this.isManager()) {
          lines.push(`${c} is archived, so it can be left with no Community Manager.`,
            `If ${c} is restored, it returns to Onboarding until a Community Manager with no end date is appointed.`);
        } else {
          lines.push(`${name} can't act as ${withArticle(role)} of ${c} from now on.`);
        }
        return lines;
      }
      case 'downgrade': {
        const lines = [`${name}'s Community Manager membership ends today, ${today}.`];
        if (!this.boardRowOf(m.userId)) lines.push(`${name} becomes a Board Member of ${c} today, with no end date.`);
        lines.push(`${name} can't act as a Community Manager of ${c} from now on.`);
        return lines;
      }
      case 'replace': {
        const repl = this.replacement();
        if (!repl) return [];
        return [
          `${repl.name} becomes a Community Manager of ${c} now, with no end date.`,
          `${name}'s Community Manager membership ends today, ${today}.`,
          "Both happen together. If one can't happen, neither does.",
        ];
      }
      default: return [];
    }
  }

  unchanged(): string[] {
    const r = this.request;
    if (r.kind === 'revoke') return [`You can send ${r.invitation.email} a new invitation later.`];
    const m = r.membership;
    const other = this.otherOpenEndedManager();
    const stillManaged = other && this.isManager() && !this.archived ? [`${this.communityName} still has a Community Manager with no end date: ${other}.`] : [];
    switch (r.kind) {
      case 'end':
        if (this.selfManagerEnd()) return [...stillManaged, "Your roles in your other communities don't change."];
        return [...stillManaged, 'Nothing is deleted. The ended membership stays in the list under Ended.'];
      case 'downgrade': {
        const board = this.boardRowOf(m.userId);
        const keeps = board
          ? [`${m.userDisplayName} keeps their Board Member membership.${board.endDate ? ` It ends ${formatPlainDate(board.endDate)}, as before.` : ''}`]
          : [];
        return [...keeps, ...stillManaged];
      }
      case 'replace': {
        const repl = this.replacement();
        if (!repl) return [];
        return [
          ...(repl.rolesHere.includes('BoardMember') ? [`${repl.name} keeps their Board Member membership.`] : []),
          `${m.userDisplayName}'s roles in other communities don't change.`,
        ];
      }
      default: return [];
    }
  }

  chooseReplacement(id: string | null): void {
    this.replacementId.set(id);
    this.replacement.set(this.people().find(p => p.userId === id) ?? null);
    this.fieldError.set(null);
  }

  async loadPeople(search: string): Promise<void> {
    try {
      this.people.set((await this.settings.getPeople(this.communityId, { search, limit: 25 })).items);
    } catch {
      this.people.set([]);
    }
  }

  async confirm(): Promise<void> {
    if (this.busy() || this.closed()) return;
    this.refusal.set(null);
    this.rateLimited.set(false);
    this.fieldError.set(null);
    const r = this.request;
    if (r.kind === 'endDate' && this.endDate && this.endDate <= todayIso()) {
      this.fieldError.set('Choose a date after today. To end a membership today, use End membership.');
      return;
    }
    if (r.kind === 'replace' && !this.replacement()) {
      this.fieldError.set('Choose a person.');
      return;
    }
    this.busy.set(true);
    try {
      this.done.emit(await this.apply(r));
    } catch (err) {
      this.onError(err);
    } finally {
      this.busy.set(false);
    }
  }

  private async apply(r: ChangeRequest): Promise<ChangeDone> {
    const c = this.communityName;
    if (r.kind === 'revoke') {
      await this.settings.revokeInvitation(this.communityId, r.invitation.id);
      return { area: 'invitations', message: `Invitation to ${r.invitation.email} revoked.` };
    }
    const m = r.membership;
    const area = m.role === 'CommunityManager' ? 'managers' : 'board-members-and-accountants';
    switch (r.kind) {
      case 'end':
        await this.settings.updateMembership(this.communityId, m.id, { end: true });
        return { area, message: `${m.userDisplayName}'s ${roleLabel(m.role)} membership ended.`, selfEnded: this.selfManagerEnd() };
      case 'downgrade':
        await this.settings.updateMembership(this.communityId, m.id, { downgradeTo: 'BoardMember' });
        return { area, message: `${m.userDisplayName} is now a Board Member only.` };
      case 'endDate': {
        const endDate = this.endDate || null;
        await this.settings.updateMembership(this.communityId, m.id, { endDate });
        return { area, message: endDate ? `${m.userDisplayName}'s end date is now ${formatPlainDate(endDate)}.` : `${m.userDisplayName} now has no end date.` };
      }
      case 'replace': {
        const repl = this.replacement()!;
        await this.settings.replaceManager(this.communityId, { currentManagerUserId: m.userId, replacementUserId: repl.userId });
        return { area, message: `${repl.name} replaced ${m.userDisplayName} as Community Manager of ${c}.` };
      }
    }
  }

  private onError(err: unknown): void {
    const code = refusalCodeOf(err);
    const m = this.member;
    if (code === 'RATE_LIMITED') {
      this.rateLimited.set(true);
      return;
    }
    if (code === 'VALIDATION_ERROR' && this.request.kind === 'replace' && m) {
      // Design choice (brief 05 §5): the replacement left the community; the picker stays.
      this.showRefusal(`The person you chose is no longer in ${this.communityName}, so they can't replace ${m.userDisplayName}. Choose someone else. ${m.userDisplayName} is still the Community Manager.`, false);
      return;
    }
    if (code === 'INVITATION_INVALID') {
      this.showRefusal(refusalMessage(code, { audience: 'staff' }), true);
      return;
    }
    if (code === 'LAST_MANAGER' || code === 'FORBIDDEN' || code === 'COMMUNITY_ARCHIVED' || code === 'NOT_FOUND') {
      const variant = this.request.kind === 'downgrade' ? 'downgrade' : this.request.kind === 'endDate' ? 'endDate' : undefined;
      this.showRefusal(refusalMessage(code, {
        personName: m?.userDisplayName, communityName: this.communityName, audience: this.audience, variant,
      }), true);
      return;
    }
    this.showRefusal(SAVE_FAILED_MESSAGE, false);
  }

  private showRefusal(message: string, final: boolean): void {
    this.refusal.set(message);
    this.closed.set(final);
    this.dialog?.focus('#mc-refusal');
  }
}

/** "Sakura Heights'" / "Keystone Park's". */
function possessive(name: string): string {
  return name.endsWith('s') ? `${name}'` : `${name}'s`;
}
