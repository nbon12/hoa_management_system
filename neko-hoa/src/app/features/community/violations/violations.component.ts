import { Component, inject, signal, computed } from '@angular/core';
import { DatePipe } from '@angular/common';
import { CommunityService } from '../../../core/services/community.service';

@Component({
  selector: 'app-violations',
  standalone: true,
  imports: [DatePipe],
  template: `
    <div class="page-header">
      <h1 class="page-title">Violations</h1>
      <div class="page-header__actions">
        <button class="btn btn--ghost">+ Filter</button>
        <button class="btn">Export</button>
      </div>
    </div>

    <!-- Summary cards -->
    <div class="grid-3">
      <div class="card" [class.card--ok]="openCount === 0" [style.border-color]="openCount === 0 ? 'var(--ok)' : ''">
        <div class="field-label">Open</div>
        <div class="mono" style="font-size:28px;" [style.color]="openCount === 0 ? 'var(--ok)' : 'var(--rose)'">
          {{ openCount }}
        </div>
        @if (openCount === 0) {
          <span class="pill pill--ok">all clear</span>
        }
      </div>
      <div class="card">
        <div class="field-label">Closed (12 mo)</div>
        <div class="mono" style="font-size:28px;">{{ closedCount }}</div>
        <div class="muted">last: {{ lastClosedDate | date:'MMM d' }}</div>
      </div>
      <div class="card">
        <div class="field-label">Most common</div>
        <div style="font-weight:600;">{{ mostCommon }}</div>
        <div class="muted">{{ mostCommonCount }} of {{ closedCount }}</div>
      </div>
    </div>

    <!-- Tab bar -->
    <div class="card" style="padding:0;overflow:hidden;">
      <div style="display:flex;border-bottom:1.5px dashed var(--line);">
        <button class="tab" style="border-radius:0;padding:10px 16px;"
                [class.tab--active]="activeTab() === 'open'"
                [style.border-bottom]="activeTab() === 'open' ? '2px solid var(--rose)' : '2px solid transparent'"
                [style.font-weight]="activeTab() === 'open' ? 600 : 400"
                (click)="activeTab.set('open')">
          Open · {{ openCount }}
        </button>
        <button class="tab" style="border-radius:0;padding:10px 16px;"
                [class.tab--active]="activeTab() === 'closed'"
                [style.border-bottom]="activeTab() === 'closed' ? '2px solid var(--rose)' : '2px solid transparent'"
                [style.font-weight]="activeTab() === 'closed' ? 600 : 400"
                (click)="activeTab.set('closed')">
          Closed · {{ closedCount }}
        </button>
        <button class="tab" style="border-radius:0;padding:10px 16px;"
                (click)="activeTab.set('rules')">
          Rules
        </button>
        <button class="tab" style="border-radius:0;padding:10px 16px;"
                (click)="activeTab.set('appeal')">
          Appeal a notice
        </button>
      </div>

      @if (activeTab() === 'open') {
        @if (openCount === 0) {
          <div style="padding:80px 20px;text-align:center;color:var(--ok);display:flex;flex-direction:column;align-items:center;gap:12px;">
            <div class="ph" style="width:80px;height:80px;border-radius:50%;border-color:var(--ok);font-size:32px;">✓</div>
            <div style="font-size:18px;font-weight:600;">Thank you for your compliance!</div>
            <p class="muted" style="max-width:360px;">No open violations on your property. Closed history is available in the tab above.</p>
          </div>
        } @else {
          <table class="data-table">
            <thead><tr><th>Issue</th><th>Date</th><th>Category</th><th>Status</th><th></th></tr></thead>
            <tbody>
              @for (v of openViolations(); track v.id) {
                <tr>
                  <td>{{ v.issue }}</td>
                  <td>{{ v.date | date:'MM/dd/yy' }}</td>
                  <td><span class="pill" style="background:var(--lav-2);">{{ v.category }}</span></td>
                  <td><span class="pill pill--warn">{{ v.status }}</span></td>
                  <td class="link">view →</td>
                </tr>
              }
            </tbody>
          </table>
        }
      }

      @if (activeTab() === 'closed') {
        <table class="data-table">
          <thead><tr><th>Issue</th><th>Date</th><th>Category</th><th>Status</th><th></th></tr></thead>
          <tbody>
            @for (v of closedViolations(); track v.id) {
              <tr>
                <td>{{ v.issue }}</td>
                <td>{{ v.date | date:'MM/dd/yy' }}</td>
                <td><span class="pill" style="background:var(--lav-2);">{{ v.category }}</span></td>
                <td><span class="pill pill--ok">{{ v.status }}</span></td>
                <td class="link">view →</td>
              </tr>
            }
          </tbody>
        </table>
      }

      @if (activeTab() === 'rules') {
        <div style="padding:20px;">
          <p class="muted">Community rules and regulations will be listed here.</p>
        </div>
      }

      @if (activeTab() === 'appeal') {
        <div style="padding:20px;">
          <p class="muted">Use this form to appeal a violation notice. A board member will review within 10 business days.</p>
          <button class="btn btn--primary" style="margin-top:12px;">Start appeal</button>
        </div>
      }
    </div>
  `
})
export class ViolationsComponent {
  private svc = inject(CommunityService);

  activeTab = signal<'open' | 'closed' | 'rules' | 'appeal'>('open');

  openViolations   = computed(() => this.svc.getViolations('open'));
  closedViolations = computed(() => this.svc.getViolations('closed'));

  get openCount()   { return this.openViolations().length; }
  get closedCount() { return this.closedViolations().length; }

  get lastClosedDate(): string {
    return this.closedViolations()[0]?.date ?? '';
  }

  get mostCommon(): string {
    const counts = this.svc.getViolations().reduce((acc, v) => {
      acc[v.category] = (acc[v.category] ?? 0) + 1;
      return acc;
    }, {} as Record<string, number>);
    return Object.entries(counts).sort((a, b) => b[1] - a[1])[0]?.[0] ?? '—';
  }

  get mostCommonCount(): number {
    const counts = this.svc.getViolations().reduce((acc, v) => {
      acc[v.category] = (acc[v.category] ?? 0) + 1;
      return acc;
    }, {} as Record<string, number>);
    return Object.entries(counts).sort((a, b) => b[1] - a[1])[0]?.[1] ?? 0;
  }
}
