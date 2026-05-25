import { Component, inject, signal, OnInit } from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../core/services/auth.service';
import { PropertyService } from '../core/services/property.service';
import { Property } from '../core/models';

interface NavGroup {
  group: string | null;
  items: { label: string; route: string }[];
}

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <div class="shell">
      <!-- Top bar -->
      <header class="shell__top">
        <div class="shell__logo">
          <span class="logo-mark"></span>
          <span class="hand" style="font-size:22px;font-weight:700;">NekoHOA</span>
        </div>
        <div style="margin-left:auto;display:flex;align-items:center;gap:8px;">
          <button class="pill" style="cursor:pointer;background:var(--pink-2);" (click)="logout()">
            🔔 3
          </button>
          <div class="avatar">{{ user()?.initials }}</div>
          <button class="btn btn--ghost" (click)="logout()" style="font-size:11px;">Sign out</button>
        </div>
      </header>

      <!-- Property strip -->
      <div class="shell__strip">
        <span class="pin-icon"></span>
        <b>{{ property()?.communityName }}</b>
        <span>· {{ property()?.address }}</span>
        <span class="mono" style="margin-left:auto;font-size:11px;color:var(--ink-mute);">
          {{ property()?.accountNumber }}
        </span>
      </div>

      <!-- Body: sidebar + content -->
      <div class="shell__body">
        <aside class="shell__side">
          @for (group of navGroups; track group.group) {
            @if (group.group) {
              <div class="shell__side-group">{{ group.group }}</div>
            }
            @for (item of group.items; track item.route) {
              <a class="shell__side-item"
                 [routerLink]="item.route"
                 routerLinkActive="shell__side-item--active">
                {{ item.label }}
              </a>
            }
          }
        </aside>

        <main class="shell__content">
          <router-outlet />
        </main>
      </div>
    </div>
  `,
  styles: [`
    .shell {
      display: flex; flex-direction: column; height: 100vh; overflow: hidden;
      font-family: 'Geist', system-ui, sans-serif;
    }

    /* top bar */
    .shell__top {
      display: flex; align-items: center; gap: 16px;
      padding: 14px 22px;
      border-bottom: 1.5px dashed var(--line);
      background: linear-gradient(180deg, var(--pink) 0%, var(--paper) 100%);
      flex-shrink: 0;
    }
    .shell__logo {
      display: flex; align-items: center; gap: 8px; color: var(--ink);
    }

    /* property strip */
    .shell__strip {
      display: flex; align-items: center; gap: 10px;
      padding: 10px 22px;
      background: var(--lav); border-bottom: 1.5px solid var(--line-soft);
      font-size: 12px; color: var(--ink-soft); flex-shrink: 0;
      b { color: var(--ink); font-weight: 600; }
    }

    /* sidebar + content */
    .shell__body { display: flex; flex: 1; min-height: 0; }

    .shell__side {
      width: 180px; background: var(--lav);
      border-right: 1.5px dashed var(--line);
      padding: 18px 10px; display: flex; flex-direction: column; gap: 2px;
      flex-shrink: 0; overflow-y: auto;
    }
    .shell__side-group {
      font-size: 10.5px; text-transform: uppercase; letter-spacing: .08em;
      color: var(--ink-mute); padding: 10px 12px 4px; font-weight: 600;
    }
    .shell__side-item {
      display: block; padding: 8px 12px; border-radius: 10px;
      color: var(--ink-soft); font-size: 12.5px; text-decoration: none;
      transition: background .1s; border: 1.5px solid transparent;
      &:hover { background: var(--lav-2); }
    }
    .shell__side-item--active {
      background: var(--paper); color: var(--ink); font-weight: 500;
      border-color: var(--ink);
    }

    .shell__content {
      flex: 1; overflow: auto; padding: 22px 24px;
      display: flex; flex-direction: column; gap: 16px;
    }
  `]
})
export class ShellComponent implements OnInit {
  private auth = inject(AuthService);
  private propertySvc = inject(PropertyService);

  user = this.auth.user;
  property = signal<Property | null>(null);

  async ngOnInit() {
    try {
      this.property.set(await this.propertySvc.getProperty());
    } catch { /* user may not have a property yet */ }
  }

  navGroups: NavGroup[] = [
    { group: null, items: [
      { label: 'Dashboard', route: '/app/dashboard' },
    ]},
    { group: 'Payments', items: [
      { label: 'Statement',  route: '/app/payments/statement' },
      { label: 'One-time',   route: '/app/payments/one-time' },
      { label: 'Recurring',  route: '/app/payments/recurring' },
    ]},
    { group: 'Property', items: [
      { label: 'Info',       route: '/app/property/info' },
      { label: 'Owner',      route: '/app/property/owner' },
      { label: 'Directory',  route: '/app/property/directory' },
    ]},
    { group: 'Community', items: [
      { label: 'Announcements', route: '/app/community/announcements' },
      { label: 'Calendar',      route: '/app/community/calendar' },
      { label: 'Violations',    route: '/app/community/violations' },
      { label: 'Documents',     route: '/app/community/documents' },
    ]},
  ];

  logout() { this.auth.logout(); }
}
