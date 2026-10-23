import { Component, EventEmitter, Input, OnInit, Output, ViewChild, computed, inject, signal } from '@angular/core';
import { CommunitySettingsService } from '../../../core/services/community-settings.service';
import { DialogComponent } from '../../../shared/dialog.component';
import { ComboPickerComponent, PickerOption } from '../../../shared/combo-picker.component';
import { RATE_LIMITED_MESSAGE, SAVE_FAILED_MESSAGE, refusalCodeOf, refusalMessage } from '../../../shared/refusal-messages';
import { CommunitySettingsContext } from '../community-settings-context';

const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

/** The drawer's "Invite resident" fixes the property (brief 06 §4.5 item 6). */
export interface FixedProperty {
  propertyId: string;
  address: string;
  residentCount: number;
}

export interface InviteDone {
  propertyId: string;
  message: string;
}

/**
 * 030 US8 / FR-024a–b / brief 06 8.06.f–l: "Invite a resident". Email only: no lookup, no
 * suggestions, no account hint (FR-018). From the drawer the property is fixed; from the header
 * it is chosen from this community's properties. While Onboarding the invitation is saved as
 * Scheduled ("Save invitation"). Refusals stay under the email field with the input kept;
 * `INVITATION_PENDING` offers "Resend invitation" for a pending one.
 */
@Component({
  selector: 'app-invite-resident-dialog',
  standalone: true,
  imports: [DialogComponent, ComboPickerComponent],
  template: `
    <app-dialog heading="Invite a resident" [subheading]="communityName" (dismiss)="cancel()">
      @if (topError()) { <p class="dlg-refusal field-error" role="alert" tabindex="-1">{{ topError() }}</p> }
      <div class="dlg-form">
        @if (fixedProperty; as fp) {
          <div>
            <p>Property: {{ fp.address }}</p>
            @if (fp.residentCount) {
              <p class="muted">This property has {{ fp.residentCount }} {{ fp.residentCount === 1 ? 'resident' : 'residents' }}. Whoever accepts is added next to them.</p>
            }
          </div>
        } @else {
          <app-combo-picker label="Property (required)" placeholder="Search by address" [helper]="'Only properties in ' + communityName + ' are listed.'"
                            [noun]="['property', 'properties']" [noMatchMessage]="noMatch" [options]="options()" [value]="propertyId()"
                            [error]="propertyError()" [disabled]="busy()" (valueChange)="chooseProperty($event)" (queryChange)="searchProperties($event)" />
        }
        <div class="dlg-field">
          <label class="field-label" for="ir-email">Email address (required)</label>
          <p class="field-help" id="ir-email-help">Until they accept, the invitation shows only this email address.</p>
          <input class="field" id="ir-email" type="email" autocomplete="off" [value]="email()" [disabled]="busy()"
                 [attr.aria-invalid]="emailError() ? 'true' : null"
                 [attr.aria-describedby]="emailError() ? 'ir-email-help ir-email-error' : 'ir-email-help'"
                 (input)="onEmail($any($event.target).value)" />
          @if (emailError()) {
            <p class="field-error" id="ir-email-error" role="alert">{{ emailError() }}</p>
            @if (pendingInvitationId()) {
              <button type="button" class="btn" [disabled]="busy()" (click)="resendPending()">Resend invitation</button>
            }
          }
        </div>
        @if (onboarding) {
          <p class="muted">{{ communityName }} is Onboarding, so nothing is emailed yet. The invitation is saved as Scheduled and emailed when a Company Administrator marks {{ communityName }} Active. The link then works for 72 hours.</p>
        }
        @if (summary()) { <p>{{ summary() }}</p> }
      </div>
      <div class="dlg-actions">
        @if (refused()) {
          <button type="button" class="btn btn--primary" (click)="closeAfterRefusal.emit()">Close</button>
        } @else {
          <button type="button" class="btn btn--primary" [disabled]="busy()" (click)="submit()">{{ primaryLabel() }}</button>
          <button type="button" class="btn" [disabled]="busy()" (click)="cancel()">Cancel</button>
          @if (rateLimited()) { <span class="field-error" role="alert">{{ rateLimitedMessage }}</span> }
        }
      </div>
    </app-dialog>
  `,
  styles: [`.dlg-field { display: flex; flex-direction: column; gap: 4px; align-items: flex-start; } .dlg-field .field { width: 100%; }`],
})
export class InviteResidentDialogComponent implements OnInit {
  @Input({ required: true }) communityId = '';
  @Input({ required: true }) communityName = '';
  @Input() onboarding = false;
  @Input() fixedProperty: FixedProperty | null = null;
  @Output() readonly done = new EventEmitter<InviteDone>();
  @Output() readonly cancelled = new EventEmitter<void>();
  /** After a refusal such as FORBIDDEN: the page is read again. */
  @Output() readonly closeAfterRefusal = new EventEmitter<void>();

  @ViewChild(DialogComponent) private dialog?: DialogComponent;

  private svc = inject(CommunitySettingsService);
  private ctx = inject(CommunitySettingsContext);

  readonly rateLimitedMessage = RATE_LIMITED_MESSAGE;
  readonly email = signal('');
  readonly propertyId = signal<string | null>(null);
  readonly options = signal<PickerOption[]>([]);
  readonly emailError = signal<string | null>(null);
  readonly propertyError = signal<string | null>(null);
  readonly pendingInvitationId = signal<string | null>(null);
  readonly topError = signal<string | null>(null);
  readonly refused = signal(false);
  readonly rateLimited = signal(false);
  readonly busy = signal(false);

  readonly noMatch = (q: string) => `No property in ${this.communityName} matches “${q}”.`;

  private readonly address = computed(() => this.fixedProperty?.address
    ?? this.options().find(o => o.id === this.propertyId())?.label ?? null);

  readonly primaryLabel = computed(() => this.onboarding
    ? (this.busy() ? 'Saving…' : 'Save invitation')
    : (this.busy() ? 'Sending…' : 'Send invitation'));

  readonly summary = computed(() => {
    const email = this.email().trim();
    const address = this.address();
    if (!EMAIL.test(email) || !address) return null;
    return this.onboarding
      ? `We'll save an invitation for ${email} to be a resident of ${address}. It's emailed when ${this.communityName} is marked Active.`
      : `We'll email ${email} an invitation to be a resident of ${address}. It's valid for 72 hours. If they already have an account, they sign in to accept. Nothing changes until they accept.`;
  });

  private searchSeq = 0;

  ngOnInit(): void {
    if (this.fixedProperty) this.propertyId.set(this.fixedProperty.propertyId);
    else void this.searchProperties('');
  }

  onEmail(value: string): void {
    this.email.set(value);
    this.emailError.set(null);
    this.pendingInvitationId.set(null);
  }

  chooseProperty(id: string | null): void {
    this.propertyId.set(id);
    this.propertyError.set(null);
  }

  /** The picker searches this community's properties by address (FR-024d). */
  async searchProperties(query: string): Promise<void> {
    const seq = ++this.searchSeq;
    try {
      const page = await this.svc.getResidents(this.communityId, { search: query.trim() || undefined, limit: 25 });
      if (seq !== this.searchSeq) return;
      const chosen = this.options().find(o => o.id === this.propertyId());
      const found = page.items.map(r => ({ id: r.propertyId, label: r.address }));
      this.options.set(chosen && !found.some(o => o.id === chosen.id) ? [chosen, ...found] : found);
    } catch {
      // The picker keeps its last list; the submit still checks the choice.
    }
  }

  cancel(): void {
    if (!this.busy()) this.cancelled.emit();
  }

  async submit(): Promise<void> {
    if (this.busy()) return;
    const email = this.email().trim();
    const propertyId = this.propertyId();
    this.topError.set(null);
    this.rateLimited.set(false);
    this.propertyError.set(propertyId ? null : 'Choose a property.');
    this.emailError.set(EMAIL.test(email) ? null : 'Enter a valid email address.');
    if (!propertyId || !EMAIL.test(email)) return;
    const address = this.address();
    this.busy.set(true);
    try {
      const sent = await this.svc.inviteResident(this.communityId, propertyId, email);
      this.done.emit({
        propertyId,
        message: sent.state === 'Scheduled'
          ? `Invitation saved. It will be sent when ${this.communityName} is marked Active.`
          : `Invitation sent to ${email}.`,
      });
    } catch (err) {
      this.onError(err, email, address);
    } finally {
      this.busy.set(false);
    }
  }

  async resendPending(): Promise<void> {
    const id = this.pendingInvitationId();
    const propertyId = this.propertyId();
    if (!id || !propertyId || this.busy()) return;
    this.busy.set(true);
    try {
      await this.svc.resendInvitation(this.communityId, id);
      this.done.emit({ propertyId, message: `Invitation sent again to ${this.email().trim()}. The earlier link no longer works.` });
    } catch (err) {
      this.onError(err, this.email().trim(), this.address());
    } finally {
      this.busy.set(false);
    }
  }

  private onError(err: unknown, email: string, propertyAddress: string | null): void {
    const code = refusalCodeOf(err);
    const body = (err as { error?: { invitationId?: string; errors?: { field?: string; message?: string }[] } })?.error;
    switch (code) {
      case 'ALREADY_RESIDENT':
      case 'SELF_GRANT_NOT_ALLOWED':
        this.emailError.set(refusalMessage(code, { email, propertyAddress }));
        return;
      case 'INVITATION_PENDING':
        this.emailError.set(refusalMessage(code, {
          email, propertyAddress, communityName: this.communityName, variant: this.onboarding ? 'scheduled' : 'resident',
        }));
        this.pendingInvitationId.set(this.onboarding ? null : body?.invitationId ?? null);
        return;
      case 'VALIDATION_ERROR': {
        const emailProblem = body?.errors?.find(e => (e.field ?? '').toLowerCase() === 'email');
        if (emailProblem?.message) this.emailError.set(emailProblem.message);
        else this.topError.set(body?.errors?.[0]?.message ?? SAVE_FAILED_MESSAGE);
        return;
      }
      case 'RATE_LIMITED':
        this.rateLimited.set(true);
        return;
      case null:
        this.topError.set(SAVE_FAILED_MESSAGE);
        break;
      default:
        this.topError.set(refusalMessage(code, { communityName: this.communityName, audience: this.ctx.audience(), surface: 'action' }));
        this.refused.set(true);
    }
    this.dialog?.focus('.dlg-refusal');
  }
}
