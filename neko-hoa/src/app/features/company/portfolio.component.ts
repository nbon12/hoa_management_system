import { AfterViewChecked, Component, ElementRef, OnInit, ViewChild, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CompanyService, PortfolioItem, PortfolioPage } from '../../core/services/company.service';
import { AuthService } from '../../core/services/auth.service';
import { BoardNavigationService } from '../../core/services/board-navigation.service';
import { ToastService } from '../../core/services/toast.service';
import { landingTargetFor } from '../../core/services/landing';
import { refusalCodeOf, refusalMessage } from '../../shared/refusal-messages';
import { PaginationComponent } from '../../shared/pagination.component';

const PAGE_SIZE = 25;

/**
 * 030 US1, US6 / FR-034, FR-035 / brief 02: the Company Administrator's list of every community of
 * the company, archived ones included, 25 to a page, A–Z by community name. Each row links to that
 * community's Community settings. A 403 sends the person to their landing (FR-035).
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
                <p>The company has no communities yet.</p>
                <a class="btn" routerLink="/app/company/communities/new">Add community</a>
              </div>
            } @else {
              <div class="pf__scroll">
                <table class="data-table" aria-labelledby="pf-title">
                  <thead>
                    <tr>
                      <th scope="col">Community</th>
                      <th scope="col">Legal name</th>
                      <th scope="col">Status</th>
                      <th scope="col">Parent</th>
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
    .pf { padding: 0; overflow: hidden; }
    .pf > .section-title { padding: 14px 16px 6px; }
    .pf__ph { height: 120px; margin: 0 16px 16px; }
    .pf__state { display: flex; flex-direction: column; gap: 10px; align-items: flex-start; padding: 8px 16px 16px; }
    .pf__scroll { overflow-x: auto; }
    .pf__name { text-align: left; font-weight: 600; background: transparent; text-transform: none; letter-spacing: 0; font-size: 12.5px; color: var(--ink); }
  `],
})
export class PortfolioComponent implements OnInit, AfterViewChecked {
  private company = inject(CompanyService);
  private auth = inject(AuthService);
  private nav = inject(BoardNavigationService);
  private router = inject(Router);
  private toasts = inject(ToastService);

  @ViewChild('heading') private heading?: ElementRef<HTMLElement>;

  readonly pageSize = PAGE_SIZE;
  readonly state = signal<'loading' | 'ready' | 'error'>('loading');
  readonly page = signal<PortfolioPage | null>(null);
  readonly offset = signal(0);
  private focusHeading = false;

  ngOnInit(): void {
    void this.load(0);
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

  async goTo(offset: number): Promise<void> {
    await this.load(offset);
    this.focusHeading = true;
  }

  async load(offset: number): Promise<void> {
    this.offset.set(offset);
    if (!this.page()) this.state.set('loading');
    try {
      this.page.set(await this.company.getCommunities({ limit: PAGE_SIZE, offset }));
      this.state.set('ready');
    } catch (err) {
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
