import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../../core/services/auth.service';
import { BoardNavigationService } from '../../../core/services/board-navigation.service';
import { BoardService, Membership } from '../../../core/services/board.service';

// 025 US5 / FR-042: the in-product membership-admin surface. Community managers create and
// edit CommunityMemberships (assign a user, role, status, term). Reachable only via the
// manager-gated nav entry (board.guard enforces the route); the server re-checks the
// ManageMemberships capability on every call. Surfaces 403 / 422 / LAST_MANAGER errors.
@Component({
  selector: 'app-membership-admin',
  standalone: true,
  imports: [FormsModule],
  template: `
    <div class="page-header">
      <h1 class="page-title">Memberships</h1>
    </div>

    @if (!communityId()) {
      <div class="card"><p class="muted">No active community selected.</p></div>
    } @else {
      <!-- Create / edit form -->
      <div class="card card--lav">
        <div class="field-label">{{ editingId() ? 'Edit membership' : 'Assign a membership' }}</div>
        <div style="display:grid;grid-template-columns:repeat(4,1fr);gap:10px;align-items:end;">
          <label>
            <div class="field-label">User ID</div>
            <input class="input" [(ngModel)]="form.userId" [disabled]="!!editingId()"
                   name="userId" placeholder="user id" />
          </label>
          <label>
            <div class="field-label">Role</div>
            <select class="input" [(ngModel)]="form.role" name="role">
              @for (r of assignableRoles; track r) { <option [value]="r">{{ r }}</option> }
            </select>
          </label>
          @if (editingId()) {
            <label>
              <div class="field-label">Status</div>
              <select class="input" [(ngModel)]="form.status" name="status">
                <option value="Active">Active</option>
                <option value="Inactive">Inactive</option>
              </select>
            </label>
          } @else {
            <label>
              <div class="field-label">Start date</div>
              <input class="input" type="date" [(ngModel)]="form.startDate" name="startDate" />
            </label>
          }
          <label>
            <div class="field-label">End date (optional)</div>
            <input class="input" type="date" [(ngModel)]="form.endDate" name="endDate" />
          </label>
        </div>
        <div style="display:flex;gap:8px;margin-top:12px;">
          <button class="btn btn--primary" [disabled]="busy()" (click)="submit()">
            {{ editingId() ? 'Save changes' : 'Create membership' }}
          </button>
          @if (editingId()) {
            <button class="btn btn--ghost" [disabled]="busy()" (click)="cancelEdit()">Cancel</button>
          }
        </div>
        @if (error()) { <p class="ma-error" role="alert" style="margin:10px 0 0;">{{ error() }}</p> }
      </div>

      <!-- Roster -->
      <div class="card" style="padding:0;overflow:hidden;">
        <table class="data-table">
          <thead><tr>
            <th>Member</th><th style="width:150px;">Role</th><th style="width:96px;">Status</th>
            <th style="width:110px;">Start</th><th style="width:110px;">End</th><th style="width:70px;"></th>
          </tr></thead>
          <tbody>
            @if (memberships().length === 0) {
              <tr><td colspan="6" class="muted" style="text-align:center;padding:24px;">No memberships yet.</td></tr>
            }
            @for (m of memberships(); track m.id) {
              <tr [attr.data-membership-id]="m.id">
                <td>{{ m.userDisplayName }}</td>
                <td class="muted">{{ m.role }}</td>
                <td>
                  @if (m.status === 'Active') {
                    <span class="pill pill--ok">active</span>
                  } @else {
                    <span class="pill">{{ m.status }}</span>
                  }
                </td>
                <td class="muted">{{ m.startDate }}</td>
                <td class="muted">{{ m.endDate ?? '—' }}</td>
                <td><button type="button" class="link" style="background:none;border:none;padding:0;font:inherit;cursor:pointer;"
                            (click)="startEdit(m)">edit</button></td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }
  `,
  styles: [`.ma-error { color: var(--warn); font-size: 12px; }`]
})
export class MembershipAdminComponent implements OnInit {
  private auth = inject(AuthService);
  private nav = inject(BoardNavigationService);
  private board = inject(BoardService);

  readonly assignableRoles = ['BoardMember', 'CommunityManager', 'Accountant'];

  readonly memberships = signal<Membership[]>([]);
  readonly editingId = signal<string | null>(null);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  form = { userId: '', role: 'BoardMember', status: 'Active', startDate: '', endDate: '' };

  readonly communityId = computed(() => {
    const memberships = this.auth.user()?.memberships ?? [];
    const active = this.nav.activeCommunityId();
    if (active) return active;
    return memberships.length ? memberships[0].communityId : null;
  });

  async ngOnInit(): Promise<void> {
    await this.reload();
  }

  private async reload(): Promise<void> {
    const cid = this.communityId();
    if (!cid) return;
    try {
      const page = await this.board.getMemberships(cid);
      this.memberships.set(page.items);
    } catch (e: any) {
      this.error.set(this.messageFor(e));
    }
  }

  startEdit(m: Membership): void {
    this.editingId.set(m.id);
    this.error.set(null);
    this.form = {
      userId: m.userId, role: m.role, status: m.status,
      startDate: m.startDate, endDate: m.endDate ?? '',
    };
  }

  cancelEdit(): void {
    this.editingId.set(null);
    this.error.set(null);
    this.form = { userId: '', role: 'BoardMember', status: 'Active', startDate: '', endDate: '' };
  }

  async submit(): Promise<void> {
    const cid = this.communityId();
    if (!cid || this.busy()) return;
    this.busy.set(true);
    this.error.set(null);
    try {
      const id = this.editingId();
      if (id) {
        await this.board.updateMembership(cid, id, {
          role: this.form.role,
          status: this.form.status,
          endDate: this.form.endDate || null,
        });
      } else {
        await this.board.createMembership(cid, {
          userId: this.form.userId,
          role: this.form.role,
          startDate: this.form.startDate,
          endDate: this.form.endDate || null,
        });
      }
      this.cancelEdit();
      await this.reload();
    } catch (e: any) {
      this.error.set(this.messageFor(e));
    } finally {
      this.busy.set(false);
    }
  }

  private messageFor(e: any): string {
    switch (e?.error?.code) {
      case 'FORBIDDEN':         return 'You are not a manager of this community.';
      case 'LAST_MANAGER':      return 'This is the last active community manager and cannot be removed or downgraded.';
      case 'VALIDATION_ERROR':  return e?.error?.message ?? 'Please check the values and try again.';
      case 'NOT_FOUND':         return 'That membership was not found in this community.';
      default:                  return 'Something went wrong. Please try again.';
    }
  }
}
