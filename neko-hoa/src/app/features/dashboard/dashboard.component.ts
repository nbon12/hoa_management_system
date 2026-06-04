import { Component, inject, signal, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CurrencyPipe, DatePipe, LowerCasePipe } from '@angular/common';
import { DashboardService } from '../../core/services/dashboard.service';
import { AuthService } from '../../core/services/auth.service';
import { DashboardSummary } from '../../core/models';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [RouterLink, CurrencyPipe, DatePipe, LowerCasePipe],
  template: `
    @if (loading()) {
      <div style="padding:40px;text-align:center;color:var(--ink-mute);">
        <span class="spinner" style="width:24px;height:24px;"></span>
        <p>Loading dashboard…</p>
      </div>
    } @else if (error()) {
      <div class="alert alert--error"><span>⚠</span> {{ error() }}</div>
    } @else if (summary()) {
    <!-- Page header -->
    <div style="display:flex;align-items:baseline;gap:10px;">
      <h1 class="page-title">Hi <span class="hand">{{ user()?.firstName }}</span> 👋</h1>
      <span class="muted" style="margin-left:auto;">Last activity 04/21/2026 · 3:58 PM</span>
    </div>
    <p class="muted">Here's what's happening at Sakura Heights this week.</p>

    <!-- 4 stat cards -->
    <div class="grid-4">
      <div class="card" [class.card--rose]="(summary()?.currentBalance ?? 0) > 0">
        <div class="field-label">Balance</div>
        <div class="mono" style="font-size:22px;">{{ summary()?.currentBalance | currency }}</div>
        @if ((summary()?.currentBalance ?? 0) > 0) {
          <span class="pill pill--warn" style="margin-top:6px;">
            due {{ summary()?.balanceDueDate | date:'M/d' }}
          </span>
        } @else {
          <span class="pill pill--ok" style="margin-top:6px;">paid up</span>
        }
      </div>
      <div class="card">
        <div class="field-label">Violations</div>
        <div class="mono" style="font-size:22px;">{{ summary()?.openViolations }}</div>
        @if ((summary()?.openViolations ?? 0) > 0) {
          <span class="pill pill--warn" style="margin-top:6px;">needs attention</span>
        } @else {
          <span class="pill pill--ok" style="margin-top:6px;">compliant</span>
        }
      </div>
      <div class="card">
        <div class="field-label">Next event</div>
        <div style="font-weight:600;font-size:13px;">{{ summary()?.nextEvent?.title }}</div>
        <div class="muted">{{ summary()?.nextEvent?.date | date:'MMM d · EEE' }}</div>
      </div>
      <div class="card">
        <div class="field-label">Documents</div>
        <div class="mono" style="font-size:22px;">{{ summary()?.documentCount }}</div>
        <div class="muted">{{ summary()?.newDocumentsThisMonth }} new this month</div>
      </div>
    </div>

    <!-- Main content grid -->
    <div class="grid-2" style="grid-template-columns:2fr 1fr;">

      <!-- Left: Recent activity -->
      <div class="card">
        <div style="display:flex;align-items:baseline;">
          <div class="section-title" style="margin:0;">Recent activity</div>
          <a class="link" style="margin-left:auto;font-size:12px;" routerLink="/app/payments/statement">View ledger →</a>
        </div>
        <table class="data-table" style="margin-top:10px;">
          <thead>
            <tr><th>Date</th><th>Description</th><th class="num">Charge</th><th class="num">Payment</th></tr>
          </thead>
          <tbody>
            @for (row of summary()?.recentActivity; track row.id) {
              <tr>
                <td>{{ row.date | date:'MM/dd/yy' }}</td>
                <td>{{ row.description }}</td>
                <td class="num">{{ row.charge ? (row.charge | currency) : '—' }}</td>
                <td class="num">{{ row.payment ? (row.payment | currency) : '—' }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>

      <!-- Right col -->
      <div class="flex-col">
        <!-- Pinned announcement -->
        @if (summary()?.pinnedAnnouncement) {
          <div class="card card--pink">
            <div style="display:flex;align-items:baseline;">
              <div class="section-title" style="margin:0;">Pinned announcement</div>
              <span class="pill" style="margin-left:auto;">NEW</span>
            </div>
            <div style="font-weight:600;margin-top:6px;">{{ summary()?.pinnedAnnouncement?.title }}</div>
            <p class="muted" style="margin:4px 0 0;font-size:12px;">{{ summary()?.pinnedAnnouncement?.body }}</p>
          </div>
        }

        <!-- This week -->
        <div class="card">
          <div class="section-title">This week</div>
          <ul style="margin:0;padding:0;list-style:none;font-size:12px;display:flex;flex-direction:column;gap:8px;">
            @for (ev of summary()?.thisWeekEvents; track ev.id) {
              <li style="display:flex;gap:8px;align-items:center;">
                <span class="pill"
                      [style.background]="categoryColor(ev.category)">
                  {{ ev.date | date:'EEE' | lowercase }}
                </span>
                {{ ev.title }} <span class="muted">· {{ ev.location }}</span>
              </li>
            }
          </ul>
        </div>
      </div>
    </div>

    <!-- Payment due card -->
    @if ((summary()?.currentBalance ?? 0) > 0) {
      <div class="card card--rose" style="max-width:480px;">
        <div style="display:flex;align-items:center;gap:8px;">
          <span style="color:var(--warn);">⚠</span>
          <span style="font-weight:600;">Payment due</span>
          <span class="pill pill--warn" style="margin-left:auto;">
            due {{ summary()?.balanceDueDate | date:'M/d' }}
          </span>
        </div>
        <div class="mono" style="font-size:34px;margin:10px 0 12px;">
          {{ summary()?.currentBalance | currency }}
        </div>
        <div style="display:flex;gap:8px;">
          <a routerLink="/app/payments/one-time" class="btn btn--primary">Pay now</a>
          <a routerLink="/app/payments/statement" class="btn btn--ghost">View statement</a>
        </div>
      </div>
    }

    <!-- Community expenses donut -->
    <div class="card card--lav">
      <div class="section-title">My community</div>
      <div class="muted" style="margin-bottom:10px;">Association expenses · last 12 mo</div>
      <div style="display:flex;gap:20px;align-items:center;">
        <div class="donut" style="flex-shrink:0;"></div>
        <div style="font-size:11px;display:grid;grid-template-columns:auto 1fr;gap:3px 8px;flex:1;">
          @for (exp of summary()?.communityExpenses; track exp.label) {
            <span style="width:8px;height:8px;border-radius:2px;align-self:center;"
                  [style.background]="exp.color">
            </span>
            <span>{{ exp.label }} · {{ exp.amount | currency:'USD':'symbol':'1.0-0' }}</span>
          }
        </div>
      </div>
    </div>

    <!-- Quick links -->
    <div class="card card--dashed">
      <div class="section-title">Quick links</div>
      <div style="display:flex;flex-direction:column;gap:5px;font-size:12px;">
        <a class="link" routerLink="/app/payments/recurring">Set up recurring payments</a>
        <a class="link" routerLink="/app/property/owner">Update contact info</a>
        <a class="link" routerLink="/app/property/owner">Update mailing address</a>
      </div>
    </div>
    } <!-- end @else if summary() -->
  `
})
export class DashboardComponent implements OnInit {
  private dashSvc = inject(DashboardService);
  user = inject(AuthService).user;

  summary  = signal<DashboardSummary | null>(null);
  loading  = signal(true);
  error    = signal('');

  async ngOnInit() {
    try {
      this.summary.set(await this.dashSvc.getSummary());
    } catch (e: any) {
      this.error.set(e?.error?.message ?? 'Failed to load dashboard.');
    } finally {
      this.loading.set(false);
    }
  }

  categoryColor(cat: string): string {
    const map: Record<string, string> = {
      Board: 'var(--lav-2)', Amenity: 'var(--pink-2)',
      Social: 'var(--social)', Maintenance: 'var(--maintenance)',
    };
    return map[cat] ?? 'var(--lav-2)';
  }
}
