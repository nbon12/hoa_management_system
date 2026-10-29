import { Component, effect, inject, signal, untracked } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { ArcListItem, ArcVoteChoice, ArchitecturalService } from '../../../core/services/architectural.service';
import { TallyComponent } from './tally.component';
import { arcContext } from './arc-context';
import { fmtDate } from './arc-format';
import { VoteChoiceComponent } from './vote-choice.component';

// 027 US5 — wireframe `NeedsYourVote` on Community Home: open applications the caller can still vote on,
// with inline Approve / Revisions needed / Deny and a link to all architectural applications (FR-032/FR-033).
// 031: a row opens that application (item 2); picking a choice no longer votes at once — nothing is
// pre-selected, and a Save button appears once a choice is picked (items 3, 4).
@Component({
  selector: 'app-arc-needs-your-vote-card',
  standalone: true,
  imports: [RouterLink, TallyComponent, VoteChoiceComponent],
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
          <li class="nyv__row" [attr.data-app-id]="a.id" (click)="rowClick($event, a)">
            <a class="mono link nyv__id" [routerLink]="['/app/board/architectural']" [queryParams]="{ open: a.id }"
               [attr.aria-label]="'Open ' + a.displayId">{{ a.displayId }}</a>
            <div class="nyv__main">
              <div class="nyv__project">{{ a.projectTitle }}</div>
              <div class="muted nyv__sub">{{ a.propertyAddress }} · {{ a.ownerName }}</div>
            </div>
            <span class="link nyv__files">📎 {{ a.attachmentCount }}</span>
            <app-arc-tally [tally]="a.tally" />
            <span class="muted nyv__due">due {{ date(a.dueDate) }}</span>
            <app-arc-vote-choice class="nyv__actions" [busy]="busyId() === a.id" submitLabel="Save"
                                 [groupLabel]="'Your vote on ' + a.displayId" (submitted)="vote(a, $event)" />
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
                border: 1.5px solid var(--line); border-radius: 11px; flex-wrap: wrap; cursor: pointer;
                transition: border-color .12s, box-shadow .12s; }
    .nyv__row:hover { border-color: var(--violet); box-shadow: 0 1px 0 var(--lav-2); }
    .nyv__id { font-size: 11px; width: 70px; }
    .nyv__main { flex: 1; min-width: 160px; }
    .nyv__project { font-weight: 600; font-size: 12.5px; }
    .nyv__sub, .nyv__due { font-size: 11px; }
    .nyv__files { font-size: 11.5px; }
    .nyv__actions { display: flex; }
    .nyv__error { color: var(--warn); font-size: 12px; }
  `]
})
export class NeedsYourVoteCardComponent {
  private arc = inject(ArchitecturalService);
  private router = inject(Router);
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

  /** 031 item 2: a click on the row's whitespace opens that application; its controls keep their own action. */
  rowClick(event: MouseEvent, a: ArcListItem): void {
    const target = event.target as HTMLElement | null;
    if (target?.closest('button, a, input, label, textarea, select, [role="group"]')) return;
    void this.router.navigate(['/app/board/architectural'], { queryParams: { open: a.id } });
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
