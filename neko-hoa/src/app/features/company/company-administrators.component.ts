import { ChangeDetectorRef, Component, OnInit, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { CompanyAdministrator, CompanyInvitation, CompanyService } from '../../core/services/company.service';
import { AuthService } from '../../core/services/auth.service';
import { BoardNavigationService } from '../../core/services/board-navigation.service';
import { ToastService } from '../../core/services/toast.service';
import { landingTargetFor } from '../../core/services/landing';
import { Paged } from '../../core/services/board.service';
import { DialogComponent } from '../../shared/dialog.component';
import { PaginationComponent } from '../../shared/pagination.component';
import { DisabledReasonDirective } from '../../shared/disabled-reason.directive';
import { formatDateTimeInZone, formatPlainDate } from '../../shared/date-format';
import { invitationStatusLabel } from '../../shared/invitation-display';
import { RATE_LIMITED_MESSAGE, SAVE_FAILED_MESSAGE, refusalCodeOf, refusalMessage, serverMessageOf } from '../../shared/refusal-messages';
import { InviteCompanyAdminDialogComponent } from './invite-company-admin-dialog.component';

const LIMIT = 25;

type ListState = 'loading' | 'ready' | 'error';

/**
 * 030 US7 / FR-037 / brief 09: the Company administrators page (company area).
 *
 * - **Current Company Administrators**, paged: "Revoke role" on every row asks first. The last
 *   administrator's is disabled, with the `LAST_COMPANY_ADMIN` reason on hover and focus; a race is
 *   still refused in the dialog. Revoking yourself says you lose access, and then the app takes you
 *   to your landing.
 * - **Invitations**, paged: Resend (no confirmation) and Revoke (asks first).
 * - "Invite a Company Administrator": email only, never a lookup (FR-018).
 *
 * Times show in the company default time zone (FR-022).
 */
@Component({
  selector: 'app-company-administrators',
  standalone: true,
  imports: [DialogComponent, PaginationComponent, DisabledReasonDirective, InviteCompanyAdminDialogComponent],
  template: `
    <div class="page-header">
      <h1 class="page-title" id="ca-title" tabindex="-1">Company administrators</h1>
      <p class="muted">The people who set up the management company's business in this app.</p>
    </div>

    <section class="card card--lav" aria-labelledby="ca-note-title">
      <h2 class="section-title" id="ca-note-title">What this role is</h2>
      <p>Company Administrators set up the company's business here: communities, their managers and residents, company defaults, and who holds this role. It's a business role, not a platform or technical one. It gives no board powers in any community: no vote on architectural applications, no recording of their outcomes, no reading of applications, and no homeowner or financial data beyond these company pages. To do those things in a community, you need a role there, given by another Company Administrator or that community's Community Manager.</p>
    </section>

    <section class="card ca__card" aria-labelledby="ca-admins-title">
      <div class="ca__head">
        <h2 class="section-title" id="ca-admins-title" tabindex="-1">Current Company Administrators</h2>
        <button type="button" class="btn btn--primary ca__invite" (click)="inviteOpen.set(true)">Invite a Company Administrator</button>
      </div>
      <p class="muted" id="ca-rule">The company always needs at least one Company Administrator. The last one can't be revoked.</p>
      @switch (adminsState()) {
        @case ('loading') { <p class="sr-only" role="status">Loading Company Administrators…</p><div class="ph ca__ph" aria-hidden="true"></div> }
        @case ('error') {
          <div role="alert" class="ca__error">
            <p>Couldn't load Company Administrators.</p>
            <button type="button" class="btn" (click)="loadAdmins()">Try again</button>
          </div>
        }
        @default {
          <div class="ca__table-wrap">
            <table class="table" aria-describedby="ca-rule">
              <caption class="sr-only">Current Company Administrators</caption>
              <thead><tr><th scope="col">Name</th><th scope="col">Email</th><th scope="col">Granted by</th><th scope="col">Granted</th><th scope="col">Actions</th></tr></thead>
              <tbody>
                @for (a of admins()?.items ?? []; track a.userId) {
                  <tr>
                    <th scope="row">{{ a.name }}{{ isMe(a) ? ' (you)' : '' }}</th>
                    <td>{{ a.email }}</td>
                    <td>
                      @if (a.grantedBy) { <span>{{ a.grantedBy }}</span> }
                      @else { <span aria-hidden="true">Setup step</span><span class="sr-only">Granted by the setup step</span> }
                    </td>
                    <td>{{ granted(a) }}</td>
                    <td>
                      <button type="button" class="btn ca__action" [attr.aria-label]="revokeLabel(a)" [appDisabledReason]="lastReason()"
                              (click)="openRevokeRole(a)">Revoke role</button>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
          <app-pagination [totalItems]="admins()?.total ?? 0" [limit]="limit" [offset]="adminsOffset()" noun="Company Administrators"
                          (pageChange)="pageAdmins($event)" />
        }
      }
    </section>

    <section class="card ca__card" aria-labelledby="ca-inv-title">
      <h2 class="section-title" id="ca-inv-title" tabindex="-1">Invitations</h2>
      <p class="muted">Invitations to be a Company Administrator. Each link works once, for 72 hours, and only for the email it was sent to. Times are in the company's default time zone.</p>
      @switch (invitationsState()) {
        @case ('loading') { <p class="sr-only" role="status">Loading invitations…</p><div class="ph ca__ph" aria-hidden="true"></div> }
        @case ('error') {
          <div role="alert" class="ca__error">
            <p>Couldn't load invitations.</p>
            <button type="button" class="btn" (click)="loadInvitations()">Try again</button>
          </div>
        }
        @default {
          @if (invitations()?.items?.length) {
            <div class="ca__table-wrap">
              <table class="table">
                <caption class="sr-only">Company Administrator invitations</caption>
                <thead><tr><th scope="col">Email</th><th scope="col">Sent by</th><th scope="col">Sent</th><th scope="col">Expires</th><th scope="col">Status</th><th scope="col">Actions</th></tr></thead>
                <tbody>
                  @for (i of invitations()?.items ?? []; track i.id) {
                    <tr>
                      <th scope="row">{{ i.email }}</th>
                      <td>{{ i.sentBy || '—' }}</td>
                      <td>{{ time(i.sentAt) }}</td>
                      <td>{{ time(i.expiresAt) }}</td>
                      <td><span class="pill">{{ status(i) }}</span></td>
                      <td>
                        <span class="ca__actions">
                          @if (i.canResend) {
                            <button type="button" class="btn ca__action" [attr.aria-label]="'Resend invitation to ' + i.email" (click)="resend(i)">Resend</button>
                          }
                          @if (i.canRevoke) {
                            <button type="button" class="btn ca__action" [attr.aria-label]="'Revoke invitation to ' + i.email" (click)="openRevokeInvitation(i)">Revoke</button>
                          }
                        </span>
                        @if (rateLimitedId() === i.id) { <span class="field-error" role="alert">{{ rateLimitedMessage }}</span> }
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
            <app-pagination [totalItems]="invitations()?.total ?? 0" [limit]="limit" [offset]="invitationsOffset()" noun="invitations"
                            (pageChange)="pageInvitations($event)" />
          } @else {
            <p class="muted">No invitations.</p>
          }
        }
      }
    </section>

    @if (inviteOpen()) {
      <app-invite-company-admin-dialog (done)="onInvited($event)" (cancelled)="inviteOpen.set(false)" (closeAfterRefusal)="reloadAfterRefusal()" />
    }

    @if (revokeRole(); as a) {
      <app-dialog [heading]="isMe(a) ? 'Revoke your own Company Administrator role?' : 'Revoke ' + a.name + '\\'s Company Administrator role?'" (dismiss)="closeRevokeRole()">
        @if (dialogError()) { <p class="dlg-refusal field-error" role="alert" tabindex="-1">{{ dialogError() }}</p> }
        <div class="dlg-section">
          <h3>What happens</h3>
          <ul>
            @if (isMe(a)) {
              <li>You lose the Company Administrator role on your next request.</li>
              <li>After that, you can't open the company area.</li>
              <li>Any scheduled or pending invitations you sent that only a Company Administrator can send are revoked.</li>
            } @else {
              <li>{{ a.name }} loses the Company Administrator role on their next request.</li>
              <li>After that, {{ a.name }} can't open the company area.</li>
              <li>Any scheduled or pending invitations {{ a.name }} sent that only a Company Administrator can send are revoked.</li>
            }
          </ul>
        </div>
        <div class="dlg-section">
          <h3>What doesn't change</h3>
          <ul>
            @if (isMe(a)) { <li>Your account isn't deleted.</li> }
            @else { <li>{{ a.name }} keeps their account and their community roles. Community roles are separate from this role.</li> }
            @if (remaining(a)) { <li>The company still has a Company Administrator: {{ remaining(a) }}.</li> }
          </ul>
        </div>
        <div class="dlg-actions">
          @if (dialogClose()) {
            <button type="button" class="btn btn--primary" (click)="closeRefusedRevoke()">Close</button>
          } @else {
            <button type="button" class="btn btn--primary" [disabled]="busy()" (click)="confirmRevokeRole(a)">{{ busy() ? 'Revoking…' : (isMe(a) ? 'Revoke my role' : 'Revoke role') }}</button>
            <button type="button" class="btn" [disabled]="busy()" (click)="closeRevokeRole()">Cancel</button>
            @if (dialogRateLimited()) { <span class="field-error" role="alert">{{ rateLimitedMessage }}</span> }
          }
        </div>
      </app-dialog>
    }

    @if (revokeInvitation(); as i) {
      <app-dialog [heading]="'Revoke the invitation to ' + i.email + '?'" (dismiss)="closeRevokeInvitation()">
        @if (dialogError()) { <p class="dlg-refusal field-error" role="alert" tabindex="-1">{{ dialogError() }}</p> }
        <div class="dlg-section">
          <h3>What happens</h3>
          <ul>
            <li>The link in the invitation email stops working at once.</li>
            <li>{{ i.email }} can't use it to become a Company Administrator.</li>
          </ul>
        </div>
        <div class="dlg-section">
          <h3>What doesn't change</h3>
          <ul><li>You can send {{ i.email }} a new invitation later.</li></ul>
        </div>
        <div class="dlg-actions">
          @if (dialogClose()) {
            <button type="button" class="btn btn--primary" (click)="reloadAfterRefusal()">Close</button>
          } @else {
            <button type="button" class="btn btn--primary" [disabled]="busy()" (click)="confirmRevokeInvitation(i)">{{ busy() ? 'Revoking…' : 'Revoke invitation' }}</button>
            <button type="button" class="btn" [disabled]="busy()" (click)="closeRevokeInvitation()">Cancel</button>
            @if (dialogRateLimited()) { <span class="field-error" role="alert">{{ rateLimitedMessage }}</span> }
          }
        </div>
      </app-dialog>
    }
  `,
  styles: [`
    :host { display: flex; flex-direction: column; gap: 14px; }
    .ca__card { display: flex; flex-direction: column; gap: 8px; }
    .ca__head { display: flex; flex-wrap: wrap; align-items: center; gap: 10px; }
    .ca__invite { margin-left: auto; }
    .ca__ph { height: 120px; }
    .ca__error { display: flex; flex-direction: column; gap: 8px; align-items: flex-start; }
    .ca__table-wrap { overflow-x: auto; position: relative; }
    .ca__actions { display: inline-flex; gap: 6px; }
    .ca__action { min-height: 32px; }
  `],
})
export class CompanyAdministratorsComponent implements OnInit {
  private company = inject(CompanyService);
  private auth = inject(AuthService);
  private nav = inject(BoardNavigationService);
  private toasts = inject(ToastService);
  private router = inject(Router);
  private cdr = inject(ChangeDetectorRef);

  readonly limit = LIMIT;
  readonly rateLimitedMessage = RATE_LIMITED_MESSAGE;

  readonly admins = signal<Paged<CompanyAdministrator> | null>(null);
  readonly adminsState = signal<ListState>('loading');
  readonly adminsOffset = signal(0);
  readonly invitations = signal<Paged<CompanyInvitation> | null>(null);
  readonly invitationsState = signal<ListState>('loading');
  readonly invitationsOffset = signal(0);
  readonly timeZoneId = signal<string | null>(null);
  readonly rateLimitedId = signal<string | null>(null);

  readonly inviteOpen = signal(false);
  readonly revokeRole = signal<CompanyAdministrator | null>(null);
  readonly revokeInvitation = signal<CompanyInvitation | null>(null);
  readonly busy = signal(false);
  readonly dialogError = signal<string | null>(null);
  readonly dialogClose = signal(false);
  readonly dialogRateLimited = signal(false);
  /** After a race or FORBIDDEN: "Close" re-reads the page (which may move the person to their landing). */
  private closeReloads = false;

  /** US7-S3: with one administrator left, "Revoke role" is disabled with this reason. */
  readonly lastReason = computed(() => ((this.admins()?.total ?? 0) <= 1 ? refusalMessage('LAST_COMPANY_ADMIN') : null));

  ngOnInit(): void {
    setTimeout(() => document.getElementById('ca-title')?.focus());
    void this.loadAdmins();
    void this.loadInvitations();
    void this.company.getDefaults().then(d => this.timeZoneId.set(d.timeZoneId), () => undefined);
  }

  isMe(a: CompanyAdministrator): boolean {
    return a.userId === this.auth.user()?.id;
  }

  revokeLabel(a: CompanyAdministrator): string {
    return this.isMe(a) ? 'Revoke your own Company Administrator role' : `Revoke ${a.name}'s Company Administrator role`;
  }

  granted(a: CompanyAdministrator): string {
    return formatPlainDate(a.grantedAt);
  }

  time(iso: string | null): string {
    return iso ? formatDateTimeInZone(iso, this.timeZoneId()) : '—';
  }

  status(i: CompanyInvitation): string {
    return invitationStatusLabel(i);
  }

  /** The administrators who stay, as the dialog names them: "Robin Hale (you)", "Casey Novak". */
  remaining(target: CompanyAdministrator): string {
    const others = (this.admins()?.items ?? []).filter(a => a.userId !== target.userId).map(a => `${a.name}${this.isMe(a) ? ' (you)' : ''}`);
    return others.length <= 1 ? (others[0] ?? '') : `${others.slice(0, -1).join(', ')} and ${others[others.length - 1]}`;
  }

  async loadAdmins(): Promise<void> {
    if (!this.admins()) this.adminsState.set('loading');
    try {
      this.admins.set(await this.company.getAdministrators(LIMIT, this.adminsOffset()));
      this.adminsState.set('ready');
    } catch (err) {
      if (!this.refusedPage(err)) this.adminsState.set('error');
    }
  }

  async loadInvitations(): Promise<void> {
    if (!this.invitations()) this.invitationsState.set('loading');
    try {
      this.invitations.set(await this.company.getInvitations({ limit: LIMIT, offset: this.invitationsOffset() }));
      this.invitationsState.set('ready');
    } catch (err) {
      if (!this.refusedPage(err)) this.invitationsState.set('error');
    }
  }

  pageAdmins(offset: number): void {
    this.adminsOffset.set(offset);
    void this.loadAdmins();
  }

  pageInvitations(offset: number): void {
    this.invitationsOffset.set(offset);
    void this.loadInvitations();
  }

  /** Resend has no confirmation; focus stays on the button. */
  async resend(i: CompanyInvitation): Promise<void> {
    this.rateLimitedId.set(null);
    try {
      await this.company.resendInvitation(i.id);
      this.toasts.show(`Invitation sent again to ${i.email}. The earlier link no longer works.`);
    } catch (err) {
      const code = refusalCodeOf(err);
      if (code === 'RATE_LIMITED') {
        this.rateLimitedId.set(i.id);
        return;
      }
      this.toasts.show(code === null
        ? "Couldn't resend the invitation. Check your connection and try again."
        : refusalMessage(code, { audience: 'companyAdmin', surface: 'action', email: i.email }));
    }
    await this.loadInvitations();
  }

  onInvited(message: string): void {
    this.inviteOpen.set(false);
    this.toasts.show(message);
    this.focusAfterRender('ca-inv-title');
    void this.loadInvitations();
  }

  openRevokeRole(a: CompanyAdministrator): void {
    if (this.lastReason()) return;
    this.resetDialog();
    this.revokeRole.set(a);
  }

  closeRevokeRole(): void {
    if (!this.busy()) this.revokeRole.set(null);
  }

  /** A refused revoke: your own (last one) returns to the page; a race or FORBIDDEN re-reads it. */
  closeRefusedRevoke(): void {
    this.revokeRole.set(null);
    if (this.closeReloads) this.reloadAfterRefusal();
  }

  async confirmRevokeRole(a: CompanyAdministrator): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    this.dialogError.set(null);
    this.dialogRateLimited.set(false);
    try {
      await this.company.revokeAdministrator(a.userId);
    } catch (err) {
      this.busy.set(false);
      const code = refusalCodeOf(err);
      if (code === 'LAST_COMPANY_ADMIN') {
        const self = this.isMe(a);
        this.closeReloads = !self;
        this.showDialogRefusal(refusalMessage(code, self ? {} : { variant: 'race', personName: a.name }));
      } else {
        this.onDialogError(err);
      }
      return;
    }
    this.busy.set(false);
    this.revokeRole.set(null);
    if (this.isMe(a)) {
      // FR-036a: the next request is refused; go to the landing now.
      this.toasts.show("You're no longer a Company Administrator.");
      const user = await this.auth.refreshCurrentUser().catch(() => this.auth.user());
      const target = landingTargetFor(user);
      this.nav.setActiveCommunity(target.activeCommunityId);
      await this.router.navigate(target.commands);
      return;
    }
    this.toasts.show(`${a.name} is no longer a Company Administrator.`);
    this.focusAfterRender('ca-admins-title');
    await this.loadAdmins();
  }

  openRevokeInvitation(i: CompanyInvitation): void {
    this.resetDialog();
    this.revokeInvitation.set(i);
  }

  closeRevokeInvitation(): void {
    if (!this.busy()) this.revokeInvitation.set(null);
  }

  async confirmRevokeInvitation(i: CompanyInvitation): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    this.dialogError.set(null);
    this.dialogRateLimited.set(false);
    try {
      await this.company.revokeInvitation(i.id);
      this.busy.set(false);
      this.revokeInvitation.set(null);
      this.toasts.show(`Invitation to ${i.email} revoked.`);
    } catch (err) {
      this.busy.set(false);
      if (refusalCodeOf(err) === 'INVITATION_INVALID') {
        this.revokeInvitation.set(null);
        this.toasts.show(refusalMessage('INVITATION_INVALID', { audience: 'companyAdmin' }));
      } else {
        this.onDialogError(err);
        return;
      }
    }
    this.focusAfterRender('ca-inv-title');
    await this.loadInvitations();
  }

  /** "Close" after a refusal such as FORBIDDEN: re-read the page. */
  reloadAfterRefusal(): void {
    this.inviteOpen.set(false);
    this.revokeRole.set(null);
    this.revokeInvitation.set(null);
    void this.loadAdmins();
    void this.loadInvitations();
  }

  private resetDialog(): void {
    this.dialogError.set(null);
    this.dialogClose.set(false);
    this.dialogRateLimited.set(false);
    this.closeReloads = false;
  }

  private onDialogError(err: unknown): void {
    const code = refusalCodeOf(err);
    if (code === 'RATE_LIMITED') {
      this.dialogRateLimited.set(true);
      return;
    }
    if (code === null) {
      this.dialogError.set(SAVE_FAILED_MESSAGE);
      return;
    }
    this.closeReloads = true;
    this.showDialogRefusal(code === 'FORBIDDEN' ? refusalMessage(code, { surface: 'action' }) : serverMessageOf(err) ?? refusalMessage(code));
  }

  private showDialogRefusal(message: string): void {
    this.dialogError.set(message);
    this.dialogClose.set(true);
  }

  /** A 403 on a list: the person no longer holds the role (US7-S2). */
  private refusedPage(err: unknown): boolean {
    if (refusalCodeOf(err) !== 'FORBIDDEN') return false;
    this.toasts.show(refusalMessage('FORBIDDEN', { surface: 'page' }));
    const target = landingTargetFor(this.auth.user());
    this.nav.setActiveCommunity(target.activeCommunityId);
    void this.router.navigate(target.commands);
    return true;
  }

  /** Renders first, so a closing dialog's own focus return runs before this one. */
  private focusAfterRender(id: string): void {
    this.cdr.detectChanges();
    setTimeout(() => document.getElementById(id)?.focus());
  }
}
