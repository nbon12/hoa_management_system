import { AfterViewChecked, Component, ElementRef, OnDestroy, OnInit, ViewChild, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import {
  CompanyService, PortfolioItem, PortfolioPage, PortfolioSort, PortfolioStatusFilter,
} from '../../core/services/company.service';
import { AuthService } from '../../core/services/auth.service';
import { BoardNavigationService } from '../../core/services/board-navigation.service';
import { ToastService } from '../../core/services/toast.service';
import { landingTargetFor } from '../../core/services/landing';
import { refusalCodeOf, refusalMessage } from '../../shared/refusal-messages';
import { PaginationComponent } from '../../shared/pagination.component';

const PAGE_SIZE = 25;
const SEARCH_DELAY_MS = 300;

type StatusKey = PortfolioStatusFilter | 'all';

const STATUS_LABELS: Record<StatusKey, string> = { all: 'All', onboarding: 'Onboarding', active: 'Active', archived: 'Archived' };

function communities(n: number): string {
  return n === 1 ? '1 community' : `${n} communities`;
}

/**
 * 030 US1, US6 / FR-034, FR-035 / brief 02: the Company Administrator's list of every community of
 * the company, archived ones included, 25 to a page, A–Z by community name by default.
 *
 * - Flags are words: "No manager" (no open-ended Community Manager; a pending invitation doesn't
 *   count) in Managers, "Differs from defaults" in Architectural review.
 * - One status filter at a time, with counts; a "No manager" toggle narrows it; search matches
 *   community or legal name as the person types; Community, Status and Parent sort (`aria-sort`).
 * - Each row links to that community's Community settings. A 403 sends the person to their landing.
 */
@Component({
  selector: 'app-portfolio',
  standalone: true,
  imports: [RouterLink, PaginationComponent],
  template: `
    <div class="page-header">
      <h1 class="page-title">Portfolio</h1>
      <div class="page-header__actions">
        <a class="btn btn--primary" routerLink="/app/company/communities/new">Add community</a>
      </div>
    </div>
    <p class="muted">Every community of the company, including archived ones.</p>

    <div class="pf__controls">
      <div class="pf__filters">
        <div class="pf__group" role="group" aria-label="Filter by status">
          @for (f of statusButtons(); track f.key) {
            <button type="button" class="btn pf__filter" [class.pf__filter--on]="status() === f.key" [attr.aria-pressed]="status() === f.key"
                    [attr.aria-label]="f.label + ', ' + f.count" (click)="setStatus(f.key)">{{ f.label }} · {{ f.value }}</button>
          }
        </div>
        <button type="button" class="btn pf__filter" [class.pf__filter--on]="noManager()" [attr.aria-pressed]="noManager()"
                [attr.aria-label]="'No manager, ' + noManagerCount()" (click)="toggleNoManager()">No manager · {{ counts()?.noManager ?? 0 }}</button>
      </div>
      <div class="pf__search">
        <label class="field-label" for="pf-search">Search communities</label>
        <input class="field" id="pf-search" type="search" placeholder="Search by community or legal name" maxlength="100"
               [value]="search()" (input)="onSearch($any($event.target).value)" />
      </div>
    </div>

    <section class="card pf" aria-labelledby="pf-title">
      <h2 class="section-title" id="pf-title" tabindex="-1" #heading>Communities</h2>
      @switch (state()) {
        @case ('loading') {
          <p class="sr-only" role="status">Loading communities…</p>
          <div class="ph pf__ph" aria-hidden="true">Communities</div>
        }
        @case ('error') {
          <div role="alert" class="pf__state">
            <p>Couldn't load communities.</p>
            <button type="button" class="btn" (click)="load(offset())">Try again</button>
          </div>
        }
        @default {
          @if (page(); as p) {
            @if (p.items.length === 0) {
              <div class="pf__state">
                @if (search().trim()) {
                  <p role="status">{{ noMatch() }}</p>
                  <button type="button" class="btn" (click)="clearSearch()">Clear search</button>
                } @else if (status() !== 'all' || noManager()) {
                  <p role="status">{{ emptyFilter() }}</p>
                } @else {
                  <p>The company has no communities yet.</p>
                  <a class="btn" routerLink="/app/company/communities/new">Add community</a>
                }
              </div>
            } @else {
              <div class="pf__scroll">
                <table class="data-table" aria-labelledby="pf-title">
                  <thead>
                    <tr>
                      <th scope="col" [attr.aria-sort]="ariaSort('name')"><button type="button" class="pf__sort" (click)="sortBy('name')">Community</button></th>
                      <th scope="col">Legal name</th>
                      <th scope="col" [attr.aria-sort]="ariaSort('status')"><button type="button" class="pf__sort" (click)="sortBy('status')">Status</button></th>
                      <th scope="col" [attr.aria-sort]="ariaSort('parent')"><button type="button" class="pf__sort" (click)="sortBy('parent')">Parent</button></th>
                      <th scope="col">Managers</th>
                      <th scope="col">Architectural review</th>
                      <th scope="col">Settings</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (c of p.items; track c.id) {
                      <tr>
                        <th scope="row" class="pf__name">{{ c.communityName }}</th>
                        <td>@if (c.legalName) { {{ c.legalName }} } @else { <span aria-hidden="true">—</span><span class="sr-only">No legal name</span> }</td>
                        <td><span class="pill">{{ c.status }}</span></td>
                        <td>@if (c.parent) { {{ c.parent.communityName }} } @else { <span aria-hidden="true">—</span><span class="sr-only">No parent</span> }</td>
                        <td>
                          <ul class="pf__managers">
                            @if (c.noManager) { <li><span class="pill pill--warn">No manager</span></li> }
                            @for (m of c.managers; track m.userId) { <li>{{ m.name }}</li> }
                            @for (i of c.pendingManagerInvitations; track i.email) {
                              <li><span aria-hidden="true">{{ i.email }} · invitation pending</span><span class="sr-only">{{ i.email }}, invitation pending</span></li>
                            }
                          </ul>
                        </td>
                        <td>
                          @if (c.differsFromDefaults) { <span class="pill pill--warn">Differs from defaults</span> }
                          @else { <span aria-hidden="true">—</span><span class="sr-only">Same as company defaults</span> }
                        </td>
                        <td>
                          <a class="link" [routerLink]="settingsLink(c)" [attr.aria-label]="'Community settings for ' + c.communityName">Community settings</a>
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
              <app-pagination [totalItems]="p.total" [limit]="pageSize" [offset]="p.offset" noun="communities" (pageChange)="goTo($event)" />
            }
          }
        }
      }
    </section>
  `,
  styles: [`
    :host { display: flex; flex-direction: column; gap: 12px; }
    .pf__controls { display: flex; flex-wrap: wrap; align-items: flex-end; justify-content: space-between; gap: 10px 20px; }
    .pf__filters { display: flex; flex-wrap: wrap; gap: 6px 14px; }
    .pf__group { display: flex; flex-wrap: wrap; gap: 6px; }
    .pf__filter--on { background: var(--lav-2); border-color: var(--ink); font-weight: 600; }
    .pf__search { display: flex; flex-direction: column; gap: 4px; min-width: 240px; }
    .pf { padding: 0; overflow: hidden; }
    .pf > .section-title { padding: 14px 16px 6px; }
    .pf__ph { height: 120px; margin: 0 16px 16px; }
    .pf__state { display: flex; flex-direction: column; gap: 10px; align-items: flex-start; padding: 8px 16px 16px; }
    .pf__scroll { overflow-x: auto; position: relative; }
    .pf__name { text-align: left; font-weight: 600; background: transparent; text-transform: none; letter-spacing: 0; font-size: 12.5px; color: var(--ink); }
    .pf__sort { background: none; border: 0; padding: 0; font: inherit; color: inherit; text-transform: inherit; letter-spacing: inherit; cursor: pointer; text-decoration: underline dotted; }
    .pf__managers { list-style: none; display: flex; flex-direction: column; gap: 2px; }
  `],
})
export class PortfolioComponent implements OnInit, AfterViewChecked, OnDestroy {
  private company = inject(CompanyService);
  private auth = inject(AuthService);
  private nav = inject(BoardNavigationService);
  private router = inject(Router);
  private toasts = inject(ToastService);
  private host = inject<ElementRef<HTMLElement>>(ElementRef);

  @ViewChild('heading') private heading?: ElementRef<HTMLElement>;

  readonly pageSize = PAGE_SIZE;
  readonly state = signal<'loading' | 'ready' | 'error'>('loading');
  readonly page = signal<PortfolioPage | null>(null);
  readonly offset = signal(0);
  readonly status = signal<StatusKey>('all');
  readonly noManager = signal(false);
  readonly search = signal('');
  readonly sort = signal<PortfolioSort>('name');
  readonly dir = signal<'asc' | 'desc'>('asc');

  readonly counts = computed(() => this.page()?.counts ?? null);
  readonly statusButtons = computed(() => {
    const c = this.counts();
    return (['all', 'onboarding', 'active', 'archived'] as StatusKey[]).map(key => {
      const value = c ? c[key] : 0;
      return { key, label: STATUS_LABELS[key], value, count: communities(value) };
    });
  });
  readonly noManagerCount = computed(() => communities(this.counts()?.noManager ?? 0));

  /** "No communities match “Keystone Pk”." · "No Archived communities match “Keystone”." */
  readonly noMatch = computed(() => {
    const q = this.search().trim();
    const s = this.status();
    return s === 'all' ? `No communities match “${q}”.` : `No ${STATUS_LABELS[s]} communities match “${q}”.`;
  });

  readonly emptyFilter = computed(() => {
    const s = this.status();
    return s === 'all' ? 'No communities match these filters.' : `No ${STATUS_LABELS[s]} communities.`;
  });

  private focusHeading = false;
  private searchTimer: ReturnType<typeof setTimeout> | null = null;
  private loadSeq = 0;

  ngOnInit(): void {
    void this.load(0);
  }

  ngOnDestroy(): void {
    if (this.searchTimer) clearTimeout(this.searchTimer);
  }

  ngAfterViewChecked(): void {
    if (this.focusHeading && this.heading) {
      this.focusHeading = false;
      const el = this.heading.nativeElement;
      setTimeout(() => el.focus());
    }
  }

  settingsLink(c: PortfolioItem): string[] {
    return ['/app/company/communities', c.id, 'settings', 'profile'];
  }

  ariaSort(key: PortfolioSort): 'ascending' | 'descending' | 'none' {
    if (this.sort() !== key) return 'none';
    return this.dir() === 'asc' ? 'ascending' : 'descending';
  }

  sortBy(key: PortfolioSort): void {
    if (this.sort() === key) this.dir.set(this.dir() === 'asc' ? 'desc' : 'asc');
    else {
      this.sort.set(key);
      this.dir.set('asc');
    }
    void this.load(0);
  }

  /** Focus stays on the pressed button (brief 02 §6). */
  setStatus(key: StatusKey): void {
    if (this.status() === key) return;
    this.status.set(key);
    void this.load(0);
  }

  toggleNoManager(): void {
    this.noManager.set(!this.noManager());
    void this.load(0);
  }

  onSearch(value: string): void {
    this.search.set(value);
    if (this.searchTimer) clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => {
      this.searchTimer = null;
      void this.load(0);
    }, SEARCH_DELAY_MS);
  }

  clearSearch(): void {
    this.search.set('');
    void this.load(0);
    this.host.nativeElement.querySelector<HTMLInputElement>('#pf-search')?.focus();
  }

  async goTo(offset: number): Promise<void> {
    await this.load(offset);
    this.focusHeading = true;
  }

  async load(offset: number): Promise<void> {
    const seq = ++this.loadSeq;
    this.offset.set(offset);
    if (!this.page()) this.state.set('loading');
    const status = this.status();
    try {
      const result = await this.company.getCommunities({
        status: status === 'all' ? undefined : status,
        noManager: this.noManager() || undefined,
        search: this.search().trim() || undefined,
        sort: this.sort(),
        dir: this.dir(),
        limit: PAGE_SIZE,
        offset,
      });
      if (seq !== this.loadSeq) return;
      this.page.set(result);
      this.state.set('ready');
    } catch (err) {
      if (seq !== this.loadSeq) return;
      if (refusalCodeOf(err) === 'FORBIDDEN') {
        // FR-035: never shown to anyone but a Company Administrator; the server refused.
        this.toasts.show(refusalMessage('FORBIDDEN', { surface: 'page' }));
        const target = landingTargetFor(this.auth.user());
        this.nav.setActiveCommunity(target.activeCommunityId);
        await this.router.navigate(target.commands);
        return;
      }
      this.state.set('error');
    }
  }
}
