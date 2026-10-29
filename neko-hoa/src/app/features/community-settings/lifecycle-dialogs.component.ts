import { Component, ViewChild, computed, effect, inject, signal, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import {
  CompanyService, LifecycleAction, LifecycleConsequences, LifecyclePreview, MembershipKeptPreview, OpenWorkItem,
} from '../../core/services/company.service';
import { CommunitySettingsService } from '../../core/services/community-settings.service';
import { ToastService } from '../../core/services/toast.service';
import { DialogComponent } from '../../shared/dialog.component';
import { formatDateTimeInZone, formatPlainDate, formatTodayUtc } from '../../shared/date-format';
import {
  RATE_LIMITED_MESSAGE, openWorkItemLine, refusalCodeOf, refusalMessage, roleLabel, serverMessageOf,
} from '../../shared/refusal-messages';
import { CommunitySettingsContext } from './community-settings-context';

type View = 'loading' | 'loadError' | 'ready' | 'refused';

interface Refused {
  message: string;
  items: OpenWorkItem[];
  link: { label: string; commands: string[] } | null;
}

interface EndedRow {
  name: string;
  role: string;
  endedOn: string;
}

/** "A", "A and B", "A, B and C" (the brief's lists have no serial comma). */
function joinAnd(items: string[]): string {
  return items.length <= 1 ? (items[0] ?? '') : `${items.slice(0, -1).join(', ')} and ${items[items.length - 1]}`;
}

/** "Morgan Ellis (Community Manager), Rowan Hayes and Jesse Lin (Board Members), and Avery Quinn (Accountant)". */
function membershipsByRole(rows: MembershipKeptPreview[]): string {
  const groups = new Map<string, string[]>();
  for (const m of rows) groups.set(m.role, [...(groups.get(m.role) ?? []), m.name]);
  const parts = Array.from(groups, ([role, names]) => `${joinAnd(names)} (${roleLabel(role)}${names.length > 1 ? 's' : ''})`);
  if (parts.length <= 2) return parts.join(' and ');
  return `${parts.slice(0, -1).join(', ')}, and ${parts[parts.length - 1]}`;
}

function isOpenEndedManager(m: MembershipKeptPreview): boolean {
  return m.role === 'CommunityManager' && !m.endDate;
}

/**
 * 030 US3 / FR-010–FR-015 / brief 04 §5.4–§5.7: the Mark Active, Archive and Restore dialogs of
 * Community settings, opened by the page header's status buttons (`ctx.lifecycleRequest`).
 *
 * Each dialog asks the server what will change when it opens (D4): it lists the preview's counts
 * and names, or, when the action would be refused, only the refusal and "Close". A refusal after
 * confirming (a race) looks the same. Restore lists the memberships that regain access, each with
 * "End membership", which swaps the dialog to a short confirmation and back (D5).
 */
@Component({
  selector: 'app-lifecycle-dialogs',
  standalone: true,
  imports: [DialogComponent, RouterLink],
  template: `
    @if (action(); as act) {
      <app-dialog [heading]="heading()" [fallbackFocus]="pageTitle" (dismiss)="cancel()">
        @if (endTarget(); as m) {
          <!-- D5: end a membership first, inside the restore dialog. -->
          @if (stepError()) { <p class="dlg-refusal field-error" role="alert">{{ stepError() }}</p> }
          <ul class="lc__lines">
            <li>It ends today, {{ today }}.</li>
            <li>{{ m.name }} won't get access back when {{ name() }} is restored.</li>
            @if (endsLastManager()) {
              <li>{{ name() }} will then have no Community Manager with no end date, so it returns to Onboarding when restored.</li>
            }
          </ul>
          <p class="dlg-section">{{ m.name }}'s account and other memberships don't change.</p>
          <div class="dlg-actions">
            <button type="button" class="btn btn--primary" [disabled]="busy()" (click)="endMembership(m)">{{ busy() ? 'Ending…' : 'End membership' }}</button>
            <button type="button" class="btn" [disabled]="busy()" (click)="back()">Back</button>
          </div>
        } @else {
          @switch (view()) {
            @case ('loading') {
              <p class="sr-only" role="status">Loading what this will change…</p>
              <div class="ph lc__ph" aria-hidden="true"></div>
            }
            @case ('loadError') {
              <div class="dlg-refusal" role="alert">
                <p class="field-error">Couldn't load what this will change.</p>
                <button type="button" class="btn" (click)="loadPreview()">Try again</button>
              </div>
            }
            @case ('refused') {
              @if (refused(); as r) {
                <div class="dlg-refusal" role="alert" tabindex="-1">
                  <p>{{ r.message }}</p>
                  @if (r.items.length) {
                    <ul class="lc__list" aria-label="Open work">
                      @for (item of r.items; track $index) { <li>{{ itemLine(item) }}</li> }
                    </ul>
                    <p class="field-help">The board decides open architectural applications, and the Community Manager records each outcome in board mode. Archive each sub-association first.</p>
                  }
                  @if (r.link) { <a class="link" [routerLink]="r.link.commands" (click)="close()">{{ r.link.label }}</a> }
                </div>
              }
            }
            @default {
              @if (endedNote()) { <p class="lc__note" role="status">{{ endedNote() }}</p> }
              @if (consequences(); as c) {
                @switch (act) {
                  @case ('activate') {
                    <ul class="lc__lines">
                      <li>{{ name() }} goes live. Residents linked to its homes can use them from their next visit.</li>
                      <li>
                        {{ scheduledLine(c) }}
                        @if (c.scheduledInvitationsToSend?.length) {
                          <ul class="lc__list" aria-label="Invitations emailed now">
                            @for (i of c.scheduledInvitationsToSend ?? []; track $index) { <li>{{ i.email }} · {{ i.propertyAddress }}</li> }
                          </ul>
                        }
                      </li>
                      @if (c.scheduledInvitationsRevoked?.length) { <li>{{ scheduledRevokedLine(c) }}</li> }
                    </ul>
                  }
                  @case ('archive') {
                    <p>The management company will no longer manage {{ name() }}. This is what happens:</p>
                    <ul class="lc__lines">
                      <li>{{ autopayLine(c) }}</li>
                      <li>{{ residentsLine(c) }}</li>
                      <li>Residents keep their homes, marked “No longer managed”. They can still read the ledger, statements, receipts and transactions. Nothing else is open to them.</li>
                      <li>{{ invitationsLine(c) }}</li>
                      <li>{{ membershipsLine(c) }}</li>
                      <li>It leaves every community switcher and My Communities.</li>
                      <li>Its Community settings become read-only. Only Company Administrators can open them.</li>
                    </ul>
                    <div class="dlg-section">
                      <h3>What doesn't change</h3>
                      <ul>
                        <li>Nothing is deleted. The profile, settings, memberships, residents and invitations are kept.</li>
                        <li>Refunds, bank returns and disputes on earlier payments still go through.</li>
                        <li>You can restore it later. Autopay won't restart, and revoked invitations stay revoked.</li>
                      </ul>
                    </div>
                  }
                  @case ('restore') {
                    <p><strong>{{ outcomeLine(c) }}</strong></p>
                    <div class="dlg-section">
                      <p id="lc-regaining">These memberships will give access again:</p>
                      @if (c.membershipsRegainingAccess?.length) {
                        <ul class="lc__rows" aria-labelledby="lc-regaining">
                          @for (m of c.membershipsRegainingAccess ?? []; track m.membershipId) {
                            <li class="lc__row">
                              <span class="lc__row-text">{{ memberLine(m) }}</span>
                              <button type="button" class="btn" [attr.aria-label]="'End ' + m.name + '\\'s ' + role(m.role) + ' membership before restoring'"
                                      (click)="startEnd(m)">End membership</button>
                            </li>
                          }
                        </ul>
                      } @else {
                        <p class="muted">No memberships will give access again.</p>
                      }
                    </div>
                    @if (ended().length) {
                      <div class="dlg-section">
                        <p id="lc-ended">These memberships have ended and stay ended:</p>
                        <ul aria-labelledby="lc-ended">
                          @for (e of ended(); track $index) { <li>{{ e.name }} · {{ role(e.role) }} · Ended {{ e.endedOn }}</li> }
                        </ul>
                      </div>
                    }
                    <ul class="lc__lines">
                      <li>{{ switchersLine(c) }}</li>
                      <li>{{ residentsRestoreLine(c) }}</li>
                    </ul>
                    <div class="dlg-section">
                      <h3>What doesn't change</h3>
                      <ul>
                        <li>Memberships keep their dates. The profile and settings don't change.</li>
                        <li>Autopay cancelled by the archive doesn't restart. Residents enroll again themselves.</li>
                        <li>Invitations revoked by the archive stay revoked.</li>
                      </ul>
                    </div>
                  }
                }
              }
            }
          }
          @if (actionError()) { <p class="dlg-refusal field-error" role="alert">{{ actionError() }}</p> }
          <div class="dlg-actions">
            @if (view() === 'refused') {
              <button type="button" class="btn btn--primary" (click)="closeAfterRefusal()">Close</button>
            } @else {
              <button type="button" class="btn btn--primary" [disabled]="view() !== 'ready' || busy()" (click)="confirm()">{{ primaryLabel() }}</button>
              <button type="button" class="btn" [disabled]="busy()" (click)="cancel()">Cancel</button>
              @if (rateLimited()) { <span class="field-error" role="alert">{{ rateLimitedMessage }}</span> }
            }
          </div>
        }
      </app-dialog>
    }
  `,
  styles: [`
    .lc__ph { height: 80px; margin-top: 10px; }
    .lc__lines { margin: 10px 0 0 18px; display: flex; flex-direction: column; gap: 6px; }
    .lc__list { margin: 6px 0 0 18px; display: flex; flex-direction: column; gap: 4px; }
    .lc__rows { list-style: none; display: flex; flex-direction: column; gap: 6px; margin-top: 6px; }
    .lc__row { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 8px; }
    .lc__note { margin-top: 8px; font-weight: 600; }
  `],
})
export class LifecycleDialogsComponent {
  private ctx = inject(CommunitySettingsContext);
  private company = inject(CompanyService);
  private settingsService = inject(CommunitySettingsService);
  private toasts = inject(ToastService);

  @ViewChild(DialogComponent) private dialog?: DialogComponent;

  readonly rateLimitedMessage = RATE_LIMITED_MESSAGE;
  readonly today = formatTodayUtc();
  pageTitle: HTMLElement | null = null;

  readonly action = signal<LifecycleAction | null>(null);
  readonly view = signal<View>('loading');
  readonly preview = signal<LifecyclePreview | null>(null);
  readonly refused = signal<Refused | null>(null);
  readonly busy = signal(false);
  readonly actionError = signal<string | null>(null);
  readonly rateLimited = signal(false);
  readonly endTarget = signal<MembershipKeptPreview | null>(null);
  readonly stepError = signal<string | null>(null);
  readonly endedNote = signal<string | null>(null);
  readonly ended = signal<EndedRow[]>([]);

  readonly name = computed(() => this.ctx.settings()?.communityName ?? '');
  readonly consequences = computed<LifecycleConsequences>(() => this.preview()?.consequences ?? {});

  readonly heading = computed(() => {
    const m = this.endTarget();
    if (m) return `End ${m.name}'s ${roleLabel(m.role)} membership first?`;
    switch (this.action()) {
      case 'activate': return `Mark ${this.name()} Active?`;
      case 'archive': return `Archive ${this.name()}?`;
      default: return `Restore ${this.name()}?`;
    }
  });

  readonly primaryLabel = computed(() => {
    const busy = this.busy();
    switch (this.action()) {
      case 'activate': return busy ? 'Marking Active…' : 'Mark community Active';
      case 'archive': return busy ? 'Archiving…' : 'Archive community';
      default: return busy ? 'Restoring…' : 'Restore community';
    }
  });

  /** Ending this membership leaves no Community Manager with no end date. */
  readonly endsLastManager = computed(() => {
    const m = this.endTarget();
    const managers = (this.consequences().membershipsRegainingAccess ?? []).filter(isOpenEndedManager);
    return !!m && isOpenEndedManager(m) && managers.length === 1 && managers[0].membershipId === m.membershipId;
  });

  private handledSeq = 0;
  private previewSeq = 0;

  constructor() {
    effect(() => {
      const request = this.ctx.lifecycleRequest();
      if (!request || request.seq === this.handledSeq) return;
      this.handledSeq = request.seq;
      untracked(() => this.open(request.action));
    });
  }

  // ─── Copy (brief 04 §5.5–§5.7) ─────────────────────────────────────────────

  itemLine(item: OpenWorkItem): string {
    return openWorkItemLine(item);
  }

  role(role: string): string {
    return roleLabel(role);
  }

  scheduledLine(c: LifecycleConsequences): string {
    const list = c.scheduledInvitationsToSend ?? [];
    if (!list.length) return 'No resident invitations are scheduled.';
    // The browser's time zone: the settings page doesn't carry the community's (deviation noted).
    const until = formatDateTimeInZone(list[0].expiresAtIfSentNow, Intl.DateTimeFormat().resolvedOptions().timeZone);
    return list.length === 1
      ? `1 scheduled resident invitation is emailed now. Its link works for 72 hours, until ${until}:`
      : `${list.length} scheduled resident invitations are emailed now. Each link works for 72 hours, until ${until}:`;
  }

  scheduledRevokedLine(c: LifecycleConsequences): string {
    const list = c.scheduledInvitationsRevoked ?? [];
    const names = list.map(i => `${i.email} · ${i.propertyAddress}`).join(', ');
    return list.length === 1
      ? `1 scheduled invitation is revoked instead, because the person who sent it can no longer send it: ${names}.`
      : `${list.length} scheduled invitations are revoked instead, because the people who sent them can no longer send them: ${names}.`;
  }

  autopayLine(c: LifecycleConsequences): string {
    const n = c.autopayCancelled ?? 0;
    if (!n) return 'No autopay schedules are active.';
    return `${n === 1 ? '1 autopay schedule is' : `${n} autopay schedules are`} cancelled. No new automatic charge starts. A charge already submitted settles normally.`;
  }

  residentsLine(c: LifecycleConsequences): string {
    const n = c.residentsEmailed ?? 0;
    if (!n) return 'No residents are linked, so no one is emailed.';
    return `${n === 1 ? '1 resident is' : `${n} residents are`} emailed once. The email says the management company no longer manages ${this.name()}, that autopay has ended where it applied, and that their payment history stays readable.`;
  }

  invitationsLine(c: LifecycleConsequences): string {
    const list = c.invitationsRevoked ?? [];
    if (!list.length) return 'No invitations are pending.';
    const names = joinAnd(list.map(i => `${i.email} (${i.role ? roleLabel(i.role) : 'resident'})`));
    return list.length === 1
      ? `1 invitation is revoked, and its link stops working: ${names}.`
      : `${list.length} invitations are revoked, and their links stop working: ${names}.`;
  }

  membershipsLine(c: LifecycleConsequences): string {
    const list = c.membershipsKept ?? [];
    if (!list.length) return 'No memberships are kept.';
    return list.length === 1
      ? `1 membership is kept but gives no access while it is archived: ${membershipsByRole(list)}.`
      : `${list.length} memberships are kept but give no access while it is archived: ${membershipsByRole(list)}.`;
  }

  outcomeLine(c: LifecycleConsequences): string {
    const name = this.name();
    if (c.returnsTo === 'Active') {
      const managers = (c.membershipsRegainingAccess ?? []).filter(isOpenEndedManager).map(m => m.name);
      return `${name} returns to Active, because it was Active before it was archived and it has a Community Manager with no end date: ${joinAnd(managers)}.`;
    }
    if (c.reason === 'NeverActivated') {
      return `${name} returns to Onboarding, because it had never been Active before it was archived. Residents won't see it until it is marked Active.`;
    }
    return `${name} returns to Onboarding, because it has no Community Manager with no end date. It can be marked Active once it has one.`;
  }

  memberLine(m: MembershipKeptPreview): string {
    const base = `${m.name} · ${roleLabel(m.role)}`;
    if (isOpenEndedManager(m)) return `${base} · no end date`;
    return m.endDate ? `${base} · ends ${formatPlainDate(m.endDate)}` : base;
  }

  switchersLine(c: LifecycleConsequences): string {
    return c.returnsTo === 'Active'
      ? 'It appears again in the community switchers and My Communities of the people above.'
      : 'It appears again in the community switchers and My Communities of the people above, marked Onboarding.';
  }

  residentsRestoreLine(c: LifecycleConsequences): string {
    return c.returnsTo === 'Active'
      ? 'Residents can use their homes again. The “No longer managed” label goes away.'
      : "Residents can't see their homes, including payment history, until it is marked Active.";
  }

  // ─── Flow ──────────────────────────────────────────────────────────────────

  async loadPreview(): Promise<void> {
    const action = this.action();
    const id = this.ctx.communityId();
    if (!action || !id) return;
    const seq = ++this.previewSeq;
    this.view.set('loading');
    try {
      const preview = await this.company.getLifecyclePreview(id, action);
      if (seq !== this.previewSeq) return;
      this.preview.set(preview);
      if (preview.allowed) this.view.set('ready');
      else this.showRefusal(preview.refusal?.code ?? null, preview.refusal?.items ?? [], preview.refusal?.message ?? null);
    } catch {
      if (seq === this.previewSeq) this.view.set('loadError');
    }
  }

  async confirm(): Promise<void> {
    const action = this.action();
    const id = this.ctx.communityId();
    if (!action || !id || this.busy() || this.view() !== 'ready') return;
    const name = this.name();
    this.busy.set(true);
    this.actionError.set(null);
    this.rateLimited.set(false);
    try {
      let message: string;
      if (action === 'activate') {
        const result = await this.company.activate(id);
        const sent = result.invitationsSent;
        message = sent
          ? `${name} is now Active. ${sent === 1 ? '1 resident invitation was' : `${sent} resident invitations were`} emailed.`
          : `${name} is now Active.`;
      } else if (action === 'archive') {
        await this.company.archive(id);
        message = `${name} is archived.`;
      } else {
        const result = await this.company.restore(id);
        message = result.status === 'Active'
          ? `${name} is restored and Active.`
          : `${name} is restored. It stays Onboarding until it has a Community Manager with no end date.`;
      }
      this.busy.set(false);
      this.close();
      this.toasts.show(message);
      await this.ctx.reload();
      // 8.04.k: focus is on the page title afterwards.
      setTimeout(() => document.getElementById('csp-title')?.focus());
    } catch (err) {
      this.busy.set(false);
      this.onActionError(err, action, name);
    }
  }

  startEnd(m: MembershipKeptPreview): void {
    this.stepError.set(null);
    this.endTarget.set(m);
    this.dialog?.focus('.dlg__title');
  }

  back(): void {
    this.endTarget.set(null);
    this.dialog?.focus('.dlg__title');
  }

  async endMembership(m: MembershipKeptPreview): Promise<void> {
    const id = this.ctx.communityId();
    if (!id || this.busy()) return;
    this.busy.set(true);
    this.stepError.set(null);
    try {
      await this.settingsService.updateMembership(id, m.membershipId, { end: true });
    } catch (err) {
      this.busy.set(false);
      const code = refusalCodeOf(err);
      this.stepError.set(code === null
        ? "Couldn't end the membership. Check your connection and try again."
        : code === 'RATE_LIMITED' ? RATE_LIMITED_MESSAGE
          : serverMessageOf(err) ?? refusalMessage(code, { communityName: this.name(), audience: this.ctx.audience(), surface: 'action' }));
      return;
    }
    this.busy.set(false);
    this.ended.update(rows => [...rows, { name: m.name, role: m.role, endedOn: this.today }]);
    this.endedNote.set(`${m.name}'s ${roleLabel(m.role)} membership ended.`);
    this.endTarget.set(null);
    this.dialog?.focus('.dlg__title');
    await this.loadPreview();
  }

  cancel(): void {
    if (this.busy()) return;
    this.close();
  }

  /** 8.04.m: after "Close" on a refusal, the page refreshes. */
  closeAfterRefusal(): void {
    this.close();
    void this.ctx.reload();
  }

  close(): void {
    this.action.set(null);
    this.previewSeq++;
  }

  private open(action: LifecycleAction): void {
    this.pageTitle = document.getElementById('csp-title');
    this.action.set(action);
    this.preview.set(null);
    this.refused.set(null);
    this.actionError.set(null);
    this.rateLimited.set(false);
    this.busy.set(false);
    this.endTarget.set(null);
    this.stepError.set(null);
    this.endedNote.set(null);
    this.ended.set([]);
    void this.loadPreview();
  }

  private onActionError(err: unknown, action: LifecycleAction, name: string): void {
    const code = refusalCodeOf(err);
    if (code === 'RATE_LIMITED') {
      this.rateLimited.set(true);
      return;
    }
    if (code === null) {
      const what = action === 'activate' ? `mark ${name} Active` : action === 'archive' ? `archive ${name}` : `restore ${name}`;
      this.actionError.set(`Couldn't ${what}. Check your connection and try again.`);
      return;
    }
    const items = (err as { error?: { items?: OpenWorkItem[] } })?.error?.items ?? [];
    this.showRefusal(code, items, serverMessageOf(err));
  }

  private showRefusal(code: string | null, items: OpenWorkItem[], serverMessage: string | null): void {
    const s = this.ctx.settings();
    const company = this.ctx.mode() === 'company';
    const id = s?.id ?? '';
    let link: Refused['link'] = null;
    if (code === 'NO_ACTIVE_MANAGER') {
      link = {
        label: 'Open Managers and memberships',
        commands: company ? ['/app/company/communities', id, 'settings', 'managers-and-memberships'] : ['/app/board/settings/managers-and-memberships'],
      };
    } else if (code === 'PARENT_ARCHIVED' && company && s?.parent) {
      link = { label: `Open ${s.parent.communityName}`, commands: ['/app/company/communities', s.parent.id, 'settings', 'profile'] };
    }
    const known = code !== null && code !== 'VALIDATION_ERROR';
    this.refused.set({
      message: known
        ? refusalMessage(code, {
          communityName: s?.communityName, audience: this.ctx.audience(), surface: 'action',
          variant: code === 'PARENT_ARCHIVED' && this.action() === 'restore' ? 'restore' : undefined,
          parentName: s?.parent?.communityName ?? null,
        })
        : serverMessage ?? refusalMessage(code),
      items,
      link,
    });
    this.view.set('refused');
    this.dialog?.focus('.dlg-refusal');
  }
}
