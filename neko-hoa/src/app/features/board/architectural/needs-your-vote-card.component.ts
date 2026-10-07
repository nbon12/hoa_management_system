import { Component, effect, inject, signal, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ArcListItem, ArcVoteChoice, ArchitecturalService } from '../../../core/services/architectural.service';
import { TallyComponent } from './tally.component';
import { arcContext } from './arc-context';
import { fmtDate } from './arc-format';

// 027 US5 — wireframe `NeedsYourVote` on Community Home: open applications the caller can still vote on,
// with inline Approve / Revisions needed / Deny and a link to all architectural applications (FR-032/FR-033).
@Component({
  selector: 'app-arc-needs-your-vote-card',
  standalone: true,
  imports: [RouterLink, TallyComponent],
  template: `
    <section class="card nyv" aria-labelledby="nyv-title">
      <div class="nyv__head">
        <h2 id="nyv-title" class="section-title" style="margin:0;">Needs your vote</h2>
        <span class="pill pill--warn nyv__count">{{ items().length }} open</span>
        <a class="link nyv__all" routerLink="/app/board/architectural">All architectural applications →</a>
      </div>
      @if (error()) { <p class="nyv__error" role="alert">{{ error() }}</p> }
      @if (loaded() && items().length === 0) {
        <p class="muted nyv__empty">Nothing needs your vote right now.</p>
      }
      <ul class="nyv__list">
        @for (a of items(); track a.id) {
          <li class="nyv__row" [attr.data-app-id]="a.id">
            <span class="mono nyv__id">{{ a.displayId }}</span>
            <div class="nyv__main">
              <div class="nyv__project">{{ a.projectTitle }}</div>
              <div class="muted nyv__sub">{{ a.propertyAddress }} · {{ a.ownerName }}</div>
            </div>
            <span class="link nyv__files">📎 {{ a.attachmentCount }}</span>
            <app-arc-tally [tally]="a.tally" />
            <span class="muted nyv__due">due {{ date(a.dueDate) }}</span>
            <div class="nyv__actions">
              <button type="button" class="btn nyv__btn nyv__approve" [disabled]="busyId() === a.id" (click)="vote(a, 'Approve')">Approve</button>
              <button type="button" class="btn nyv__btn" [disabled]="busyId() === a.id" (click)="vote(a, 'RevisionsNeeded')">Revisions needed</button>
              <button type="button" class="btn nyv__btn" [disabled]="busyId() === a.id" (click)="vote(a, 'Deny')">Deny</button>
            </div>
          </li>
        }
      </ul>
    </section>
  `,
  styles: [`
    .nyv { border-color: var(--violet); background: var(--lav); }
    .nyv__head { display: flex; align-items: center; gap: 8px; margin-bottom: 10px; }
    .nyv__count { font-size: 10px; }
    .nyv__all { margin-left: auto; font-size: 11.5px; }
    .nyv__list { list-style: none; padding: 0; margin: 0; display: flex; flex-direction: column; gap: 8px; }
    .nyv__row { display: flex; align-items: center; gap: 12px; padding: 11px 13px; background: var(--paper);
                border: 1.5px solid var(--line); border-radius: 11px; flex-wrap: wrap; }
    .nyv__id { font-size: 11px; color: var(--ink-soft); width: 70px; }
    .nyv__main { flex: 1; min-width: 160px; }
    .nyv__project { font-weight: 600; font-size: 12.5px; }
    .nyv__sub, .nyv__due { font-size: 11px; }
    .nyv__files { font-size: 11.5px; }
    .nyv__actions { display: flex; gap: 5px; }
    .nyv__btn { padding: 4px 10px; font-size: 11px; }
    .nyv__approve { background: var(--ok-bg); }
    .nyv__error { color: var(--warn); font-size: 12px; }
  `]
})
export class NeedsYourVoteCardComponent {
  private arc = inject(ArchitecturalService);
  private readonly ctx = arcContext();

  readonly items = signal<ArcListItem[]>([]);
  readonly loaded = signal(false);
  readonly busyId = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly date = fmtDate;

  constructor() {
    effect(() => {
      if (this.ctx.communityId()) untracked(() => void this.load());
    }, { allowSignalWrites: true });
  }

  private async load(): Promise<void> {
    const cid = this.ctx.communityId();
    if (!cid) return;
    try {
      const page = await this.arc.list(cid, { awaitingMyVote: true, limit: 25 });
      this.items.set(page.items);
      this.error.set(null);
    } catch {
      this.error.set('Applications awaiting your vote could not be loaded.');
    } finally {
      this.loaded.set(true);
    }
  }

  async vote(a: ArcListItem, choice: ArcVoteChoice): Promise<void> {
    const cid = this.ctx.communityId();
    if (!cid || this.busyId()) return;
    this.busyId.set(a.id);
    try {
      await this.arc.vote(cid, a.id, choice);
      this.items.update(list => list.filter(i => i.id !== a.id));
      this.error.set(null);
    } catch (e: any) {
      // Reload first (the application may have been decided), then show why the vote was refused.
      await this.load();
      this.error.set(e?.error?.message ?? 'Your vote could not be saved.');
    } finally {
      this.busyId.set(null);
    }
  }
}
