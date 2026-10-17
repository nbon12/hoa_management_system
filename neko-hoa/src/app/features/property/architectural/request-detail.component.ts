import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ResidentArchitecturalService } from '../../../core/services/resident-architectural.service';
import { ResidentArcAttachment, ResidentArcDetail } from '../../../core/models';
import { InfoReplyComponent } from './info-reply.component';
import { WithdrawDialogComponent } from './withdraw-dialog.component';
import { errorMessage, fileSize, projectTypeLabel, statusLabel, statusTone } from './resident-arc-format';

const TIMELINE_LABELS: Record<string, string> = {
  Submitted: 'Submitted', InfoRequested: 'Board asked for more information', InfoReplied: 'You replied',
  Approved: 'Approved', Denied: 'Denied', Withdrawn: 'Withdrawn',
};

/**
 * 029 US2–US4/US6: one request — what was submitted, its timeline and files, the board's questions,
 * and the outcome. Residents never see votes, voters or board comments (FR-016); the API doesn't
 * send them and this page has nowhere to show them.
 */
@Component({
  selector: 'app-arc-request-detail',
  standalone: true,
  imports: [DatePipe, RouterLink, InfoReplyComponent, WithdrawDialogComponent],
  styles: [`
    :host { display: block; max-width: 860px; }
    .facts { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 10px 20px; }
    .facts .full { grid-column: 1 / -1; }
    .value { overflow-wrap: anywhere; white-space: pre-wrap; }
    .files, .timeline, .revisions { list-style: none; margin: 0; padding: 0; display: grid; gap: 6px; }
    .files li { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; }
    .stack { display: grid; gap: 14px; }
    .actions { display: flex; flex-wrap: wrap; gap: 10px; }
    @media (max-width: 600px) {
      .facts { grid-template-columns: 1fr; }
      .actions .btn { flex: 1 1 100%; }
      .page-header { flex-wrap: wrap; }
    }
  `],
  template: `
    @if (loading()) {
      <p class="muted"><span class="spinner"></span> Loading…</p>
    } @else if (!detail()) {
      <div class="alert alert--error" role="alert"><span>⚠</span> {{ error() }}</div>
      <a class="btn btn--ghost" routerLink="/app/property/architectural">Back to my requests</a>
    } @else {
    @if (detail(); as d) {
      <div class="page-header">
        <h1 class="page-title">{{ d.displayId }}@if (d.revision > 1) { <span class="pill" style="margin-left:6px;">v{{ d.revision }}</span> } · {{ d.projectTitle }}</h1>
        <div class="page-header__actions">
          <a class="btn btn--ghost" routerLink="/app/property/architectural">Back to my requests</a>
        </div>
      </div>
      <p><span class="pill {{ tone(d) }}">{{ label(d) }}</span>
        <span class="muted"> · Received {{ d.receivedDate | date:'MM/dd/yy' }} · Decision due {{ d.dueDate | date:'MM/dd/yy' }}</span></p>
      @if (error()) { <div class="alert alert--error" role="alert"><span>⚠</span> {{ error() }}</div> }

      <div class="stack">
        @if (d.decision; as decision) {
          <section class="card" aria-labelledby="decision-title">
            <h2 class="section-title" id="decision-title">
              @if (decision.outcome === 'Approved') { Approved } @else { {{ decision.wording === 'RevisionsRequested' ? 'Denied · revisions requested' : 'Denied' }} }
            </h2>
            @if (decision.outcome === 'Approved') {
              @if (d.conditionsOfApproval) {
                <div class="field-label">Conditions of approval</div>
                <p class="value">{{ d.conditionsOfApproval }}</p>
              }
              <p class="muted">Keep the work consistent with what you submitted. Changes need a new request.</p>
            } @else {
              <div class="field-label">{{ decision.wording === 'RevisionsRequested' ? 'What needs to change' : 'Reason' }}</div>
              <p class="value">{{ d.ownerReason }}</p>
              @if (d.formalDisapprovalStatement) { <p class="muted value">{{ d.formalDisapprovalStatement }}</p> }
              @if (d.canRevise) {
                <a class="btn btn--primary" [routerLink]="['/app/property/architectural', d.id, 'revise']">Revise and resubmit</a>
              }
            }
          </section>
        }

        @for (q of unanswered(d); track q.id) {
          <app-arc-info-reply [applicationId]="d.id" [request]="q" (replied)="detail.set($event)" />
        }

        <section class="card" aria-labelledby="details-title">
          <h2 class="section-title" id="details-title">What you submitted</h2>
          <div class="facts">
            <div><div class="field-label">Project type</div><div class="value">{{ type(d) }}</div></div>
            <div><div class="field-label">Planned dates</div>
              <div class="value">{{ d.plannedStartDate | date:'MM/dd/yy' }} – {{ d.plannedCompletionDate | date:'MM/dd/yy' }}</div></div>
            <div class="full"><div class="field-label">Description</div><div class="value">{{ d.description }}</div></div>
            @if (d.contractorName || d.contractorContact) {
              <div class="full"><div class="field-label">Contractor</div>
                <div class="value">{{ d.contractorName }}@if (d.contractorContact) { · {{ d.contractorContact }} }</div></div>
            }
          </div>
        </section>

        <section class="card" aria-labelledby="files-title">
          <h2 class="section-title" id="files-title">Files</h2>
          @if (d.attachments.length === 0) {
            <p class="muted">No files attached.</p>
          } @else {
            <ul class="files">
              @for (f of d.attachments; track f.id) {
                <li>
                  <span>📎 {{ f.fileName }} <span class="muted">· {{ size(f.sizeBytes) }}@if (f.infoRequestId) { · added with a reply }</span></span>
                  <button class="btn btn--ghost" (click)="open(d.id, f)" [attr.aria-label]="'Open ' + f.fileName">Open</button>
                </li>
              }
            </ul>
          }
        </section>

        @if (answered(d).length) {
          <section class="card" aria-labelledby="questions-title">
            <h2 class="section-title" id="questions-title">Questions from the board</h2>
            @for (q of answered(d); track q.id) {
              <div style="margin-bottom:10px;">
                <div class="field-label">Asked {{ q.requestedAt | date:'MM/dd/yy' }}</div>
                <p class="value" style="margin:2px 0;">{{ q.message }}</p>
                <div class="field-label">Your reply · {{ q.respondedAt | date:'MM/dd/yy' }}</div>
                <p class="value" style="margin:2px 0;">{{ q.responseMessage }}</p>
              </div>
            }
          </section>
        }

        <section class="card" aria-labelledby="timeline-title">
          <h2 class="section-title" id="timeline-title">Timeline</h2>
          <ol class="timeline">
            @for (e of d.timeline; track $index) {
              <li><span class="mono">{{ e.at | date:'MM/dd/yy' }}</span> · {{ timelineLabel(e.event) }}</li>
            }
          </ol>
        </section>

        @if (d.revisions.length > 1) {
          <section class="card" aria-labelledby="revisions-title">
            <h2 class="section-title" id="revisions-title">Revisions</h2>
            <ul class="revisions">
              @for (r of d.revisions; track r.id) {
                <li>
                  @if (r.id === d.id) { <strong>v{{ r.revision }} (this one)</strong> }
                  @else { <a [routerLink]="['/app/property/architectural', r.id]">v{{ r.revision }}</a> }
                  · {{ statusText(r.status) }} · received {{ r.receivedDate | date:'MM/dd/yy' }}
                </li>
              }
            </ul>
          </section>
        }

        @if (d.canWithdraw) {
          <div class="actions">
            <button class="btn btn--ghost" (click)="withdrawing.set(true)">Withdraw request</button>
          </div>
        }
      </div>

      @if (withdrawing()) {
        <app-arc-withdraw-dialog [applicationId]="d.id" [displayId]="d.displayId ?? ''"
          (withdrawn)="detail.set($event); withdrawing.set(false)" (cancelled)="withdrawing.set(false)" />
      }
    }
    }
  `,
})
export class RequestDetailComponent implements OnInit {
  private api = inject(ResidentArchitecturalService);
  private route = inject(ActivatedRoute);

  readonly size = fileSize;
  detail = signal<ResidentArcDetail | null>(null);
  loading = signal(true);
  error = signal<string | null>(null);
  withdrawing = signal(false);

  async ngOnInit(): Promise<void> {
    try {
      this.detail.set(await this.api.detail(this.route.snapshot.paramMap.get('id')!));
    } catch (err) {
      this.error.set(errorMessage(err, 'This request could not be loaded.'));
    } finally {
      this.loading.set(false);
    }
  }

  unanswered(d: ResidentArcDetail) { return d.status === 'MoreInfoRequested' ? d.infoRequests.filter(q => !q.respondedAt) : []; }
  answered(d: ResidentArcDetail) { return d.infoRequests.filter(q => !!q.respondedAt); }
  label(d: ResidentArcDetail): string { return statusLabel(d.status); }
  statusText = statusLabel;
  tone(d: ResidentArcDetail): string { return statusTone(d.status); }
  type(d: ResidentArcDetail): string { return projectTypeLabel(d.projectType); }
  timelineLabel(event: string): string { return TIMELINE_LABELS[event] ?? event; }

  async open(applicationId: string, file: ResidentArcAttachment): Promise<void> {
    try {
      const link = await this.api.attachmentUrl(applicationId, file.id);
      window.open(link.url, '_blank', 'noopener');
    } catch (err) {
      this.error.set(errorMessage(err));
    }
  }
}
