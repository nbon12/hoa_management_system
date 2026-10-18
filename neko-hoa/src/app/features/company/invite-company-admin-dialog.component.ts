import { Component, EventEmitter, Output, ViewChild, computed, inject, signal } from '@angular/core';
import { CompanyService } from '../../core/services/company.service';
import { DialogComponent } from '../../shared/dialog.component';
import { RATE_LIMITED_MESSAGE, SAVE_FAILED_MESSAGE, refusalCodeOf, refusalMessage, serverMessageOf } from '../../shared/refusal-messages';

const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

/**
 * 030 US7 / FR-037 / brief 09 8.09.b–e: "Invite a Company Administrator". The email field is the
 * only input: nothing looks up, suggests or confirms who owns the email (FR-018), and an
 * invitation is always sent. `SELF_GRANT_NOT_ALLOWED`, `ALREADY_MEMBER` and `INVITATION_PENDING`
 * show under the field with the input kept; the last offers "Resend invitation".
 */
@Component({
  selector: 'app-invite-company-admin-dialog',
  standalone: true,
  imports: [DialogComponent],
  template: `
    <app-dialog heading="Invite a Company Administrator" (dismiss)="cancel()">
      @if (topError()) { <p class="dlg-refusal field-error" role="alert" tabindex="-1">{{ topError() }}</p> }
      <p>A Company Administrator sets up the company's business in this app. The role gives no board powers in any community.</p>
      <div class="dlg-form">
        <div class="ica__field">
          <label class="field-label" for="ica-email">Email address (required)</label>
          <p class="field-help" id="ica-email-help">Until they accept, the invitation shows only this email address.</p>
          <input class="field" id="ica-email" type="email" autocomplete="off" [value]="email()" [disabled]="busy()"
                 [attr.aria-invalid]="emailError() ? 'true' : null"
                 [attr.aria-describedby]="emailError() ? 'ica-email-help ica-email-error' : 'ica-email-help'"
                 (input)="onEmail($any($event.target).value)" />
          @if (emailError()) {
            <p class="field-error" id="ica-email-error" role="alert">{{ emailError() }}</p>
            @if (pendingId()) { <button type="button" class="btn" [disabled]="busy()" (click)="resendPending()">Resend invitation</button> }
          }
        </div>
        <p class="muted">We always send an invitation, even if this email already has an account. They become a Company Administrator only if they accept.</p>
        @if (summary()) { <p>{{ summary() }}</p> }
      </div>
      <div class="dlg-actions">
        @if (refused()) {
          <button type="button" class="btn btn--primary" (click)="closeAfterRefusal.emit()">Close</button>
        } @else {
          <button type="button" class="btn btn--primary" [disabled]="busy()" (click)="submit()">{{ busy() ? 'Sending…' : 'Send invitation' }}</button>
          <button type="button" class="btn" [disabled]="busy()" (click)="cancel()">Cancel</button>
          @if (rateLimited()) { <span class="field-error" role="alert">{{ rateLimitedMessage }}</span> }
        }
      </div>
    </app-dialog>
  `,
  styles: [`.ica__field { display: flex; flex-direction: column; gap: 4px; align-items: flex-start; } .ica__field .field { width: 100%; }`],
})
export class InviteCompanyAdminDialogComponent {
  /** The toast's message. */
  @Output() readonly done = new EventEmitter<string>();
  @Output() readonly cancelled = new EventEmitter<void>();
  @Output() readonly closeAfterRefusal = new EventEmitter<void>();

  @ViewChild(DialogComponent) private dialog?: DialogComponent;

  private company = inject(CompanyService);

  readonly rateLimitedMessage = RATE_LIMITED_MESSAGE;
  readonly email = signal('');
  readonly emailError = signal<string | null>(null);
  readonly pendingId = signal<string | null>(null);
  readonly topError = signal<string | null>(null);
  readonly refused = signal(false);
  readonly rateLimited = signal(false);
  readonly busy = signal(false);

  readonly summary = computed(() => {
    const email = this.email().trim();
    return EMAIL.test(email)
      ? `We'll email ${email} an invitation to be a Company Administrator of the management company. The link works once, for 72 hours, and only for that email address.`
      : null;
  });

  onEmail(value: string): void {
    this.email.set(value);
    this.emailError.set(null);
    this.pendingId.set(null);
  }

  cancel(): void {
    if (!this.busy()) this.cancelled.emit();
  }

  async submit(): Promise<void> {
    if (this.busy()) return;
    const email = this.email().trim();
    this.topError.set(null);
    this.rateLimited.set(false);
    if (!EMAIL.test(email)) {
      this.emailError.set('Enter a valid email address.');
      return;
    }
    this.busy.set(true);
    try {
      await this.company.inviteAdministrator(email);
      this.done.emit(`Invitation sent to ${email}.`);
    } catch (err) {
      this.onError(err, email);
    } finally {
      this.busy.set(false);
    }
  }

  async resendPending(): Promise<void> {
    const id = this.pendingId();
    if (!id || this.busy()) return;
    this.busy.set(true);
    try {
      await this.company.resendInvitation(id);
      this.done.emit(`Invitation sent again to ${this.email().trim()}. The earlier link no longer works.`);
    } catch (err) {
      this.onError(err, this.email().trim());
    } finally {
      this.busy.set(false);
    }
  }

  private onError(err: unknown, email: string): void {
    const code = refusalCodeOf(err);
    switch (code) {
      case 'SELF_GRANT_NOT_ALLOWED':
      case 'ALREADY_MEMBER':
        this.emailError.set(refusalMessage(code, { variant: 'companyAdmins', email }));
        return;
      case 'INVITATION_PENDING':
        this.emailError.set(refusalMessage(code, { variant: 'companyAdmins', email }));
        this.pendingId.set((err as { error?: { invitationId?: string } })?.error?.invitationId ?? null);
        return;
      case 'VALIDATION_ERROR':
        this.emailError.set(serverMessageOf(err) ?? 'Enter a valid email address.');
        return;
      case 'RATE_LIMITED':
        this.rateLimited.set(true);
        return;
      case null:
        this.topError.set(SAVE_FAILED_MESSAGE);
        break;
      default:
        this.topError.set(refusalMessage(code, { surface: 'action', audience: 'companyAdmin' }));
        this.refused.set(true);
    }
    this.dialog?.focus('.dlg-refusal');
  }
}
