import { Component, inject, signal, OnInit } from '@angular/core';
import { PropertyService } from '../../../core/services/property.service';
import { CommunityService } from '../../../core/services/community.service';
import { DirectoryField } from '../../../core/models';

interface Neighbor { address: string; name: string | null; email: string | null; phone: string | null; }
interface DirectoryResponse { neighbors: Neighbor[]; totalSharing: number; totalHouseholds: number; }

@Component({
  selector: 'app-directory',
  standalone: true,
  template: `
    <div class="page-header">
      <h1 class="page-title">Directory</h1>
      <span class="muted" style="margin-left:10px;">Sakura Heights · {{ dirResponse()?.totalHouseholds ?? 248 }} households</span>
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
          <div style="font-size:18px;font-weight:600;margin-top:6px;">Your listing</div>
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

    <!-- Community directory -->
    <div class="card" style="margin-top:16px;">
      <div style="display:flex;align-items:center;gap:12px;margin-bottom:12px;">
        <div class="section-title" style="margin:0;">Neighbor Directory</div>
        @if (dirResponse()) {
          <span class="badge badge--green">{{ dirResponse()!.totalSharing }} sharing</span>
        }
      </div>

      @if (!dirResponse()) {
        <p class="muted" style="font-size:12px;">Loading…</p>
      } @else if (dirResponse()!.neighbors.length === 0) {
        <p class="muted" style="font-size:12px;">No neighbors have opted into the directory yet.</p>
      } @else {
        <table class="data-table">
          <thead>
            <tr>
              <th>Address</th>
              <th>Name</th>
              <th>Email</th>
              <th>Phone</th>
            </tr>
          </thead>
          <tbody>
            @for (n of dirResponse()!.neighbors; track n.address) {
              <tr>
                <td>{{ n.address }}</td>
                <td>{{ n.name ?? '—' }}</td>
                <td>{{ n.email ?? '—' }}</td>
                <td>{{ n.phone ?? '—' }}</td>
              </tr>
            }
          </tbody>
        </table>
      }
    </div>
  `
})
export class DirectoryComponent implements OnInit {
  private svc     = inject(PropertyService);
  private commSvc = inject(CommunityService);

  fields      = signal<DirectoryField[]>([]);
  dirResponse = signal<DirectoryResponse | null>(null);

  sharedFields() { return this.fields().filter(f => f.shared); }

  async ngOnInit() {
    const [fields, dir] = await Promise.all([
      this.svc.getDirectoryFields(),
      this.commSvc.getCommunityDirectory(),
    ]);
    this.fields.set(fields);
    this.dirResponse.set(dir);
  }

  async toggle(key: string) {
    const current = this.fields().find(f => f.key === key);
    if (!current) return;
    const newShared = !current.shared;
    this.fields.set(this.fields().map(f => f.key === key ? { ...f, shared: newShared } : f));
    try {
      await this.svc.toggleDirectoryField(key, newShared);
    } catch {
      this.fields.set(this.fields().map(f => f.key === key ? { ...f, shared: !newShared } : f));
    }
  }
}
