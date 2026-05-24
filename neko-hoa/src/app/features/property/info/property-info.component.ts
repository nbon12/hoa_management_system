import { Component, inject } from '@angular/core';
import { PropertyService } from '../../../core/services/property.service';
import { CurrencyPipe } from '@angular/common';

@Component({
  selector: 'app-property-info',
  standalone: true,
  imports: [CurrencyPipe],
  template: `
    <div class="page-header">
      <h1 class="page-title">Property <span class="hand">info</span></h1>
      <span class="pill pill--ok" style="margin-left:10px;">✓ Active</span>
      <div class="page-header__actions">
        <button class="btn btn--ghost">Request changes</button>
      </div>
    </div>

    <div class="grid-2">
      <div class="card">
        <div class="field-label">Account number</div>
        <div class="mono" style="font-size:14px;font-weight:600;margin-top:4px;">{{ prop.accountNumber }}</div>
      </div>
      <div class="card">
        <div class="field-label">Annual assessment</div>
        <div class="mono" style="font-size:18px;margin-top:4px;">{{ prop.annualAssessment | currency }}</div>
        <div class="muted">due {{ prop.assessmentDueDay }}st of each month</div>
      </div>
    </div>

    <!-- Property details -->
    <div class="card">
      <div class="section-title">🏠 Property details</div>
      <div class="grid-3" style="gap:14px 24px;margin-top:10px;">
        @for (field of detailFields; track field.label) {
          <div>
            <div class="field-label">{{ field.label }}</div>
            <div style="font-size:13px;font-weight:500;">{{ field.value || '—' }}</div>
          </div>
        }
      </div>
    </div>

    <!-- Assessment rules -->
    <div class="card">
      <div class="section-title">📑 Assessment rules · {{ prop.fiscalYear }}</div>
      <table class="data-table" style="margin-top:10px;">
        <thead>
          <tr><th>Rule type</th><th>Rule</th></tr>
        </thead>
        <tbody>
          <tr>
            <td>Regular assessment</td>
            <td><b>{{ prop.monthlyAssessment | currency }}</b> due the <b>{{ prop.assessmentDueDay }}st</b> of each <b>month</b>.</td>
          </tr>
          <tr>
            <td>Late fee</td>
            <td><b>{{ prop.lateFeeAmount | currency }}</b> per missed period, applied {{ prop.lateFeeGraceDays }} days after the due date.</td>
          </tr>
          <tr>
            <td>Finance charge</td>
            <td>{{ prop.financeChargeRate }}% per annum, simple compounding, 365-day year.</td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Late fee timeline -->
    <div class="card card--dashed">
      <div class="section-title">Late fee timeline · {{ prop.fiscalYear }}</div>
      <div style="position:relative;padding:24px 0 36px;">
        <div style="height:2px;background:var(--line);position:relative;">
          @for (m of months; track m; let i = $index) {
            <div style="position:absolute;transform:translateX(-50%);"
                 [style.left]="(i / 11 * 100) + '%'"
                 [style.top]="'-4px'">
              <div style="width:10px;height:10px;border-radius:50%;background:var(--rose);border:1.5px solid var(--ink);"></div>
              <div style="position:absolute;top:14px;left:50%;transform:translateX(-50%);font-size:10px;color:var(--ink-soft);white-space:nowrap;">
                {{ m }}
              </div>
            </div>
          }
        </div>
      </div>
      <p class="muted" style="font-size:11px;">+{{ prop.lateFeeAmount | currency }} added each month past 30 days late.</p>
    </div>
  `
})
export class PropertyInfoComponent {
  prop = inject(PropertyService).getProperty();

  months = ['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec'];

  get detailFields() {
    return [
      { label: 'Account #',      value: this.prop.accountNumber },
      { label: 'Community ID',   value: this.prop.communityId },
      { label: 'Check digit / PIN', value: '0' },
      { label: 'Lot',            value: this.prop.lot },
      { label: 'Phase',          value: this.prop.phase },
      { label: 'Section',        value: this.prop.section },
      { label: 'Block',          value: this.prop.block },
      { label: 'Fiscal year',    value: String(this.prop.fiscalYear) },
      { label: 'Year built',     value: String(this.prop.yearBuilt) },
    ];
  }
}
