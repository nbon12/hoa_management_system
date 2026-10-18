import { Component, ElementRef, OnInit, computed, inject, signal } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { Router } from '@angular/router';
import {
  CommunityInvitation, CommunitySettingsService, SettingsMembership,
} from '../../core/services/community-settings.service';
import { AuthService } from '../../core/services/auth.service';
import { BoardNavigationService } from '../../core/services/board-navigation.service';
import { PageNoticeService } from '../../core/services/page-notice.service';
import { ToastService } from '../../core/services/toast.service';
import { landingTargetFor } from '../../core/services/landing';
import { ActionsMenuComponent, MenuAction } from '../../shared/actions-menu.component';
import { DisabledReasonDirective } from '../../shared/disabled-reason.directive';
import { formatPlainDate } from '../../shared/date-format';
import {
  invitationStatusDetail, invitationStatusLabel, invitationTime, isOpenInvitation,
} from '../../shared/invitation-display';
import { RATE_LIMITED_MESSAGE, refusalCodeOf, refusalMessage, roleLabel } from '../../shared/refusal-messages';
import { CommunitySettingsContext } from './community-settings-context';
import { AppointDialogComponent, AppointDone, AppointKind } from './appoint-dialog.component';
import { ChangeDone, ChangeRequest, MembershipChangeDialogComponent } from './membership-change-dialog.component';

type Area = 'managers' | 'board-members-and-accountants' | 'invitations';

/**
 * 030 US2, US5 / FR-018–FR-022b / brief 05: Community settings › Managers and memberships.
 * Managers, Board members and accountants, role Invitations, and (US8) Residents, stacked, with jump
 * links. Appointments, ends, downgrades, end dates, replacement, resend and revoke all go through
 * the server, which decides; `can.*` and each row's `can*` only hide or explain controls. In an
 * Archived community only "End membership" remains (FR-012).
 */
@Component({
  selector: 'app-managers-memberships-section',
  standalone: true,
  imports: [NgTemplateOutlet, ActionsMenuComponent, DisabledReasonDirective, AppointDialogComponent, MembershipChangeDialogComponent],
  template: `
    <section class="mm" aria-labelledby="mm-title">
      <h2 class="section-title mm__title" id="mm-title" tabindex="-1" data-section-heading>Managers and memberships</h2>
      <p class="muted">Who runs {{ name() }}, who holds a role, and the invitations waiting for an answer. Residents are at the end of this section.</p>
      <nav class="mm__jump" aria-label="Jump to">
        <a class="link" href="#mm-managers" (click)="jump($event, 'managers')">Managers</a> ·
        <a class="link" href="#mm-board" (click)="jump($event, 'board-members-and-accountants')">Board members and accountants</a> ·
        <a class="link" href="#mm-invitations" (click)="jump($event, 'invitations')">Invitations</a> ·
        <a class="link" href="#mm-residents" (click)="jump($event, 'residents')">Residents</a>
      </nav>

      <!-- Managers -->
      <div class="card mm__card">
        <div class="mm__head">
          <h3 class="section-title" id="mm-managers" tabindex="-1" data-setting-id="managers">Managers</h3>
          @if (canAppoint('manager')) {
            <button type="button" class="btn" [attr.aria-label]="'Appoint a Community Manager for ' + name()" (click)="appoint.set('manager')">Appoint a manager</button>
          }
        </div>
        <p class="field-help" id="mm-rule">{{ ruleHelp() }}</p>
        @if (membershipsError()) {
          <div class="mm__state" role="alert"><p>Couldn't load managers.</p><button type="button" class="btn" (click)="loadMemberships()">Try again</button></div>
        } @else if (memberships()) {
          @if (managers().active.length === 0 && managers().ended.length === 0) {
            <div class="mm__state">
              <p>{{ name() }} has no Community Manager yet.</p>
              <p class="muted">{{ name() }} can't be marked Active until it has a Community Manager with no end date.</p>
              @for (inv of pendingManagerInvitations(); track inv.id) {
                <p class="muted">Invitation pending: {{ inv.email }}. It doesn't count until it's accepted.</p>
              }
            </div>
          } @else {
            <table class="data-table" aria-describedby="mm-rule">
              <caption class="sr-only">Community Managers of {{ name() }}</caption>
              <thead><tr><th scope="col">Name</th><th scope="col">Term</th><th scope="col">Actions</th></tr></thead>
              <tbody>
                @for (m of managers().active; track m.id) {
                  <tr>
                    <th scope="row" class="mm__name">{{ displayName(m) }}</th>
                    <td>
                      @if (m.openEnded) { <span class="pill">Open-ended</span><br /><span class="muted">No end date</span> }
                      @else { {{ endsLine(m) }}<br /><span class="muted">Doesn't count toward the rule above</span> }
                    </td>
                    <td><ng-container *ngTemplateOutlet="rowActions; context: { $implicit: m }" /></td>
                  </tr>
                }
                @if (managers().ended.length) {
                  <tr><th colspan="3" scope="rowgroup" class="mm__ended" aria-label="Ended Community Manager memberships">Ended</th></tr>
                  @for (m of managers().ended; track m.id) {
                    <tr><th scope="row" class="mm__name">{{ displayName(m) }}</th><td>{{ endedLine(m) }}</td><td><ng-container *ngTemplateOutlet="noActions" /></td></tr>
                  }
                }
              </tbody>
            </table>
          }
        }
      </div>

      <!-- Board members and accountants -->
      <div class="card mm__card">
        <div class="mm__head">
          <h3 class="section-title" id="mm-board" tabindex="-1" data-setting-id="board-members-and-accountants">Board members and accountants</h3>
          @if (canAppoint('board')) {
            <button type="button" class="btn" [attr.aria-label]="'Appoint a board member or accountant for ' + name()" (click)="appoint.set('board')">Appoint a board member or accountant</button>
          }
        </div>
        @if (membershipsError()) {
          <div class="mm__state" role="alert"><p>Couldn't load board members and accountants.</p><button type="button" class="btn" (click)="loadMemberships()">Try again</button></div>
        } @else if (memberships()) {
          @if (board().active.length === 0 && board().ended.length === 0) {
            <p class="mm__state">No board members or accountants yet.</p>
          } @else {
            <table class="data-table">
              <caption class="sr-only">Board members and accountants of {{ name() }}</caption>
              <thead><tr><th scope="col">Name</th><th scope="col">Role</th><th scope="col">Term</th><th scope="col">Actions</th></tr></thead>
              <tbody>
                @for (m of board().active; track m.id) {
                  <tr>
                    <th scope="row" class="mm__name">{{ displayName(m) }}</th>
                    <td>{{ roleName(m.role) }}</td>
                    <td>{{ m.endDate ? endsLine(m) : 'No end date' }}</td>
                    <td><ng-container *ngTemplateOutlet="rowActions; context: { $implicit: m }" /></td>
                  </tr>
                }
                @if (board().ended.length) {
                  <tr><th colspan="4" scope="rowgroup" class="mm__ended" aria-label="Ended board and accountant memberships">Ended</th></tr>
                  @for (m of board().ended; track m.id) {
                    <tr><th scope="row" class="mm__name">{{ displayName(m) }}</th><td>{{ roleName(m.role) }}</td><td>{{ endedLine(m) }}</td><td><ng-container *ngTemplateOutlet="noActions" /></td></tr>
                  }
                }
              </tbody>
            </table>
          }
        }
      </div>

      <!-- Invitations -->
      <div class="card mm__card">
        <h3 class="section-title" id="mm-invitations" tabindex="-1" data-setting-id="invitations">Invitations</h3>
        <p class="field-help">Invitations to be a Community Manager, Board Member or Accountant. Each link works for 72 hours. Times are in {{ possessiveName() }} time zone. Resident invitations are under Residents.</p>
        @if (invitationsError()) {
          <div class="mm__state" role="alert"><p>Couldn't load invitations.</p><button type="button" class="btn" (click)="loadInvitations()">Try again</button></div>
        } @else if (invitations()) {
          @if (invitationRows().length === 0) {
            <p class="mm__state">No invitations.</p>
          } @else {
            <div class="mm__scroll">
              <table class="data-table">
                <caption class="sr-only">Role invitations for {{ name() }}</caption>
                <thead><tr><th scope="col">Email</th><th scope="col">Role</th><th scope="col">Sent by</th><th scope="col">Sent</th><th scope="col">Expires</th><th scope="col">Status</th><th scope="col">Actions</th></tr></thead>
                <tbody>
                  @for (inv of invitationRows(); track inv.id) {
                    <tr>
                      <th scope="row" class="mm__name">{{ inv.email }}</th>
                      <td>{{ roleName(inv.role) }}@if (inv.termEndDate) {<br /><span class="muted">Term ends {{ plainDate(inv.termEndDate) }}</span>}</td>
                      <td>{{ inv.sentBy || '—' }}</td>
                      <td>{{ time(inv.sentAt) }}</td>
                      <td>{{ inv.expiresAt ? time(inv.expiresAt) : '—' }}</td>
                      <td><span class="pill">{{ statusLabel(inv) }}</span>@if (statusDetail(inv); as d) {<br /><span class="muted">{{ d }}</span>}</td>
                      <td>
                        @if (!readOnly() && isOpen(inv) && (inv.canResend || inv.canRevoke)) {
                          <div class="mm__inv-actions">
                            @if (inv.canResend || inv.resendBlockedReason) {
                              <button type="button" class="btn" [attr.aria-label]="'Resend invitation to ' + inv.email"
                                      [appDisabledReason]="resendReason(inv)" (click)="resend(inv)">Resend</button>
                            }
                            @if (inv.canRevoke) {
                              <button type="button" class="btn btn--ghost" [attr.aria-label]="'Revoke invitation to ' + inv.email"
                                      (click)="change.set({ kind: 'revoke', invitation: inv })">Revoke</button>
                            }
                          </div>
                          @if (rateLimitedFor() === inv.id) { <p class="field-error" role="alert">{{ rateLimitedMessage }}</p> }
                        } @else {
                          <ng-container *ngTemplateOutlet="noActions" />
                        }
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        }
      </div>

      <!-- Residents (brief 06; US8) -->
      <div class="card mm__card">
        <h3 class="section-title" id="mm-residents" tabindex="-1" data-setting-id="residents">Residents</h3>
        <ng-content select="[residents]" />
      </div>
    </section>

    <ng-template #noActions><span aria-hidden="true">—</span><span class="sr-only">No actions</span></ng-template>

    <ng-template #rowActions let-m>
      @if (readOnly()) {
        @if (ctx.settings()?.can?.endMembershipsWhileArchived) {
          <button type="button" class="btn" [attr.aria-label]="endLabel(m)"
                  (click)="change.set({ kind: 'end', membership: m })">End membership</button>
        } @else { <ng-container *ngTemplateOutlet="noActions" /> }
      } @else if (actionsFor(m).length) {
        <app-actions-menu [actions]="actionsFor(m)" [accessibleName]="'Actions for ' + m.userDisplayName" (chosen)="onAction(m, $event)" />
      } @else {
        <ng-container *ngTemplateOutlet="noActions" />
      }
    </ng-template>

    @if (appoint(); as kind) {
      <app-appoint-dialog [kind]="kind" [communityId]="communityId()" [communityName]="name()" [communityStatus]="status()"
                          [hasOpenEndedManager]="hasOpenEndedManager()" [audience]="ctx.audience()" [fallbackFocus]="areaHeading(kind === 'manager' ? 'managers' : 'board-members-and-accountants')"
                          (done)="onAppointed($event)" (cancelled)="appoint.set(null)" />
    }
    @if (change(); as request) {
      <app-membership-change-dialog [request]="request" [communityId]="communityId()" [communityName]="name()" [archived]="readOnly()"
                                    [currentUserId]="currentUserId()" [memberships]="memberships() ?? []" [audience]="ctx.audience()"
                                    [fallbackFocus]="areaHeading(request.kind === 'revoke' ? 'invitations' : 'managers')"
                                    (done)="onChanged($event)" (cancelled)="change.set(null)" (closeAfterRefusal)="closeAfterRefusal()" />
    }
  `,
  styles: [`
    .mm { display: flex; flex-direction: column; gap: 12px; }
    .mm__jump { font-size: 12px; }
    .mm__card { padding: 0; overflow: hidden; }
    .mm__card > .section-title, .mm__card > .field-help, .mm__head, .mm__state { padding: 0 14px; }
    .mm__card > .section-title, .mm__head { padding-top: 12px; }
    .mm__card > .field-help { margin-bottom: 8px; }
    .mm__head { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; justify-content: space-between; }
    .mm__state { padding-bottom: 14px; display: flex; flex-direction: column; gap: 6px; align-items: flex-start; }
    .mm__name { text-align: left; font-weight: 600; background: transparent; text-transform: none; letter-spacing: 0; font-size: 12.5px; color: var(--ink); }
    .mm__ended { text-align: left; background: var(--lav); }
    .mm__scroll { overflow-x: auto; }
    .mm__inv-actions { display: flex; gap: 6px; flex-wrap: wrap; }
  `],
})
export class ManagersMembershipsSectionComponent implements OnInit {
  readonly ctx = inject(CommunitySettingsContext);
  private settingsService = inject(CommunitySettingsService);
  private auth = inject(AuthService);
  private nav = inject(BoardNavigationService);
  private notices = inject(PageNoticeService);
  private toasts = inject(ToastService);
  private router = inject(Router);
  private host = inject<ElementRef<HTMLElement>>(ElementRef);

  readonly rateLimitedMessage = RATE_LIMITED_MESSAGE;
  readonly memberships = signal<SettingsMembership[] | null>(null);
  readonly membershipsError = signal(false);
  readonly invitations = signal<CommunityInvitation[] | null>(null);
  readonly invitationsError = signal(false);
  private readonly timeZoneId = signal<string | null>(null);
  readonly appoint = signal<AppointKind | null>(null);
  readonly change = signal<ChangeRequest | null>(null);
  readonly rateLimitedFor = signal<string | null>(null);

  readonly communityId = computed(() => this.ctx.settings()?.id ?? '');
  readonly name = computed(() => this.ctx.settings()?.communityName ?? '');
  readonly status = computed(() => this.ctx.settings()?.status ?? 'Active');
  readonly readOnly = this.ctx.readOnly;
  readonly currentUserId = computed(() => this.auth.user()?.id ?? null);

  readonly managers = computed(() => this.split(m => m.role === 'CommunityManager', managerOrder));
  readonly board = computed(() => this.split(m => m.role === 'BoardMember' || m.role === 'Accountant', boardOrder));
  readonly invitationRows = computed(() => this.invitations() ?? []);
  readonly hasOpenEndedManager = computed(() => this.managers().active.some(m => m.openEnded));
  readonly pendingManagerInvitations = computed(() =>
    (this.invitations() ?? []).filter(i => i.role === 'CommunityManager' && (i.effectiveState === 'Pending' || i.effectiveState === 'Scheduled')));

  ngOnInit(): void {
    void this.loadMemberships();
    void this.loadInvitations();
  }

  async loadMemberships(): Promise<void> {
    if (!this.communityId()) return;
    try {
      const page = await this.settingsService.getMemberships(this.communityId(), 100, 0);
      this.memberships.set(page.items.filter(m => m.role !== 'Resident'));
      this.membershipsError.set(false);
    } catch {
      this.membershipsError.set(true);
    }
  }

  async loadInvitations(): Promise<void> {
    if (!this.communityId()) return;
    try {
      const page = await this.settingsService.getInvitations(this.communityId(), { kind: 'membership', limit: 100 });
      this.timeZoneId.set(page.timeZoneId ?? null);
      this.invitations.set(sortInvitations(page.items.filter(i => i.effectiveState !== 'Accepted' && i.state !== 'Accepted')));
      this.invitationsError.set(false);
    } catch {
      this.invitationsError.set(true);
    }
  }

  ruleHelp(): string {
    return this.readOnly()
      ? `The rule that a community needs a Community Manager with no end date doesn't apply while it's archived. These memberships give no access while ${this.name()} is archived. You can end them. Nothing else here can change.`
      : "A community always needs at least one Community Manager with no end date. Managers with an end date don't count toward this.";
  }

  endLabel(m: SettingsMembership): string {
    return `End ${m.userDisplayName}'s ${roleLabel(m.role)} membership`;
  }

  canAppoint(kind: AppointKind): boolean {
    const can = this.ctx.settings()?.can;
    if (!can || this.readOnly()) return false;
    return kind === 'manager' ? can.manageManagers : can.manageMemberships;
  }

  displayName(m: SettingsMembership): string {
    return m.userId === this.currentUserId() ? `${m.userDisplayName} (you)` : m.userDisplayName;
  }

  roleName(role: string | null): string {
    return roleLabel(role);
  }

  endsLine(m: SettingsMembership): string {
    return m.endDate ? `Ends ${formatPlainDate(m.endDate)}` : 'No end date';
  }

  endedLine(m: SettingsMembership): string {
    return m.endDate ? `Ended ${formatPlainDate(m.endDate)}` : 'Ended';
  }

  plainDate(iso: string): string {
    return formatPlainDate(iso);
  }

  time(iso: string | null): string {
    return invitationTime(iso, this.timeZoneId());
  }

  possessiveName(): string {
    const n = this.name();
    return n.endsWith('s') ? `${n}'` : `${n}'s`;
  }

  statusLabel(inv: CommunityInvitation): string {
    return invitationStatusLabel(inv);
  }

  statusDetail(inv: CommunityInvitation): string | null {
    // The Expires column already gives the time; the status line only says why a row was revoked.
    return invitationStatusLabel(inv).startsWith('Revoked') || inv.effectiveState === 'Scheduled'
      ? invitationStatusDetail(inv, this.timeZoneId(), this.name()) : null;
  }

  isOpen(inv: CommunityInvitation): boolean {
    return isOpenInvitation(inv);
  }

  resendReason(inv: CommunityInvitation): string | null {
    if (inv.canResend) return null;
    return inv.resendBlockedReason
      ? refusalMessage(inv.resendBlockedReason, { variant: 'resend', communityName: this.name(), audience: this.ctx.audience() })
      : null;
  }

  /** The menu for an active row: only the items this viewer may use (brief 05 §4.1). */
  actionsFor(m: SettingsMembership): MenuAction[] {
    const can = this.ctx.settings()?.can;
    if (!can) return [];
    const isManager = m.role === 'CommunityManager';
    if (isManager ? !can.manageManagers : !can.manageMemberships) return [];
    const reason = (allowed: boolean, variant?: string) => allowed ? null
      : refusalMessage(m.reasonCode ?? 'LAST_MANAGER', { personName: m.userDisplayName, communityName: this.name(), audience: this.ctx.audience(), variant });
    const actions: MenuAction[] = [];
    if (m.canEditEndDate || m.reasonCode) actions.push({ key: 'endDate', label: 'Change end date', reason: reason(m.canEditEndDate, 'endDate') });
    if (isManager && (m.canDowngrade || m.reasonCode) && this.mayDowngrade(m)) {
      actions.push({ key: 'downgrade', label: 'Downgrade to Board Member', reason: reason(m.canDowngrade, 'downgrade') });
    }
    if (m.canEnd || m.reasonCode) actions.push({ key: 'end', label: 'End membership', reason: reason(m.canEnd) });
    if (isManager && can.replaceLastManager && this.ctx.mode() === 'company' && m.openEnded && m.reasonCode === 'LAST_MANAGER') {
      actions.push({ key: 'replace', label: 'Replace manager' });
    }
    return actions;
  }

  onAction(m: SettingsMembership, key: string): void {
    this.change.set({ kind: key as 'end' | 'downgrade' | 'endDate' | 'replace', membership: m });
  }

  async resend(inv: CommunityInvitation): Promise<void> {
    this.rateLimitedFor.set(null);
    try {
      await this.settingsService.resendInvitation(this.communityId(), inv.id);
      this.toasts.show(`Invitation sent again to ${inv.email}. The earlier link no longer works.`);
      await this.loadInvitations();
    } catch (err) {
      const code = refusalCodeOf(err);
      if (code === 'RATE_LIMITED') {
        this.rateLimitedFor.set(inv.id);
        return;
      }
      this.toasts.show(refusalMessage(code, { audience: 'staff', variant: 'resend', communityName: this.name() }));
      await this.loadInvitations();
    }
  }

  async onAppointed(result: AppointDone): Promise<void> {
    this.appoint.set(null);
    this.toasts.show(result.message);
    await this.refreshAll();
    this.focusArea(result.area);
  }

  async onChanged(result: ChangeDone): Promise<void> {
    this.change.set(null);
    if (result.selfEnded) {
      // Brief 05 §6 / 8.05.t: the person can no longer open this page.
      const user = await this.auth.refreshCurrentUser().catch(() => this.auth.user());
      this.notices.show(`You're no longer a Community Manager of ${this.name()}.`);
      const target = landingTargetFor(user);
      this.nav.setActiveCommunity(target.activeCommunityId);
      await this.router.navigate(target.commands);
      return;
    }
    this.toasts.show(result.message);
    await this.refreshAll();
    this.focusArea(result.area);
  }

  async closeAfterRefusal(): Promise<void> {
    this.change.set(null);
    await this.refreshAll();
  }

  jump(event: Event, area: Area | 'residents'): void {
    event.preventDefault();
    this.focusArea(area);
  }

  areaHeading(area: Area): HTMLElement | null {
    return this.host.nativeElement.querySelector<HTMLElement>(`[data-setting-id="${area}"]`);
  }

  private focusArea(area: Area | 'residents'): void {
    setTimeout(() => this.host.nativeElement.querySelector<HTMLElement>(`[data-setting-id="${area}"]`)?.focus());
  }

  private async refreshAll(): Promise<void> {
    await Promise.all([this.loadMemberships(), this.loadInvitations(), this.ctx.reload()]);
  }

  /** Downgrading yourself would grant you a role unless you already hold Board Member here (brief 05 Q7). */
  private mayDowngrade(m: SettingsMembership): boolean {
    if (m.userId !== this.currentUserId()) return true;
    return (this.memberships() ?? []).some(x => x.userId === m.userId && x.role === 'BoardMember' && x.effective);
  }

  private split(match: (m: SettingsMembership) => boolean, order: (a: SettingsMembership, b: SettingsMembership) => number) {
    const rows = (this.memberships() ?? []).filter(match);
    return {
      active: rows.filter(m => m.effective).sort(order),
      ended: rows.filter(m => !m.effective).sort((a, b) => (b.endDate ?? '').localeCompare(a.endDate ?? '')),
    };
  }
}

const byName = (a: SettingsMembership, b: SettingsMembership) => a.userDisplayName.localeCompare(b.userDisplayName);

/** Open-ended first, then by end date; A–Z within each (brief 05 §4.1). */
function managerOrder(a: SettingsMembership, b: SettingsMembership): number {
  if (a.openEnded !== b.openEnded) return a.openEnded ? -1 : 1;
  return (a.endDate ?? '').localeCompare(b.endDate ?? '') || byName(a, b);
}

/** Board Members, then Accountants; A–Z within each. */
function boardOrder(a: SettingsMembership, b: SettingsMembership): number {
  if (a.role !== b.role) return a.role === 'BoardMember' ? -1 : 1;
  return byName(a, b);
}

/** Open invitations first, newest sent first; then revoked, newest first. */
function sortInvitations(items: CommunityInvitation[]): CommunityInvitation[] {
  const open = (i: CommunityInvitation) => (isOpenInvitation(i) ? 0 : 1);
  return [...items].sort((a, b) => open(a) - open(b) || (b.sentAt ?? '').localeCompare(a.sentAt ?? ''));
}
