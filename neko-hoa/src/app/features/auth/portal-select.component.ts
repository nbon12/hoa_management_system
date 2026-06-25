import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

const PORTALS = [
  { t: 'Resident',     d: 'Your account, payments, requests.',  big: true  },
  { t: 'Board / Mgmt', d: 'Manage the community.',              big: false },
  { t: 'Vendor',       d: 'Invoices & work orders.',            big: false },
  { t: 'Closing',      d: 'Estoppels & statements.',            big: false },
  { t: 'Attorney',     d: 'Referred accounts.',                 big: false },
];

@Component({
  selector: 'app-portal-select',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div style="min-height:100vh;display:flex;flex-direction:column;background:var(--paper);">
      <header style="padding:18px 28px;border-bottom:1.5px dashed var(--line);display:flex;align-items:center;gap:10px;">
        <span class="logo-mark"></span>
        <span class="hand" style="font-size:24px;font-weight:700;">NekoHOA</span>
        <span style="margin-left:auto;font-size:12px;color:var(--ink-soft);">Portal selection</span>
      </header>

      <div style="flex:1;display:grid;grid-template-columns:1.2fr 1fr;">
        <!-- Left hero -->
        <div style="padding:48px 40px;background:var(--pink);display:flex;flex-direction:column;justify-content:center;">
          <h1 style="font-size:36px;line-height:1.1;margin:8px 0 12px;font-weight:600;">
            Pick the <span class="hand" style="color:var(--rose);">door</span> you came through.
          </h1>
          <p style="color:var(--ink-soft);max-width:320px;">
            Five portals, one community. Most folks want the Resident door.
          </p>
          <picture>
            <source srcset="assets/pink-home.webp" type="image/webp" />
            <img src="assets/pink-home.png"
                 alt="Illustrated pink community houses"
                 width="320" height="140"
                 style="margin-top:24px;width:100%;max-width:320px;height:140px;object-fit:cover;border-radius:12px;display:block;" />
          </picture>
        </div>

        <!-- Right portal list -->
        <div style="padding:48px 32px;display:flex;flex-direction:column;gap:10px;justify-content:center;">
          @for (portal of portals; track portal.t) {
            <a [routerLink]="portal.big ? '/login' : '/login'"
               class="card"
               style="display:flex;align-items:center;gap:14px;padding:14px 16px;cursor:pointer;text-decoration:none;"
               [style.background]="portal.big ? 'var(--lav)' : 'var(--paper)'"
               [style.border-color]="portal.big ? 'var(--ink)' : 'var(--line)'">
              <div class="ph" style="width:36px;height:36px;flex-shrink:0;border-radius:8px;">◇</div>
              <div style="flex:1;">
                <div style="font-weight:600;">
                  {{ portal.t }}
                  @if (portal.big) { <span class="pill" style="margin-left:6px;">most popular</span> }
                </div>
                <div class="muted">{{ portal.d }}</div>
              </div>
              <span style="color:var(--rose);font-size:18px;">→</span>
            </a>
          }
        </div>
      </div>
    </div>
  `
})
export class PortalSelectComponent {
  portals = PORTALS;
}
