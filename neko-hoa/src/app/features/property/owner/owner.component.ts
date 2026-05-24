import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { PropertyService } from '../../../core/services/property.service';
import { Owner } from '../../../core/models';

@Component({
  selector: 'app-owner',
  standalone: true,
  imports: [FormsModule, DatePipe],
  template: `
    <div class="page-header">
      <h1 class="page-title">Owner profile</h1>
      <div class="page-header__actions">
        @if (editing()) {
          <button class="btn btn--ghost" (click)="cancel()">Cancel</button>
          <button class="btn btn--primary" (click)="save()" [disabled]="saving()">
            @if (saving()) { <span class="spinner"></span> } @else { Save changes }
          </button>
        } @else {
          <button class="btn" (click)="editing.set(true)">Edit profile</button>
        }
      </div>
    </div>

    @if (saved()) {
      <div class="alert alert--success"><span>✓</span> Profile updated.</div>
    }

    <!-- Profile header card -->
    <div class="card" style="display:grid;grid-template-columns:auto 1fr auto;gap:18px;align-items:center;">
      <div class="avatar avatar--lg">{{ form.firstName[0] }}{{ form.lastName[0] }}</div>
      <div>
        <div style="font-size:18px;font-weight:600;">{{ form.firstName }} {{ form.lastName }}</div>
        <div class="muted">Owner · voting rights enabled · member since 2021</div>
        <div style="display:flex;gap:6px;margin-top:6px;flex-wrap:wrap;">
          <span class="pill">📧 {{ form.email }}</span>
          <span class="pill">📞 {{ form.phone || '(919) ___-____' }}</span>
        </div>
      </div>
    </div>

    <div class="grid-2">
      <!-- Account details -->
      <div class="card">
        <div style="display:flex;align-items:center;gap:8px;">
          <div class="section-title" style="margin:0;">👤 Account details</div>
          <span class="pill pill--ok" style="margin-left:auto;">active</span>
        </div>
        <p class="muted" style="margin:2px 0 14px;">Ownership info on file with the association.</p>
        <div class="grid-2" style="gap:10px 20px;">
          @for (f of accountFields; track f.key) {
            <div>
              <div class="field-label">{{ f.label }}</div>
              @if (editing() && f.editable) {
                <input class="field" [type]="f.type || 'text'" [name]="f.key"
                       [ngModel]="getField(f.key)"
                       (ngModelChange)="setField(f.key, $event)">
              } @else {
                <div class="field field--dashed" style="color:var(--ink);">
                  {{ getFieldDisplay(f.key) || '—' }}
                </div>
              }
            </div>
          }
        </div>
      </div>

      <div class="flex-col">
        <!-- Mailing preferences -->
        <div class="card">
          <div class="section-title">Mailing preferences</div>
          <div style="display:flex;flex-direction:column;gap:10px;margin-top:6px;">
            <label style="display:flex;align-items:center;gap:10px;cursor:pointer;">
              <span class="toggle" [class.toggle--on]="form.mailingToProperty"
                    (click)="form.mailingToProperty = !form.mailingToProperty"></span>
              <div>
                <div style="font-weight:500;">Mail to property</div>
                <div class="muted" style="font-size:11px;">{{ prop.address }}</div>
              </div>
            </label>
            <label style="display:flex;align-items:center;gap:10px;cursor:pointer;">
              <span class="toggle" [class.toggle--on]="form.paperlessStatements"
                    (click)="form.paperlessStatements = !form.paperlessStatements"></span>
              <div>
                <div style="font-weight:500;">Email statements (paperless)</div>
                <div class="muted" style="font-size:11px;">Faster · save paper</div>
              </div>
            </label>
            <label style="display:flex;align-items:center;gap:10px;cursor:pointer;">
              <span class="toggle" [class.toggle--on]="form.smsReminders"
                    (click)="form.smsReminders = !form.smsReminders"></span>
              <div>
                <div style="font-weight:500;">SMS reminders</div>
                <div class="muted" style="font-size:11px;">Day before due</div>
              </div>
            </label>
          </div>
        </div>

        <!-- Co-owners -->
        <div class="card">
          <div class="section-title">Co-owners</div>
          <div class="card card--dashed" style="padding:14px;display:flex;align-items:center;gap:10px;color:var(--ink-soft);">
            <div class="ph" style="width:36px;height:36px;border-radius:50%;">+</div>
            Add a co-owner
          </div>
        </div>
      </div>
    </div>

    <!-- Address history -->
    <div class="card">
      <div class="section-title">Address history</div>
      <table class="data-table" style="margin-top:10px;">
        <thead>
          <tr><th>Date</th><th>Event</th><th>Address</th></tr>
        </thead>
        <tbody>
          @for (h of addressHistory; track h.date) {
            <tr>
              <td>{{ h.date | date:'MMM d, yyyy' }}</td>
              <td><span class="pill" [class.pill--ok]="h.event === 'created'">{{ h.event }}</span></td>
              <td>{{ h.address }}</td>
            </tr>
          }
        </tbody>
      </table>
    </div>
  `
})
export class OwnerComponent {
  private svc = inject(PropertyService);
  prop           = this.svc.getProperty();
  addressHistory = this.svc.getAddressHistory();

  editing = signal(false);
  saving  = signal(false);
  saved   = signal(false);

  form: Owner = { ...this.svc.getOwner() };
  private _original: Owner = { ...this.form };

  accountFields = [
    { key: 'firstName',       label: 'First name',       editable: true },
    { key: 'lastName',        label: 'Last name',        editable: true },
    { key: 'email',           label: 'Email',            editable: true, type: 'email' },
    { key: 'phone',           label: 'Phone',            editable: true, type: 'tel' },
    { key: 'accountNumber',   label: 'Account #',        editable: false },
    { key: 'communityName',   label: 'Community',        editable: false },
    { key: 'propertyAddress', label: 'Property address', editable: false },
    { key: 'votingRights',    label: 'Voting rights',    editable: false },
  ];

  getField(key: string): string {
    return String((this.form as any)[key] ?? '');
  }
  getFieldDisplay(key: string): string {
    const v = (this.form as any)[key];
    if (typeof v === 'boolean') return v ? 'Yes' : 'No';
    return String(v ?? '');
  }
  setField(key: string, value: string) {
    (this.form as any)[key] = value;
  }

  cancel() {
    this.form = { ...this._original };
    this.editing.set(false);
  }

  async save() {
    this.saving.set(true);
    await this.svc.updateOwner(this.form);
    this._original = { ...this.form };
    this.saving.set(false);
    this.editing.set(false);
    this.saved.set(true);
    setTimeout(() => this.saved.set(false), 3000);
  }
}
