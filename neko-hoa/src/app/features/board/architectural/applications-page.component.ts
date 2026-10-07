import { Component, OnDestroy, OnInit, computed, effect, inject, signal, viewChild, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { ArcCounts, ArcListItem, ArcVoteChoice, ArchitecturalService } from '../../../core/services/architectural.service';
import { ApplicationDetailPanelComponent } from './application-detail-panel.component';
import { TallyComponent } from './tally.component';
import { arcContext } from './arc-context';
import { FORMAL_DENIAL_NOTE, decisionReachedLabel, fmtDate, myVoteLabel, outcomeLabel } from './arc-format';

// 027 US1/US2 — wireframe `BoardArchApps`: "Architectural applications" with the "N awaiting your vote" pill,
// Open/Closed tabs with counts, "Search address or owner", and the FR-002 table (ID · Property · Project ·
// Attachments · Due · Board votes · Your vote) with inline Approve / Revisions needed / Deny / Info.
@Component({
  selector: 'app-arc-applications-page',
  standalone: true,
  imports: [FormsModule, TallyComponent, ApplicationDetailPanelComponent],
  template: `
    <div class="page-header ap__header">
      <h1 class="page-title">Architectural <span class="hand">applications</span></h1>
      @if (counts().awaitingMyVote > 0) {
        <span class="pill pill--warn ap__pill">{{ counts().awaitingMyVote }} awaiting your vote</span>
      }
    </div>

    @if (!ctx.communityId()) {
      <div class="card"><p class="muted">No active community selected.</p></div>
    } @else {
      <div class="ap__toolbar">
        <div class="tab-bar" role="tablist" aria-label="Application status">
          <button type="button" role="tab" class="tab" [class.tab--active]="status() === 'open'"
                  [attr.aria-selected]="status() === 'open'" (click)="setStatus('open')">Open · {{ counts().open }}</button>
          <button type="button" role="tab" class="tab" [class.tab--active]="status() === 'closed'"
                  [attr.aria-selected]="status() === 'closed'" (click)="setStatus('closed')">Closed · {{ counts().closed }}</button>
        </div>
        <label class="ap__search">
          <span class="sr-only">Search address or owner</span>
          <input class="input" type="search" placeholder="🔍 Search address or owner" aria-label="Search address or owner"
                 [ngModel]="search()" (ngModelChange)="onSearch($event)" name="search" />
        </label>
      </div>

      @if (error()) { <p class="ap__error" role="alert">{{ error() }}</p> }

      <div class="card" style="padding:0;overflow:hidden;">
        <table class="data-table ap__table">
          <thead><tr>
            <th style="width:96px;">ID</th><th>Property</th><th>Project</th>
            <th style="width:112px;">Attachments</th><th style="width:78px;">Due</th>
            <th style="width:132px;">Board votes</th><th style="width:230px;">Your vote</th>
          </tr></thead>
          <tbody>
            @if (!loading() && items().length === 0) {
              <tr><td colspan="7" class="muted ap__empty">
                @if (search()) { No applications match “{{ search() }}”. }
                @else { No {{ status() }} applications. }
              </td></tr>
            }
            @for (a of items(); track a.id) {
              <tr [attr.data-app-id]="a.id" [class.ap__row--selected]="a.id === selectedId()">
                <td class="mono ap__id">
                  <button type="button" class="link ap__open" (click)="select(a.id)">{{ a.displayId }}</button>
                  @if (a.revision > 1) { <span class="pill pill--rose ap__badge" [attr.aria-label]="'version ' + a.revision">v{{ a.revision }}</span> }
                </td>
                <td><div class="ap__addr">{{ a.propertyAddress }}</div><div class="muted ap__owner">{{ a.ownerName }}</div></td>
                <td>
                  {{ a.projectTitle }}
                  @if (a.overdue) { <span class="pill pill--warn ap__tag">overdue</span> }
                  @if (a.infoRequested) { <span class="pill ap__tag">info requested</span> }
                </td>
                <td>
                  @if (a.attachmentCount > 0) {
                    <button type="button" class="link ap__files" (click)="select(a.id)">📎 {{ a.attachmentCount }} {{ a.attachmentCount === 1 ? 'file' : 'files' }}</button>
                  } @else { <span class="muted">none</span> }
                </td>
                <td class="mono">{{ date(a.dueDate) }}</td>
                <td><app-arc-tally [tally]="a.tally" /></td>
                <td>
                  @if (a.decision) {
                    <span class="pill" [class.pill--ok]="a.decision.outcome === 'Approved'">
                      {{ a.status === 'Closed' ? outcome(a) : decided(a) }}
                    </span>
                  } @else if (a.myVote.state === 'CanVote') {
                    <div class="ap__actions">
                      <button type="button" class="btn ap__btn ap__approve" [disabled]="busyId() === a.id" (click)="vote(a, 'Approve')">Approve</button>
                      <button type="button" class="btn ap__btn" [disabled]="busyId() === a.id" (click)="vote(a, 'RevisionsNeeded')">Revisions needed</button>
                      <button type="button" class="btn ap__btn" [disabled]="busyId() === a.id" (click)="vote(a, 'Deny')">Deny</button>
                      <button type="button" class="btn btn--ghost ap__btn" (click)="info(a.id)">Info</button>
                    </div>
                  } @else if (a.myVote.state === 'Voted') {
                    <span class="pill">{{ voteText(a.myVote.choice) }}</span>
                    @if (a.myVote.choice === 'RevisionsNeeded' && notedId() === a.id) { <div class="muted ap__note">{{ formalNote }}</div> }
                  } @else if (a.myVote.state === 'Recused') {
                    <span class="pill" title="You own this property">recused</span>
                  }
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>

      @if (selectedId(); as id) {
        <app-arc-detail-panel #panel [communityId]="ctx.communityId()!" [applicationId]="id" [manager]="ctx.isManager()"
                              [startWithInfo]="infoFor() === id"
                              (changed)="replace($event)" (openRevision)="select($event)" />
      }
    }
  `,
  styles: [`
    .ap__header { display: flex; align-items: baseline; gap: 12px; }
    .ap__pill { margin-left: auto; }
    .ap__toolbar { display: flex; gap: 8px; align-items: center; margin-bottom: 10px; }
    .ap__search { margin-left: auto; width: 220px; }
    .ap__search .input { width: 100%; }
    .ap__id { font-size: 11px; white-space: nowrap; }
    .ap__open, .ap__files { background: none; border: none; padding: 0; font: inherit; cursor: pointer; }
    .ap__badge, .ap__tag { font-size: 10px; margin-left: 4px; }
    .ap__addr { font-weight: 500; }
    .ap__owner { font-size: 10.5px; }
    .ap__actions { display: flex; gap: 5px; flex-wrap: wrap; }
    .ap__btn { padding: 4px 9px; font-size: 11px; }
    .ap__approve { background: var(--ok-bg); }
    .ap__note { font-size: 10.5px; margin-top: 4px; }
    .ap__empty { text-align: center; padding: 24px; }
    .ap__error { color: var(--warn); font-size: 12px; }
    .ap__row--selected { outline: 2px solid var(--violet); outline-offset: -2px; }
  `]
})
export class ApplicationsPageComponent implements OnInit, OnDestroy {
  private arc = inject(ArchitecturalService);
  private route = inject(ActivatedRoute);
  readonly ctx = arcContext();

  readonly status = signal<'open' | 'closed'>('open');
  readonly search = signal('');
  readonly items = signal<ArcListItem[]>([]);
  readonly counts = signal<ArcCounts>({ open: 0, closed: 0, awaitingMyVote: 0 });
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly selectedId = signal<string | null>(null);
  readonly busyId = signal<string | null>(null);
  readonly notedId = signal<string | null>(null);
  readonly infoFor = signal<string | null>(null);
  readonly formalNote = FORMAL_DENIAL_NOTE;
  private readonly panel = viewChild<ApplicationDetailPanelComponent>('panel');
  private searchTimer: ReturnType<typeof setTimeout> | null = null;

  readonly date = fmtDate;
  readonly voteText = myVoteLabel;
  readonly decided = (a: ArcListItem) => decisionReachedLabel(a.decision!);
  readonly outcome = (a: ArcListItem) => outcomeLabel(a.decision!);

  constructor() {
    // Edge case "board member of several communities": reload whenever the active community changes.
    effect(() => {
      if (this.ctx.communityId()) untracked(() => void this.reload());
    }, { allowSignalWrites: true });
  }

  ngOnInit(): void {
    const open = this.route.snapshot.queryParamMap.get('open');
    if (open) this.selectedId.set(open);
  }

  ngOnDestroy(): void {
    if (this.searchTimer) clearTimeout(this.searchTimer);
  }

  setStatus(status: 'open' | 'closed'): void {
    this.status.set(status);
    void this.reload();
  }

  onSearch(value: string): void {
    this.search.set(value);
    if (this.searchTimer) clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => void this.reload(), 300);
  }

  select(id: string): void {
    this.infoFor.set(null);
    this.selectedId.set(id);
  }

  /** US4-S1: Info opens the panel with the comment box focused. */
  info(id: string): void {
    const alreadyOpen = this.selectedId() === id && this.infoFor() === id;
    this.infoFor.set(id);
    this.selectedId.set(id);
    if (alreadyOpen) this.panel()?.focusForInfo();
  }

  async vote(a: ArcListItem, choice: ArcVoteChoice): Promise<void> {
    const cid = this.ctx.communityId();
    if (!cid || this.busyId()) return;
    this.busyId.set(a.id);
    this.error.set(null);
    try {
      const row = await this.arc.vote(cid, a.id, choice);
      this.replace(row);
      if (choice === 'RevisionsNeeded') this.notedId.set(a.id);
      this.counts.update(c => ({ ...c, awaitingMyVote: Math.max(0, c.awaitingMyVote - 1) }));
    } catch (e: any) {
      // Reload first (the row's state may have changed), then show why the vote was refused.
      await this.reload();
      this.error.set(e?.error?.message ?? 'Your vote could not be saved.');
    } finally {
      this.busyId.set(null);
    }
  }

  replace(row: ArcListItem): void {
    this.items.update(list => list.map(i => (i.id === row.id ? { ...i, ...row } : i)));
  }

  async reload(): Promise<void> {
    const cid = this.ctx.communityId();
    if (!cid) return;
    this.loading.set(true);
    try {
      const page = await this.arc.list(cid, { status: this.status(), search: this.search().trim() || undefined });
      this.items.set(page.items);
      this.counts.set(page.counts);
      this.error.set(null);
    } catch {
      this.error.set('Applications could not be loaded.');
    } finally {
      this.loading.set(false);
    }
  }
}
