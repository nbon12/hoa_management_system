import { ChangeDetectorRef, Component, ElementRef, OnDestroy, computed, effect, inject, signal, untracked } from '@angular/core';
import {
  CommunitySettingsService, PropertyInvitation, ResidentProperty, ResidentRow, ResidentsFilter, ResidentsPage,
} from '../../../core/services/community-settings.service';
import { ToastService } from '../../../core/services/toast.service';
import { PropertyDrawerComponent } from '../../../shared/property-drawer.component';
import { PaginationComponent } from '../../../shared/pagination.component';
import { DisabledReasonDirective } from '../../../shared/disabled-reason.directive';
import { formatPlainDate } from '../../../shared/date-format';
import { invitationStatusDetail, invitationStatusLabel, invitationTime, isOpenInvitation } from '../../../shared/invitation-display';
import { RATE_LIMITED_MESSAGE, refusalCodeOf, refusalMessage } from '../../../shared/refusal-messages';
import { CommunitySettingsContext } from '../community-settings-context';
import { FixedProperty, InviteDone, InviteResidentDialogComponent } from './invite-resident-dialog.component';
import { ResidentChange, ResidentChangeDialogComponent, ResidentChangeDone } from './resident-change-dialog.component';

const LIMIT = 25;
const SEARCH_DELAY_MS = 300;

type FilterKey = ResidentsFilter | 'all';

interface FilterButton {
  key: FilterKey;
  label: string;
  count: number;
}

const FILTER_EMPTY: Record<ResidentsFilter, string> = {
  noResident: 'Every property has a resident.',
  invitationPending: 'No properties have a pending or scheduled invitation.',
  invitationExpired: 'No properties have an expired invitation.',
};

/** "1 scheduled", "1 pending", "1 expired" (brief 06 §4.1); "None" when no invitation is open. */
function summaryParts(row: ResidentRow): string[] {
  const s = row.invitationSummary;
  return [
    s.scheduled ? `${s.scheduled} scheduled` : null,
    s.pending ? `${s.pending} pending` : null,
    s.expired ? `${s.expired} expired` : null,
  ].filter((p): p is string => !!p);
}

/** Newest first, revoked last (brief 06 §4.1). A Scheduled invitation has no send time yet: it is newest. */
function sortInvitations(list: PropertyInvitation[]): PropertyInvitation[] {
  const time = (i: PropertyInvitation) => (i.sentAt ? Date.parse(i.sentAt) : Number.MAX_SAFE_INTEGER);
  const revoked = (i: PropertyInvitation) => invitationStatusLabel(i).startsWith('Revoked');
  return [...list].sort((a, b) => Number(revoked(a)) - Number(revoked(b)) || time(b) - time(a));
}

/**
 * 030 US8 / FR-024–FR-024g / brief 06: the Residents area of Managers and memberships, projected
 * into its "Residents" card. One row per property (address, residents, invitations in words), with
 * search, one filter at a time and pages. Every action is in the property drawer (owner decision
 * 2026-10-17): Remove, Resend, Revoke and "Invite resident". The header's "Invite a resident" asks
 * for the property. In an Archived community only search, filters, pages and the drawer remain.
 */
@Component({
  selector: 'app-residents-section',
  standalone: true,
  imports: [PropertyDrawerComponent, PaginationComponent, DisabledReasonDirective, InviteResidentDialogComponent, ResidentChangeDialogComponent],
  template: `
    @if (loaded() && !propertiesLoaded()) {
      <p>Properties haven't been loaded for {{ name() }} yet.</p>
      <p class="muted">Residents can be invited once its properties are loaded.</p>
    } @else {
      <div class="rs__head">
        <p class="muted rs__helper">Every property in {{ name() }}, its residents and its resident invitations. Invitation links work for 72 hours. Times are in {{ name() }}' time zone. A property with no resident can also be claimed with the claim code mailed to its owner.</p>
        @if (canWrite() && loaded()) {
          <button type="button" class="btn btn--primary rs__invite" [attr.aria-label]="'Invite a resident to a property in ' + name()"
                  (click)="openInvite(null)">Invite a resident</button>
        }
      </div>
      @if (onboarding()) {
        <p class="muted">{{ name() }} is Onboarding. Resident invitations are saved as Scheduled and emailed when it is marked Active.</p>
      }

      <div class="rs__tools">
        <div class="rs__search">
          <label class="field-label" for="rs-search">Search residents</label>
          <input class="field" id="rs-search" type="search" #searchBox placeholder="Search by address, resident name or email"
                 maxlength="100" [value]="search()" (input)="onSearch($any($event.target).value)" />
        </div>
        <div class="rs__filters" role="group" aria-label="Filter properties">
          @for (f of filterButtons(); track f.key) {
            <button type="button" class="btn rs__filter" [class.rs__filter--on]="filter() === f.key"
                    [attr.aria-pressed]="filter() === f.key" (click)="setFilter(f.key)">{{ f.label }} · {{ f.count }}</button>
          }
        </div>
        @if (filter() === 'invitationPending') { <p class="field-help">Includes scheduled invitations.</p> }
      </div>

      <div class="rs__layout">
        <div class="rs__main">
          @switch (state()) {
            @case ('loading') {
              <p class="sr-only" role="status">Loading residents…</p>
              <div class="ph rs__ph" aria-hidden="true"></div>
            }
            @case ('error') {
              <div role="alert" class="rs__error">
                <p>Couldn't load residents.</p>
                <button type="button" class="btn" (click)="reload()">Try again</button>
              </div>
            }
            @default {
              @if (rows().length) {
                <div class="rs__table-wrap">
                  <table class="table rs__table">
                    <caption class="sr-only">Properties in {{ name() }}, with residents and invitations</caption>
                    <thead><tr><th scope="col">Property</th><th scope="col">Residents</th><th scope="col">Invitations</th></tr></thead>
                    <tbody>
                      @for (r of rows(); track r.propertyId) {
                        <tr class="rs__row" [class.rs__row--selected]="r.propertyId === selectedId()" (click)="openDrawer(r)">
                          <th scope="row">
                            <button type="button" class="link rs__address" [attr.data-property-id]="r.propertyId"
                                    [attr.aria-expanded]="r.propertyId === selectedId()"
                                    (click)="$event.stopPropagation(); openDrawer(r)">{{ r.address }}</button>
                            @if (!r.residentNames.length) { <span class="pill pill--warn rs__chip">No resident</span> }
                          </th>
                          <td>
                            {{ r.residentNames.join(', ') }}
                            @if (r.matchedEmail) { <span class="muted rs__matched">{{ r.matchedEmail }}</span> }
                          </td>
                          <td>
                            @for (part of parts(r); track part; let last = $last) {
                              <span class="pill rs__chip">{{ part }}</span>@if (!last) {<span aria-hidden="true"> · </span>}
                            } @empty { None }
                          </td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
                <app-pagination [totalItems]="total()" [limit]="limit" [offset]="offset()" noun="properties" (pageChange)="setOffset($event)" />
              } @else if (search()) {
                <p role="status">No properties match “{{ search() }}”.</p>
                <button type="button" class="btn" (click)="clearSearch()">Clear search</button>
              } @else if (filter() !== 'all') {
                <p role="status">{{ filterEmpty() }}</p>
              } @else {
                <p role="status">No properties.</p>
              }
            }
          }
        </div>

        @if (selected(); as sel) {
          <app-property-drawer [heading]="sel.address" [returnFocusTo]="addressButton(sel.propertyId)" (close)="closeDrawer()">
            @switch (drawerState()) {
              @case ('loading') { <p class="sr-only" role="status">Loading the property…</p><div class="ph rs__ph" aria-hidden="true"></div> }
              @case ('error') {
                <div role="alert">
                  <p>Couldn't load this property.</p>
                  <button type="button" class="btn" (click)="loadDrawer(sel.propertyId)">Try again</button>
                </div>
              }
              @default {
                @if (detail(); as d) {
                  @if (!d.residents.length) { <span class="pill pill--warn rs__chip">No resident</span> }
                  <h3 class="section-title rs__drawer-h" id="rs-drawer-residents">Residents</h3>
                  @if (d.residents.length) {
                    <ul class="rs__blocks" aria-labelledby="rs-drawer-residents">
                      @for (p of d.residents; track p.userId) {
                        <li class="rs__block">
                          <b>{{ p.name }}</b>
                          <span>{{ p.email }}</span>
                          <span class="muted">Resident since {{ since(p.residentSince) }}</span>
                          @if (canWrite()) {
                            <button type="button" class="btn rs__action" [attr.aria-label]="'Remove ' + p.name + ' from ' + d.address"
                                    (click)="openChange({ kind: 'remove', resident: p, property: d })">Remove</button>
                          }
                        </li>
                      }
                    </ul>
                  } @else {
                    <p class="muted">No residents yet.</p>
                  }

                  <h3 class="section-title rs__drawer-h" id="rs-drawer-invitations">Invitations</h3>
                  @if (invitations().length) {
                    <ul class="rs__blocks" aria-labelledby="rs-drawer-invitations">
                      @for (i of invitations(); track i.id) {
                        <li class="rs__block">
                          <span><b>{{ i.email }}</b> <span class="pill rs__chip">{{ statusLabel(i) }}</span></span>
                          <span class="muted">Sent by {{ i.sentBy || 'someone' }} · {{ sentTime(i) }}</span>
                          @if (statusDetail(i); as detail) { <span class="muted">{{ detail }}</span> }
                          @if (canWrite() && isOpen(i)) {
                            <span class="rs__actions">
                              @if (i.canResend || i.resendBlockedReason) {
                                <button type="button" class="btn rs__action" [attr.aria-label]="'Resend invitation to ' + i.email"
                                        [appDisabledReason]="resendReason(i)" (click)="resend(i)">Resend</button>
                              }
                              @if (i.canRevoke) {
                                <button type="button" class="btn rs__action" [attr.aria-label]="'Revoke invitation to ' + i.email"
                                        (click)="openChange({ kind: 'revoke', invitation: i, property: d })">Revoke</button>
                              }
                            </span>
                            @if (rateLimitedId() === i.id) { <span class="field-error" role="alert">{{ rateLimitedMessage }}</span> }
                          }
                        </li>
                      }
                    </ul>
                  } @else {
                    <p class="muted">No invitations.</p>
                  }

                  @if (canWrite() && d.canInvite) {
                    <button type="button" class="btn btn--primary rs__drawer-invite" [attr.aria-label]="'Invite a resident to ' + d.address"
                            (click)="openInvite({ propertyId: d.propertyId, address: d.address, residentCount: d.residents.length })">Invite resident</button>
                  }
                }
              }
            }
          </app-property-drawer>
        }
      </div>
    }

    @if (inviteFor(); as inv) {
      <app-invite-resident-dialog [communityId]="communityId()" [communityName]="name()" [onboarding]="onboarding()"
                                  [fixedProperty]="inv.fixed" (done)="onInvited($event)" (cancelled)="inviteFor.set(null)"
                                  (closeAfterRefusal)="onRefusalClosed()" />
    }
    @if (change(); as c) {
      <app-resident-change-dialog [communityId]="communityId()" [communityName]="name()" [onboarding]="onboarding()" [change]="c"
                                  (done)="onChanged($event)" (cancelled)="change.set(null)" (closeAfterRefusal)="onRefusalClosed()" />
    }
  `,
  styles: [`
    :host { display: flex; flex-direction: column; gap: 10px; }
    .rs__head { display: flex; flex-wrap: wrap; align-items: flex-start; gap: 10px; }
    .rs__helper { flex: 1 1 320px; }
    .rs__invite { margin-left: auto; }
    .rs__tools { display: flex; flex-direction: column; gap: 8px; }
    .rs__search { display: flex; flex-direction: column; gap: 4px; max-width: 420px; }
    .rs__filters { display: flex; flex-wrap: wrap; gap: 6px; }
    .rs__filter--on { background: var(--lav-2); border-color: var(--ink); font-weight: 600; }
    .rs__layout { display: flex; gap: 12px; align-items: flex-start; min-width: 0; }
    .rs__main { flex: 1; min-width: 0; display: flex; flex-direction: column; gap: 10px; }
    .rs__ph { height: 160px; }
    .rs__error { display: flex; flex-direction: column; gap: 8px; align-items: flex-start; }
    .rs__table-wrap { overflow-x: auto; position: relative; }
    .rs__row { cursor: pointer; }
    .rs__row--selected { background: var(--lav); }
    .rs__address { background: none; border: 0; padding: 0; font: inherit; text-align: left; }
    .rs__chip { font-size: 10.5px; margin-left: 6px; }
    .rs__matched { display: block; }
    .rs__drawer-h { margin-top: 12px; }
    .rs__blocks { list-style: none; display: flex; flex-direction: column; gap: 10px; }
    .rs__block { display: flex; flex-direction: column; gap: 2px; padding: 8px; border: 1px solid var(--line-soft); border-radius: 10px; background: var(--paper); }
    .rs__actions { display: flex; gap: 6px; margin-top: 4px; }
    .rs__action { min-height: 32px; }
    .rs__drawer-invite { margin-top: 12px; }
  `],
})
export class ResidentsSectionComponent implements OnDestroy {
  private ctx = inject(CommunitySettingsContext);
  private svc = inject(CommunitySettingsService);
  private toasts = inject(ToastService);
  private host = inject<ElementRef<HTMLElement>>(ElementRef);
  private cdr = inject(ChangeDetectorRef);

  readonly limit = LIMIT;
  readonly rateLimitedMessage = RATE_LIMITED_MESSAGE;

  readonly communityId = computed(() => this.ctx.communityId() ?? '');
  readonly name = computed(() => this.ctx.settings()?.communityName ?? '');
  readonly onboarding = computed(() => this.ctx.settings()?.status === 'Onboarding');
  readonly canWrite = computed(() => !this.ctx.readOnly() && this.ctx.settings()?.can.manageResidents === true);

  readonly search = signal('');
  readonly filter = signal<FilterKey>('all');
  readonly offset = signal(0);
  readonly state = signal<'loading' | 'ready' | 'error'>('loading');
  readonly page = signal<ResidentsPage | null>(null);

  readonly loaded = computed(() => this.page() !== null);
  readonly propertiesLoaded = computed(() => this.page()?.propertiesLoaded !== false);
  readonly rows = computed(() => this.page()?.items ?? []);
  readonly total = computed(() => this.page()?.total ?? 0);
  readonly filterEmpty = computed(() => {
    const f = this.filter();
    return f === 'all' ? '' : FILTER_EMPTY[f];
  });

  readonly filterButtons = computed<FilterButton[]>(() => {
    const c = this.page()?.counts ?? { all: 0, noResident: 0, invitationPending: 0, invitationExpired: 0 };
    return [
      { key: 'all', label: 'All', count: c.all },
      { key: 'noResident', label: 'No resident', count: c.noResident },
      { key: 'invitationPending', label: 'Invitation pending', count: c.invitationPending },
      { key: 'invitationExpired', label: 'Invitation expired', count: c.invitationExpired },
    ];
  });

  readonly selected = signal<{ propertyId: string; address: string } | null>(null);
  readonly selectedId = computed(() => this.selected()?.propertyId ?? null);
  readonly drawerState = signal<'loading' | 'ready' | 'error'>('loading');
  readonly detail = signal<ResidentProperty | null>(null);
  readonly invitations = computed(() => sortInvitations(this.detail()?.invitations ?? []));
  readonly rateLimitedId = signal<string | null>(null);

  readonly inviteFor = signal<{ fixed: FixedProperty | null } | null>(null);
  readonly change = signal<ResidentChange | null>(null);

  private searchTimer: ReturnType<typeof setTimeout> | null = null;
  private loadSeq = 0;
  private drawerSeq = 0;

  constructor() {
    effect(() => {
      const id = this.ctx.communityId();
      untracked(() => { if (id) void this.reload(); });
    });
  }

  ngOnDestroy(): void {
    if (this.searchTimer) clearTimeout(this.searchTimer);
  }

  parts(row: ResidentRow): string[] {
    return summaryParts(row);
  }

  since(date: string): string {
    return formatPlainDate(date);
  }

  statusLabel(i: PropertyInvitation): string {
    return invitationStatusLabel(i);
  }

  statusDetail(i: PropertyInvitation): string | null {
    return invitationStatusDetail(i, this.detail()?.timeZoneId, this.name());
  }

  sentTime(i: PropertyInvitation): string {
    return invitationTime(i.sentAt, this.detail()?.timeZoneId);
  }

  isOpen(i: PropertyInvitation): boolean {
    return isOpenInvitation(i);
  }

  /** brief 06 §4.5 item 14: the reason is the drawer's own line. */
  resendReason(i: PropertyInvitation): string | null {
    if (i.canResend) return null;
    return i.resendBlockedReason === 'COMMUNITY_NOT_ACTIVE'
      ? `Sent when ${this.name()} is marked Active`
      : refusalMessage(i.resendBlockedReason, { communityName: this.name(), audience: this.ctx.audience(), variant: 'resend' });
  }

  addressButton(propertyId: string): HTMLElement | null {
    return this.host.nativeElement.querySelector<HTMLElement>(`.rs__address[data-property-id="${propertyId}"]`);
  }

  onSearch(value: string): void {
    this.search.set(value);
    if (this.searchTimer) clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => {
      this.searchTimer = null;
      this.offset.set(0);
      void this.reload();
    }, SEARCH_DELAY_MS);
  }

  clearSearch(): void {
    this.search.set('');
    this.offset.set(0);
    void this.reload();
    this.host.nativeElement.querySelector<HTMLInputElement>('#rs-search')?.focus();
  }

  setFilter(key: FilterKey): void {
    if (this.filter() === key) return;
    this.filter.set(key);
    this.offset.set(0);
    void this.reload();
  }

  setOffset(offset: number): void {
    this.offset.set(offset);
    void this.reload();
  }

  async reload(): Promise<void> {
    const id = this.ctx.communityId();
    if (!id) return;
    const seq = ++this.loadSeq;
    if (!this.page()) this.state.set('loading');
    const filter = this.filter();
    try {
      const result = await this.svc.getResidents(id, {
        search: this.search().trim() || undefined,
        filter: filter === 'all' ? undefined : filter,
        limit: LIMIT,
        offset: this.offset(),
      });
      if (seq !== this.loadSeq) return;
      this.page.set(result);
      this.state.set('ready');
    } catch {
      if (seq === this.loadSeq) this.state.set('error');
    }
  }

  openDrawer(row: { propertyId: string; address: string }): void {
    this.selected.set({ propertyId: row.propertyId, address: row.address });
    this.rateLimitedId.set(null);
    void this.loadDrawer(row.propertyId);
  }

  closeDrawer(): void {
    this.selected.set(null);
    this.detail.set(null);
  }

  async loadDrawer(propertyId: string): Promise<void> {
    const id = this.ctx.communityId();
    if (!id) return;
    const seq = ++this.drawerSeq;
    if (this.detail()?.propertyId !== propertyId) {
      this.detail.set(null);
      this.drawerState.set('loading');
    }
    try {
      const result = await this.svc.getResidentProperty(id, propertyId);
      if (seq !== this.drawerSeq) return;
      this.detail.set(result);
      this.drawerState.set('ready');
    } catch {
      if (seq === this.drawerSeq) this.drawerState.set('error');
    }
  }

  openInvite(fixed: FixedProperty | null): void {
    this.inviteFor.set({ fixed });
  }

  openChange(c: ResidentChange): void {
    this.change.set(c);
  }

  /** Resend has no confirmation (brief 06 §4.5 item 11). Focus stays on the button. */
  async resend(i: PropertyInvitation): Promise<void> {
    const id = this.ctx.communityId();
    if (!id || !i.canResend) return;
    this.rateLimitedId.set(null);
    try {
      await this.svc.resendInvitation(id, i.id);
      this.toasts.show(`Invitation sent again to ${i.email}. The earlier link no longer works.`);
    } catch (err) {
      const code = refusalCodeOf(err);
      if (code === 'RATE_LIMITED') {
        this.rateLimitedId.set(i.id);
        return;
      }
      this.toasts.show(code === null
        ? "Couldn't resend the invitation. Check your connection and try again."
        : refusalMessage(code, { communityName: this.name(), audience: this.ctx.audience(), variant: 'resend', surface: 'action' }));
    }
    await this.refreshAfterChange();
  }

  onInvited(done: InviteDone): void {
    this.inviteFor.set(null);
    this.afterDialog(done.message, done.propertyId);
  }

  onChanged(done: ResidentChangeDone): void {
    this.change.set(null);
    this.afterDialog(done.message, done.propertyId);
  }

  /** A refusal like FORBIDDEN: the page is read again, which may move the person to their landing. */
  onRefusalClosed(): void {
    this.inviteFor.set(null);
    this.change.set(null);
    void this.ctx.reload();
  }

  /**
   * brief 06 §6: after a dialog's change, the toast shows and focus moves to the property's address.
   * The dialog goes first, so its own focus return (to its opener) runs before this one.
   */
  private afterDialog(message: string, propertyId: string): void {
    this.cdr.detectChanges();
    this.toasts.show(message);
    setTimeout(() => this.addressButton(propertyId)?.focus());
    void this.refreshAfterChange();
  }

  /** Re-reads the list and the open drawer. */
  private async refreshAfterChange(): Promise<void> {
    const open = this.selectedId();
    await Promise.all([this.reload(), open ? this.loadDrawer(open) : Promise.resolve()]);
  }
}

