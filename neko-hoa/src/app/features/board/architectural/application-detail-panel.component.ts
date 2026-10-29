import { Component, effect, inject, input, output, signal, viewChild, untracked } from '@angular/core';
import { ArcDetail, ArcListItem, ArcOutcomeRequest, ArchitecturalService } from '../../../core/services/architectural.service';
import { CastVoteCardComponent, ArcVoteEvent } from './cast-vote-card.component';
import { RecordOutcomeComponent } from './record-outcome.component';
import { TallyComponent } from './tally.component';
import {
  FORMAL_DENIAL_NOTE, INFO_DUE_NOTICE, decisionReachedLabel, finalResultLabel, fmtDate, fmtDateTime, fmtSize, myVoteLabel,
  outcomeLabel, shortProject, votedLabel,
} from './arc-format';
import { FinalMarkComponent } from './final-mark.component';

// 027 US3 — wireframe `BoardArchApps` lower cards: application details with attachments that open in a new
// tab through a short-lived link fetched on click (no storage URL is ever rendered — FR-012), the board's
// votes and comments, info requests, earlier revisions, the "Cast your vote" card and, for managers,
// recording the outcome.
@Component({
  selector: 'app-arc-detail-panel',
  standalone: true,
  imports: [CastVoteCardComponent, RecordOutcomeComponent, TallyComponent, FinalMarkComponent],
  template: `
    @if (loading() && !detail()) {
      <!-- 031 item 9: skeletons stand in for the details and the vote card while the application loads. -->
      <div class="grid-2 adp" aria-busy="true" aria-label="Loading application">
        <div class="card adp__skeleton" data-testid="detail-skeleton">
          <span class="skeleton skeleton--title"></span>
          <span class="skeleton skeleton--mid"></span><span class="skeleton skeleton--mid"></span>
          <span class="skeleton skeleton--short"></span>
          <span class="skeleton skeleton--block"></span>
          <span class="skeleton"></span><span class="skeleton skeleton--mid"></span>
        </div>
        <div class="card adp__skeleton" data-testid="vote-skeleton">
          <span class="skeleton skeleton--title"></span>
          <span class="skeleton skeleton--block"></span>
          <span class="skeleton skeleton--mid"></span>
          <span class="skeleton skeleton--short"></span>
        </div>
      </div>
    } @else {
    @if (detail(); as d) {
      <div class="grid-2 adp">
        <div class="card">
          <h2 class="section-title adp__title">
            {{ d.displayId }} · {{ short(d.projectTitle) }}
            @if (d.revision > 1) { <span class="pill pill--rose adp__badge" [attr.aria-label]="'version ' + d.revision">v{{ d.revision }}</span> }
          </h2>
          <div class="grid-2" style="gap:10px;margin-bottom:12px;">
            <div><div class="field-label">Owner</div><b>{{ d.ownerName }}</b></div>
            <div><div class="field-label">Received</div><b>{{ date(d.receivedDate) }}</b></div>
            <div><div class="field-label">Property</div>{{ d.propertyAddress }}</div>
            <div><div class="field-label">Due</div>{{ date(d.dueDate) }} @if (d.overdue) { <span class="pill pill--warn">overdue</span> }</div>
          </div>

          <div class="field-label">Attachments <span class="muted adp__hint">· opens in a new tab</span></div>
          @if (submitted(d).length === 0) {
            <p class="muted">No attachments.</p>
          } @else {
            <ul class="adp__files">
              @for (f of submitted(d); track f.id) {
                <li>
                  <button type="button" class="adp__file" (click)="openAttachment(f.id)"
                          [attr.aria-label]="'Open ' + f.fileName + ' in a new tab'">
                    <span aria-hidden="true">📄</span>
                    <span class="link adp__name">{{ f.fileName }}</span>
                    <span class="muted adp__size">{{ size(f.sizeBytes) }}</span>
                    <span aria-hidden="true">↗</span>
                  </button>
                  @if (unavailable().has(f.id)) { <span class="adp__error" role="alert">Attachment unavailable</span> }
                </li>
              }
            </ul>
          }

          <div class="field-label" style="margin-top:12px;">Board votes</div>
          <app-arc-tally [tally]="d.tally" />
          @if (d.decision && d.status !== 'Closed') {
            <p class="adp__decision"><b>{{ decided(d) }}</b></p>
          }
          @if (d.ownerReason) {
            <p class="adp__reason"><span class="field-label">Reason</span> {{ d.ownerReason }}</p>
          }
          @if (d.conditionsOfApproval) {
            <p class="adp__reason"><span class="field-label">Conditions</span> {{ d.conditionsOfApproval }}</p>
          }
          @if (d.votes.length) {
            <ul class="adp__votes">
              @for (v of d.votes; track $index) {
                <li><b>{{ v.voterName }}</b> — {{ votedText(v.choice) }}@if (v.comment) {: <span class="adp__comment">{{ v.comment }}</span>}</li>
              }
            </ul>
          }
          <!-- 031 item 10 -->
          <p class="adp__final">
            <span class="field-label">Board final result:</span>
            <app-arc-final-mark [decision]="isFinal(d) ? d.decision : null" />
            <b>{{ finalResult(d) }}</b>
          </p>
          @if (d.decision && !isFinal(d) && d.votesLockAt) {
            <p class="muted adp__pending">Pending — board members can change their votes until {{ dateTime(d.votesLockAt) }}.</p>
          }

          @for (r of d.infoRequests; track r.id) {
            <div class="adp__info" [class.adp__info--open]="!r.respondedAt">
              <span class="pill pill--warn">info requested</span>
              <span>{{ r.message }}</span>
              <span class="muted">— {{ r.requestedBy }}, {{ date(r.requestedAt) }}</span>
              <span class="muted adp__due">{{ infoDueNotice }}</span>
              @if (r.responseMessage) {
                <div class="adp__reply">Owner replied ({{ date(r.respondedAt!) }}): {{ r.responseMessage }}</div>
              }
            </div>
          }

          @if (d.revisions.length > 1) {
            <div class="field-label" style="margin-top:12px;">Versions</div>
            <ul class="adp__revisions">
              @for (r of d.revisions; track r.id) {
                <li>
                  @if (r.id === d.id) { <b>v{{ r.revision }}</b> (this version) }
                  @else {
                    <button type="button" class="link adp__rev" (click)="openRevision.emit(r.id)">v{{ r.revision }}</button>
                    · received {{ date(r.receivedDate) }}@if (r.decision) { · {{ outcome({ decision: r.decision }) }} }
                  }
                </li>
              }
            </ul>
          }
        </div>

        <div>
          @if (d.lapseStatement) {
            <!-- 031 item 12: decided by the lapse rule — explain why, citing the governing documents. -->
            <div class="card adp__lapse" role="note">
              <div class="section-title" style="margin:0 0 8px;">
                <app-arc-final-mark [decision]="d.decision" />
                {{ d.decision?.outcome === 'Approved' ? 'Approved by default' : 'Denied by default' }}
              </div>
              <p class="adp__lapse-text">{{ d.lapseStatement }}</p>
            </div>
          } @else if (d.myVote.state === 'CanVote') {
            <app-arc-cast-vote-card #voteCard [dueDate]="d.dueDate" [ruleText]="d.ruleText" [busy]="busy()"
                                    (voted)="vote($event)" (infoRequested)="requestInfo($event)" />
          } @else {
            <div class="card">
              @if (d.myVote.state === 'Voted') {
                <p class="adp__mine">
                  <span class="pill">{{ voteText(d.myVote.choice) }}</span>
                  @if (d.canUndoVote) {
                    <button type="button" class="link adp__undo" [disabled]="busy()" (click)="undo()">undo</button>
                  }
                </p>
                @if (d.canUndoVote && d.votesLockAt) {
                  <p class="muted adp__note">You can undo and recast your vote until {{ dateTime(d.votesLockAt) }}.</p>
                }
              }
              @if (d.myVote.state === 'Voted' && d.myVote.choice === 'RevisionsNeeded') { <p class="muted adp__note">{{ formalNote }}</p> }
              @if (d.myVote.state === 'Recused') { <p class="muted">You own this property, so you are recused.</p> }
              <p class="muted adp__rule">{{ d.ruleText }}</p>
            </div>
          }
          @if (manager() && (d.status === 'DecisionReached' || d.status === 'Closed')) {
            <app-arc-record-outcome [detail]="d" [busy]="busy()" (recorded)="recordOutcome($event)" (resend)="resendEmail()" />
          }
          @if (error()) { <p class="adp__error" role="alert">{{ error() }}</p> }
        </div>
      </div>

      <!-- 031 item 15: the homeowner's comments, in their own section under the details. -->
      <section class="card adp__comments" aria-labelledby="owner-comments-title">
        <h3 class="section-title" id="owner-comments-title" style="margin:0 0 8px;">Homeowner comments</h3>
        @if (!d.ownerComments?.length) {
          <p class="muted">The homeowner hasn't commented on this application.</p>
        } @else {
          <ul class="adp__thread">
            @for (c of d.ownerComments; track c.id) {
              <li class="adp__entry">
                <div class="adp__meta"><b>{{ c.authorName }}</b> <span class="muted">· {{ dateTime(c.createdAt) }}</span></div>
                @if (c.body) { <p class="adp__comment adp__body">{{ c.body }}</p> }
                @for (f of c.attachments; track f.id) {
                  <button type="button" class="adp__file" (click)="openAttachment(f.id)"
                          [attr.aria-label]="'Open ' + f.fileName + ' in a new tab'">
                    <span aria-hidden="true">📄</span>
                    <span class="link adp__name">{{ f.fileName }}</span>
                    <span class="muted adp__size">{{ size(f.sizeBytes) }}</span>
                    <span aria-hidden="true">↗</span>
                  </button>
                  @if (unavailable().has(f.id)) { <span class="adp__error" role="alert">Attachment unavailable</span> }
                }
              </li>
            }
          </ul>
        }
      </section>
    } @else if (error()) {
      <p class="adp__error" role="alert">{{ error() }}</p>
    }
    }
  `,
  styles: [`
    .adp { margin-top: 14px; }
    .adp__title { margin: 0 0 10px; display: flex; gap: 8px; align-items: center; }
    .adp__badge { font-size: 10.5px; }
    .adp__hint { text-transform: none; letter-spacing: 0; font-weight: 400; }
    .adp__files, .adp__votes, .adp__revisions { list-style: none; padding: 0; margin: 6px 0 0; display: flex; flex-direction: column; gap: 6px; }
    .adp__file { display: flex; align-items: center; gap: 10px; width: 100%; padding: 8px 11px; border: 1.5px solid var(--line);
                 border-radius: 10px; background: var(--paper); font: inherit; cursor: pointer; text-align: left; }
    .adp__name { flex: 1; font-size: 12px; }
    .adp__size { font-size: 10.5px; }
    .adp__decision { margin: 8px 0; }
    .adp__comment { white-space: pre-wrap; }
    .adp__reason { margin: 4px 0 0; font-size: 13px; }
    .adp__info { display: flex; gap: 8px; align-items: baseline; flex-wrap: wrap; margin-top: 8px; font-size: 12px; }
    .adp__rev { background: none; border: none; padding: 0; font: inherit; cursor: pointer; }
    .adp__error { color: var(--warn); font-size: 12px; }
    .adp__note, .adp__rule { font-size: 11px; }
    .adp__skeleton { min-height: 180px; }
    .adp__final { display: flex; align-items: center; gap: 8px; margin: 10px 0 0; font-size: 13px; }
    .adp__final .field-label { margin: 0; }
    .adp__pending { font-size: 11px; margin: 4px 0 0; }
    .adp__mine { display: flex; align-items: center; gap: 10px; }
    .adp__undo { background: none; border: none; padding: 0; font: inherit; font-size: 12px; cursor: pointer; }
    .adp__lapse { background: var(--ok-bg); }
    .adp__lapse .section-title { display: flex; align-items: center; gap: 8px; }
    .adp__lapse-text { font-size: 12.5px; line-height: 1.55; margin: 0; }
    .adp__comments { margin-top: 14px; }
    .adp__thread { list-style: none; padding: 0; margin: 0; display: flex; flex-direction: column; gap: 10px; }
    .adp__entry { border-left: 3px solid var(--lav-2); padding: 4px 0 4px 10px; display: flex; flex-direction: column; gap: 6px; }
    .adp__meta { font-size: 12px; }
    .adp__body { margin: 0; font-size: 12.5px; }
  `]
})
export class ApplicationDetailPanelComponent {
  private arc = inject(ArchitecturalService);

  readonly communityId = input.required<string>();
  readonly applicationId = input.required<string>();
  readonly manager = input<boolean>(false);
  /** US4-S1: open with the comment box focused for a Request info (set when Info is clicked on a row). */
  readonly startWithInfo = input<boolean>(false);
  /** Emits the refreshed row after any change so the list can update in place. */
  readonly changed = output<ArcListItem>();
  readonly openRevision = output<string>();

  readonly detail = signal<ArcDetail | null>(null);
  // Starts true so the very first frame already shows the skeletons (031 item 9).
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly unavailable = signal(new Set<string>());
  readonly formalNote = FORMAL_DENIAL_NOTE;
  readonly infoDueNotice = INFO_DUE_NOTICE;
  private readonly voteCard = viewChild<CastVoteCardComponent>('voteCard');
  private pendingInfoFocus = false;

  readonly date = fmtDate;
  readonly size = fmtSize;
  readonly short = shortProject;
  readonly voteText = myVoteLabel;
  readonly votedText = votedLabel;
  readonly dateTime = fmtDateTime;
  /** Files the owner submitted or added with a reply; comment files are listed with their comment. */
  readonly submitted = (d: ArcDetail) => d.attachments.filter(f => !f.ownerCommentId);
  readonly isFinal = (d: ArcDetail) => !!d.decision && (d.status === 'Closed' || !!d.decisionFinal);
  readonly finalResult = (d: ArcDetail) => finalResultLabel(this.isFinal(d) ? d.decision : null);
  readonly decided = (d: ArcDetail) => decisionReachedLabel(d.decision!);
  readonly outcome = (d: { decision: ArcDetail['decision'] }) => outcomeLabel(d.decision!);

  constructor() {
    effect(() => {
      const cid = this.communityId();
      const id = this.applicationId();
      untracked(() => void this.load(cid, id));
    });
    // The vote card only exists once the detail has loaded, so focus is applied when it appears.
    effect(() => {
      const card = this.voteCard();
      const wantsInfo = this.startWithInfo();
      if (card && (wantsInfo || this.pendingInfoFocus)) {
        this.pendingInfoFocus = false;
        queueMicrotask(() => card.focusForInfo());
      }
    });
  }

  /** US4-S1: open with the comment box focused for a Request info. */
  focusForInfo(): void {
    const card = this.voteCard();
    if (card) card.focusForInfo();
    else this.pendingInfoFocus = true;
  }

  private async load(communityId: string, applicationId: string): Promise<void> {
    this.error.set(null);
    // 031 item 9: switching applications blanks the panel to skeletons at once; a refresh of the same one doesn't.
    if (this.detail()?.id !== applicationId) {
      this.detail.set(null);
      this.loading.set(true);
    }
    try {
      const d = await this.arc.detail(communityId, applicationId);
      if (applicationId === this.applicationId()) this.detail.set(d);
    } catch {
      this.detail.set(null);
      this.error.set('This application could not be loaded.');
    } finally {
      if (applicationId === this.applicationId()) this.loading.set(false);
    }
  }

  /** Re-reads the application, e.g. after the list voted or undid a vote on it from its row. */
  reload(): Promise<void> {
    return this.refresh();
  }

  private async refresh(): Promise<void> {
    await this.load(this.communityId(), this.applicationId());
    const d = this.detail();
    if (d) this.changed.emit(d);
  }

  async openAttachment(attachmentId: string): Promise<void> {
    try {
      const link = await this.arc.attachmentUrl(this.communityId(), this.applicationId(), attachmentId);
      window.open(link.url, '_blank', 'noopener');
    } catch {
      this.unavailable.update(s => new Set(s).add(attachmentId));
    }
  }

  async vote(e: ArcVoteEvent): Promise<void> {
    await this.run(async () => {
      await this.arc.vote(this.communityId(), this.applicationId(), e.choice, e.comment);
      this.voteCard()?.reset();
    });
  }

  /** 031 item 5: undo my vote so I can recast it. */
  async undo(): Promise<void> {
    await this.run(() => this.arc.undoVote(this.communityId(), this.applicationId()));
  }

  async requestInfo(message: string): Promise<void> {
    await this.run(async () => {
      await this.arc.requestInfo(this.communityId(), this.applicationId(), message);
      this.voteCard()?.reset();
    });
  }

  async recordOutcome(body: ArcOutcomeRequest): Promise<void> {
    await this.run(() => this.arc.recordOutcome(this.communityId(), this.applicationId(), body));
  }

  async resendEmail(): Promise<void> {
    await this.run(() => this.arc.resendOutcomeEmail(this.communityId(), this.applicationId()));
  }

  private async run(action: () => Promise<unknown>): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set(null);
    try {
      await action();
      await this.refresh();
    } catch (e: any) {
      this.error.set(e?.error?.message ?? 'That action could not be completed.');
    } finally {
      this.busy.set(false);
    }
  }
}
