import { Component, computed, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ArcDetail, ArcOutcomeRequest } from '../../../core/services/architectural.service';

// 027 US6 / FR-025–FR-026: the community manager records a reached decision. A denial needs a reason
// for the owner; conditions apply to approvals only; the owner wording is chosen only for a denial
// by default (lapse) — otherwise it follows the board's votes. Shows the owner email status and Resend.
@Component({
  selector: 'app-arc-record-outcome',
  standalone: true,
  imports: [FormsModule],
  template: `
    <div class="card ro">
      @if (detail().status === 'DecisionReached') {
        <div class="section-title" style="margin:0 0 8px;">Record the outcome</div>
        @if (isDenial()) {
          <label class="field-label" for="ro-reason">What would need to change for approval?</label>
          <textarea id="ro-reason" class="input ro__text" rows="3" maxlength="2000" [(ngModel)]="reason" name="reason"
                    required aria-required="true"></textarea>
          @if (isLapse()) {
            <label class="field-label" for="ro-wording">Owner sees</label>
            <select id="ro-wording" class="input" [(ngModel)]="wording" name="wording">
              <option value="RevisionsRequested">Revisions requested</option>
              <option value="Denied">Denied</option>
            </select>
          }
        } @else {
          <label class="field-label" for="ro-conditions">Conditions of approval (optional)</label>
          <textarea id="ro-conditions" class="input ro__text" rows="2" maxlength="2000" [(ngModel)]="conditions" name="conditions"></textarea>
        }
        @if (error()) { <p class="ro__error" role="alert">{{ error() }}</p> }
        <button type="button" class="btn btn--primary" [disabled]="busy()" (click)="submit()">Record outcome and email owner</button>
      } @else {
        <div class="section-title" style="margin:0 0 8px;">Owner email</div>
        <p class="muted" style="margin:0 0 8px;">{{ emailStatusText() }}</p>
        @if (detail().ownerEmailStatus === 'Failed') {
          <button type="button" class="btn" [disabled]="busy()" (click)="resend.emit()">Resend email</button>
        }
      }
    </div>
  `,
  styles: [`
    .ro__text { width: 100%; margin-bottom: 8px; }
    .ro__error { color: var(--warn); font-size: 12px; }
  `]
})
export class RecordOutcomeComponent {
  readonly detail = input.required<ArcDetail>();
  readonly busy = input<boolean>(false);
  readonly recorded = output<ArcOutcomeRequest>();
  readonly resend = output<void>();

  readonly error = signal<string | null>(null);
  reason = '';
  conditions = '';
  wording: 'RevisionsRequested' | 'Denied' = 'RevisionsRequested';

  readonly isDenial = computed(() => this.detail().decision?.outcome === 'Denied');
  readonly isLapse = computed(() => this.detail().decision?.source === 'Lapse');

  emailStatusText(): string {
    switch (this.detail().ownerEmailStatus) {
      case 'Sent': return 'The owner was emailed the outcome.';
      case 'Pending': return 'The owner email is queued.';
      case 'Failed': return 'The owner email could not be sent.';
      case 'NoOwnerEmail': return 'No owner email on file — notify the owner another way.';
      default: return '';
    }
  }

  submit(): void {
    if (this.isDenial()) {
      const reason = this.reason.trim();
      if (!reason) {
        this.error.set('A reason for the owner is required for a denial.');
        return;
      }
      this.error.set(null);
      this.recorded.emit(this.isLapse() ? { ownerReason: reason, wording: this.wording } : { ownerReason: reason });
    } else {
      this.error.set(null);
      this.recorded.emit(this.conditions.trim() ? { conditionsOfApproval: this.conditions.trim() } : {});
    }
  }
}
