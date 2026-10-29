import { Component, OnDestroy, OnInit, computed, effect, inject, signal, viewChild, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { ArcCounts, ArcListItem, ArcVoteChoice, ArchitecturalService } from '../../../core/services/architectural.service';
import { ApplicationDetailPanelComponent } from './application-detail-panel.component';
import { TallyComponent } from './tally.component';
import { arcContext } from './arc-context';
import { FORMAL_DENIAL_NOTE, decisionReachedLabel, fmtDate, fmtDateTime, myVoteLabel, outcomeLabel } from './arc-format';
import { VoteChoiceComponent } from './vote-choice.component';
import { FinalMarkComponent } from './final-mark.component';

// 027 US1/US2 — wireframe `BoardArchApps`: "Architectural applications" with the "N awaiting your vote" pill,
// Open/Closed tabs with counts, "Search address or owner", and the FR-002 table (ID · Property · Project ·
// Attachments · Due · Board votes · Your vote) with inline Approve / Revisions needed / Deny / Info.
// 031: rows open the application on click; votes are picked then submitted; "Your vote" is followed by a
// Final decision column holding the undo link (while the vote can change) or the final ✓ / ✕ / ↻ mark.
@Component({
  selector: 'app-arc-applications-page',
  standalone: true,
  imports: [FormsModule, TallyComponent, ApplicationDetailPanelComponent, VoteChoiceComponent, FinalMarkComponent],
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
        @if (status() === 'closed') {
          <label class="ap__withdrawn">
            <input type="checkbox" [ngModel]="includeWithdrawn()" (ngModelChange)="setIncludeWithdrawn($event)" name="includeWithdrawn" />
            Show withdrawn
          </label>
        }
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
            <th style="width:104px;">Attachments</th><th style="width:74px;">Due</th>
            <th style="width:150px;">Board votes</th><th style="width:300px;">Your vote</th>
            <th style="width:92px;">Final decision</th>
          </tr></thead>
          <tbody>
            @if (!loading() && items().length === 0) {
              <tr><td colspan="8" class="muted ap__empty">
                @if (search()) { No applications match “{{ search() }}”. }
                @else { No {{ status() }} applications. }
              </td></tr>
            }
            @for (a of items(); track a.id) {
              <!-- 031 item 1: the whole row opens the application; buttons inside keep their own action. -->
              <tr class="ap__row" [attr.data-app-id]="a.id" [class.ap__row--selected]="a.id === selectedId()"
                  (click)="rowClick($event, a.id)">
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
                <td>
                  <app-arc-tally [tally]="a.tally" />
                  @if (a.decision) {
                    <div><span class="pill ap__decision" [class.pill--ok]="a.decision.outcome === 'Approved'">
                      {{ a.status === 'Closed' ? outcome(a) : decided(a) }}
                    </span></div>
                  }
                </td>
                <td>
                  @if (a.myVote.state === 'CanVote') {
                    <div class="ap__actions">
                      <app-arc-vote-choice [busy]="busyId() === a.id" (submitted)="vote(a, $event)" />
                      <button type="button" class="btn btn--ghost ap__btn" (click)="ask(a.id)">Ask question</button>
                    </div>
                  } @else if (a.myVote.state === 'Voted') {
                    <span class="pill">{{ voteText(a.myVote.choice) }}</span>
                    @if (a.myVote.choice === 'RevisionsNeeded' && notedId() === a.id) { <div class="muted ap__note">{{ formalNote }}</div> }
                  } @else if (a.myVote.state === 'Recused') {
                    <span class="pill" title="You own this property">recused</span>
                  }
                </td>
                <td class="ap__final">
                  @if (a.canUndoVote) {
                    <!-- 031 item 5: a vote can be undone (then recast) until the decision is final. -->
                    <button type="button" class="link ap__undo" [disabled]="busyId() === a.id" (click)="undo(a)"
                            [attr.aria-label]="'Undo your vote on ' + a.displayId">undo</button>
                    @if (a.votesLockAt) { <div class="muted ap__lock">until {{ dateTime(a.votesLockAt) }}</div> }
                  } @else if (a.decision && (a.decisionFinal || a.status === 'Closed')) {
                    <app-arc-final-mark [decision]="a.decision" />
                  } @else if (a.decision) {
                    <span class="muted ap__lock" [attr.title]="'Votes can change until ' + dateTime(a.votesLockAt)">pending</span>
                  }
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>

      @if (selectedId(); as id) {
        <app-arc-detail-panel #panel id="arc-detail" [communityId]="ctx.communityId()!" [applicationId]="id" [manager]="ctx.isManager()"
                              [startWithInfo]="infoFor() === id"
                              (changed)="changed($event)" (openRevision)="select($event)" />
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
    .ap__actions { display: flex; gap: 5px; flex-wrap: wrap; align-items: center; }
    .ap__btn { padding: 4px 9px; font-size: 11px; }
    .ap__row { cursor: pointer; }
    .ap__row:focus-within td { background: var(--pink); }
    .ap__decision { font-size: 10px; margin-top: 4px; }
    .ap__final { text-align: center; }
    .ap__undo { background: none; border: none; padding: 0; font: inherit; font-size: 12px; cursor: pointer; }
    .ap__lock { font-size: 10px; }
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
  readonly includeWithdrawn = signal(false);
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
  readonly dateTime = fmtDateTime;
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

  /** 029: resident-withdrawn applications are hidden unless the board asks for them. */
  setIncludeWithdrawn(value: boolean): void {
    this.includeWithdrawn.set(value);
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
    this.scrollToPanel();
  }

  /** 031 item 1: a click anywhere on the row's whitespace opens it; controls inside the row keep their own action. */
  rowClick(event: MouseEvent, id: string): void {
    const target = event.target as HTMLElement | null;
    if (target?.closest('button, a, input, label, textarea, select, [role="group"]')) return;
    this.select(id);
  }

  /** US4-S1 / 031 item 11: "Ask question" opens the panel with the comment box focused. */
  ask(id: string): void {
    const alreadyOpen = this.selectedId() === id && this.infoFor() === id;
    this.infoFor.set(id);
    this.selectedId.set(id);
    if (alreadyOpen) this.panel()?.focusForInfo();
    this.scrollToPanel();
  }

  private scrollToPanel(): void {
    setTimeout(() => {
      const panel = document.getElementById('arc-detail');
      if (panel && typeof panel.scrollIntoView === 'function') panel.scrollIntoView({ behavior: 'smooth', block: 'start' });
    });
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
      await this.refreshCounts();
      if (this.selectedId() === a.id) void this.panel()?.reload();
    } catch (e: any) {
      // Reload first (the row's state may have changed), then show why the vote was refused.
      await this.reload();
      this.error.set(e?.error?.message ?? 'Your vote could not be saved.');
    } finally {
      this.busyId.set(null);
    }
  }

  /** 031 item 5: undo my vote so I can recast it, while the decision isn't final. */
  async undo(a: ArcListItem): Promise<void> {
    const cid = this.ctx.communityId();
    if (!cid || this.busyId()) return;
    this.busyId.set(a.id);
    this.error.set(null);
    try {
      this.replace(await this.arc.undoVote(cid, a.id));
      if (this.notedId() === a.id) this.notedId.set(null);
      await this.refreshCounts();
      if (this.selectedId() === a.id) void this.panel()?.reload();
    } catch (e: any) {
      await this.reload();
      this.error.set(e?.error?.message ?? 'Your vote could not be undone.');
    } finally {
      this.busyId.set(null);
    }
  }

  /** The panel changed the application (vote, undo, question, outcome): update the row and the counts. */
  changed(row: ArcListItem): void {
    this.replace(row);
    void this.refreshCounts();
  }

  /**
   * 031 item 7: the header pill and tab counts always come from the server after a change — a vote cast from
   * the detail panel used to leave "1 awaiting your vote" stale.
   */
  private async refreshCounts(): Promise<void> {
    const cid = this.ctx.communityId();
    if (!cid) return;
    try {
      const page = await this.arc.list(cid, { status: 'open', awaitingMyVote: true, limit: 1 });
      this.counts.set(page.counts);
    } catch {
      // Counts are advisory; the next reload corrects them.
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
      const page = await this.arc.list(cid, {
        status: this.status(), search: this.search().trim() || undefined,
        includeWithdrawn: this.status() === 'closed' && this.includeWithdrawn() ? true : undefined,
      });
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
