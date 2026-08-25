import { Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { BoardService, MyCommunity } from '../../../core/services/board.service';
import { BoardNavigationService } from '../../../core/services/board-navigation.service';

// 025 FR-025: the "My Communities" list. Rendered/reached only when the user holds more
// than one active community membership. Selecting a row sets the active community and
// lands the user on that community's home.
@Component({
  selector: 'app-board-communities',
  standalone: true,
  template: `
    <div class="page-header">
      <h1 class="page-title">My communities</h1>
    </div>
    <p class="muted" style="margin-top:-8px;">
      Shown only when you hold more than one — board member or manager alike.
    </p>

    @if (loading()) {
      <div class="card"><p class="muted">Loading…</p></div>
    } @else if (communities().length === 0) {
      <div class="card"><p class="muted">You don't hold an active membership in any community.</p></div>
    } @else {
      <div class="card" style="padding:0;overflow:hidden;">
        <table class="data-table">
          <thead><tr><th>Community</th><th style="width:150px;">Role</th><th style="width:96px;">Status</th></tr></thead>
          <tbody>
            @for (c of communities(); track c.id) {
              <tr>
                <td><button type="button" class="link" style="background:none;border:none;padding:0;font:inherit;cursor:pointer;"
                            (click)="open(c)">{{ c.communityName }}</button></td>
                <td class="muted">{{ c.role }}</td>
                <td>
                  @if (c.status === 'Active') {
                    <span style="color:var(--ok);font-weight:600;">✓</span>
                  } @else {
                    <span class="muted">{{ c.status }}</span>
                  }
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }
  `
})
export class CommunitiesComponent implements OnInit {
  private board = inject(BoardService);
  private nav = inject(BoardNavigationService);
  private router = inject(Router);

  readonly communities = signal<MyCommunity[]>([]);
  readonly loading = signal(true);

  async ngOnInit(): Promise<void> {
    try {
      const page = await this.board.getMyCommunities();
      this.communities.set(page.items);
    } catch {
      this.communities.set([]);
    } finally {
      this.loading.set(false);
    }
  }

  async open(c: MyCommunity): Promise<void> {
    this.nav.setActiveCommunity(c.id);
    await this.router.navigate(['/app/board/home']);
  }
}
