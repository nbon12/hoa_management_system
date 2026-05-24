import { Component, inject, signal } from '@angular/core';
import { PropertyService } from '../../../core/services/property.service';
import { DirectoryField } from '../../../core/models';

@Component({
  selector: 'app-directory',
  standalone: true,
  template: `
    <div class="page-header">
      <h1 class="page-title">Directory</h1>
      <span class="muted" style="margin-left:10px;">Sakura Heights · 248 households</span>
    </div>

    <!-- Info banner -->
    <div class="card card--pink" style="display:flex;gap:10px;">
      <span>ⓘ</span>
      <p style="margin:0;font-size:12px;">
        Nothing is shared by default. You can turn fields on individually below.
        Changes save automatically.
      </p>
    </div>

    <div class="grid-2">
      <!-- What I share -->
      <div class="card">
        <div class="section-title">What I share</div>
        <p class="muted" style="font-size:11px;margin-bottom:10px;">Toggle each field. Changes save automatically.</p>
        <table class="data-table">
          <thead>
            <tr><th>Field</th><th>Value</th><th>Shared?</th></tr>
          </thead>
          <tbody>
            @for (f of fields(); track f.key) {
              <tr>
                <td>{{ f.label }}</td>
                <td class="muted">{{ f.value }}</td>
                <td>
                  <span class="toggle" [class.toggle--on]="f.shared"
                        (click)="toggle(f.key)"></span>
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>

      <div class="flex-col">
        <!-- Directory preview -->
        <div class="card card--lav">
          <div class="field-label">Directory preview</div>
          <div style="font-size:18px;font-weight:600;margin-top:6px;">714 Keystone Park Dr</div>
          @if (sharedFields().length === 0) {
            <p class="muted" style="font-size:11px;margin-top:6px;">
              The owner &amp; contact info has not been shared with the directory.
            </p>
          } @else {
            <div style="margin-top:8px;display:flex;flex-direction:column;gap:4px;font-size:12px;">
              @for (f of sharedFields(); track f.key) {
                <div><span class="muted">{{ f.label }}:</span> {{ f.value }}</div>
              }
            </div>
          }
        </div>

        <!-- Privacy promise -->
        <div class="card card--dashed">
          <div class="section-title">🔒 Privacy promise</div>
          <ul style="margin:0;padding-left:18px;color:var(--ink-soft);font-size:12px;line-height:1.7;">
            <li>Only verified residents of <b>Sakura Heights</b> see the directory.</li>
            <li>We never share your info with third parties.</li>
            <li>You can revoke any time — changes are immediate.</li>
          </ul>
        </div>
      </div>
    </div>
  `
})
export class DirectoryComponent {
  private svc = inject(PropertyService);
  fields = signal<DirectoryField[]>(this.svc.getDirectoryFields());

  sharedFields() { return this.fields().filter(f => f.shared); }

  toggle(key: string) {
    this.fields.set(this.svc.toggleDirectoryField(key, this.fields()));
  }
}
