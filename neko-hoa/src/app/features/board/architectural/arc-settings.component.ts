import { Component, effect, inject, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ArcSettings, ArchitecturalService } from '../../../core/services/architectural.service';
import { arcContext } from './arc-context';

// 027 FR-029/FR-030: the community manager sets the architectural review rules to follow the association's
// governing documents. Changes apply to applications received afterwards.
@Component({
  selector: 'app-arc-settings',
  standalone: true,
  imports: [FormsModule],
  template: `
    <div class="page-header"><h1 class="page-title">ARC settings</h1></div>
    @if (form(); as f) {
      <form class="card as" (ngSubmit)="save()" novalidate>
        <p class="muted">Set these to match your governing documents. Changes apply to applications received from now on.</p>
        <div class="grid-2 as__grid">
          <label>
            <div class="field-label">Review period (days)</div>
            <input class="input" type="number" min="1" max="365" name="reviewPeriodDays" [(ngModel)]="f.reviewPeriodDays" required />
          </label>
          <label>
            <div class="field-label">Reminder to the board (days before due, 0 = off)</div>
            <input class="input" type="number" min="0" max="30" name="reminderDays" [(ngModel)]="f.reminderDays" required />
          </label>
          <label>
            <div class="field-label">When the review period ends without a decision</div>
            <select class="input" name="lapseRule" [(ngModel)]="f.lapseRule">
              <option value="FlagOverdueOnly">Flag as overdue only</option>
              <option value="DeemedApproved">Deemed approved</option>
              <option value="DeemedDenied">Deemed denied</option>
            </select>
          </label>
          <label>
            <div class="field-label">How a decision is reached</div>
            <select class="input" name="decisionRule" [(ngModel)]="f.decisionRule">
              <option value="MajorityOfMembers">Majority of board members</option>
              <option value="MajorityOfVotesCastWithQuorum">Majority of votes cast (with a quorum)</option>
            </select>
          </label>
          <label>
            <div class="field-label">Time zone</div>
            <input class="input" name="timeZoneId" [(ngModel)]="f.timeZoneId" required />
          </label>
        </div>
        <label>
          <div class="field-label">Formal disapproval statement (included in every denial email)</div>
          <textarea class="input as__statement" rows="3" maxlength="1000" name="formalDisapprovalStatement"
                    [(ngModel)]="f.formalDisapprovalStatement" required></textarea>
        </label>
        @if (error()) { <p class="as__error" role="alert">{{ error() }}</p> }
        @if (saved()) { <p class="as__ok" role="status">Settings saved.</p> }
        <button type="submit" class="btn btn--primary" [disabled]="busy()">Save settings</button>
      </form>
    } @else if (error()) {
      <p class="as__error" role="alert">{{ error() }}</p>
    }
  `,
  styles: [`
    .as__grid { gap: 12px; margin-bottom: 12px; }
    .as__grid .input, .as__statement { width: 100%; }
    .as__error { color: var(--warn); font-size: 12px; }
    .as__ok { color: var(--ink-soft); font-size: 12px; }
  `]
})
export class ArcSettingsComponent {
  private arc = inject(ArchitecturalService);
  private readonly ctx = arcContext();

  readonly form = signal<ArcSettings | null>(null);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly saved = signal(false);

  constructor() {
    effect(() => {
      const cid = this.ctx.communityId();
      if (cid) untracked(() => void this.load(cid));
    }, { allowSignalWrites: true });
  }

  private async load(cid: string): Promise<void> {
    try {
      this.form.set({ ...(await this.arc.getSettings(cid)) });
    } catch {
      this.error.set('Settings could not be loaded.');
    }
  }

  /** Client-side checks mirror the server's (FR-029); the server stays authoritative. */
  validate(f: ArcSettings): string | null {
    if (!Number.isInteger(f.reviewPeriodDays) || f.reviewPeriodDays < 1 || f.reviewPeriodDays > 365)
      return 'Review period must be between 1 and 365 days.';
    if (!Number.isInteger(f.reminderDays) || f.reminderDays < 0 || f.reminderDays > 30)
      return 'Reminder days must be between 0 and 30.';
    if (!f.timeZoneId?.trim()) return 'Time zone is required.';
    if (!f.formalDisapprovalStatement?.trim()) return 'The formal disapproval statement is required.';
    return null;
  }

  async save(): Promise<void> {
    const cid = this.ctx.communityId();
    const f = this.form();
    if (!cid || !f || this.busy()) return;
    this.saved.set(false);
    const problem = this.validate(f);
    if (problem) {
      this.error.set(problem);
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    try {
      this.form.set({ ...(await this.arc.putSettings(cid, { ...f, updatedAt: undefined })) });
      this.saved.set(true);
    } catch (e: any) {
      this.error.set(e?.error?.message ?? 'Settings could not be saved.');
    } finally {
      this.busy.set(false);
    }
  }
}
