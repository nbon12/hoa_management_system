import { Component, EventEmitter, Input, OnInit, Output, ViewChild, computed, inject, signal } from '@angular/core';
import {
  CommunitySettingsService, PropertyInvitation, PropertyResident, RemovalPreview, ResidentProperty,
} from '../../../core/services/community-settings.service';
import { DialogComponent } from '../../../shared/dialog.component';
import { RATE_LIMITED_MESSAGE, refusalCodeOf, refusalMessage } from '../../../shared/refusal-messages';
import { CommunitySettingsContext } from '../community-settings-context';

export type ResidentChange =
  | { kind: 'remove'; resident: PropertyResident; property: ResidentProperty }
  | { kind: 'revoke'; invitation: PropertyInvitation; property: ResidentProperty };

export interface ResidentChangeDone {
  propertyId: string;
  message: string;
}

/**
 * 030 US8 / FR-024c / brief 06 §5: the Remove resident and Revoke invitation dialogs.
 *
 * Remove asks the server what will change when it opens (`removal-preview`): whether the
 * property's autopay is cancelled and who is emailed. It never says whether the resident has
 * other homes (§4.5 item 8). A refusal shows at the top, and the primary becomes "Close".
 */
@Component({
  selector: 'app-resident-change-dialog',
  standalone: true,
  imports: [DialogComponent],
  template: `
    <app-dialog [heading]="heading()" (dismiss)="cancel()">
      @if (refusal()) {
        <p class="dlg-refusal field-error" role="alert" tabindex="-1">{{ refusal() }}</p>
      } @else {
        @if (actionError()) { <p class="dlg-refusal field-error" role="alert" tabindex="-1">{{ actionError() }}</p> }
        @switch (change.kind) {
          @case ('remove') {
            @switch (previewState()) {
              @case ('loading') { <p class="sr-only" role="status">Loading what this will change…</p><div class="ph rc__ph" aria-hidden="true"></div> }
              @case ('error') {
                <div class="dlg-refusal" role="alert">
                  <p class="field-error">Couldn't load what this will change.</p>
                  <button type="button" class="btn" (click)="loadPreview()">Try again</button>
                </div>
              }
              @default {
                <div class="dlg-section">
                  <h3>What happens</h3>
                  <ul>@for (line of removeHappens(); track $index) { <li>{{ line }}</li> }</ul>
                </div>
                <div class="dlg-section">
                  <h3>What doesn't change</h3>
                  <ul>@for (line of removeUnchanged(); track $index) { <li>{{ line }}</li> }</ul>
                </div>
              }
            }
          }
          @case ('revoke') {
            <div class="dlg-section">
              <h3>What happens</h3>
              <ul>@for (line of revokeHappens(); track $index) { <li>{{ line }}</li> }</ul>
            </div>
            <div class="dlg-section">
              <h3>What doesn't change</h3>
              <ul><li>You can send {{ email() }} a new invitation later.</li></ul>
            </div>
          }
        }
      }
      <div class="dlg-actions">
        @if (refusal()) {
          <button type="button" class="btn btn--primary" (click)="closeAfterRefusal.emit()">Close</button>
        } @else {
          <button type="button" class="btn btn--primary" [disabled]="busy() || (change.kind === 'remove' && previewState() !== 'ready')"
                  (click)="confirm()">{{ primaryLabel() }}</button>
          <button type="button" class="btn" [disabled]="busy()" (click)="cancel()">Cancel</button>
          @if (rateLimited()) { <span class="field-error" role="alert">{{ rateLimitedMessage }}</span> }
        }
      </div>
    </app-dialog>
  `,
  styles: [`.rc__ph { height: 80px; margin-top: 10px; }`],
})
export class ResidentChangeDialogComponent implements OnInit {
  @Input({ required: true }) communityId = '';
  @Input({ required: true }) communityName = '';
  @Input() onboarding = false;
  @Input({ required: true }) change!: ResidentChange;
  @Output() readonly done = new EventEmitter<ResidentChangeDone>();
  @Output() readonly cancelled = new EventEmitter<void>();
  @Output() readonly closeAfterRefusal = new EventEmitter<void>();

  @ViewChild(DialogComponent) private dialog?: DialogComponent;

  private svc = inject(CommunitySettingsService);
  private ctx = inject(CommunitySettingsContext);

  readonly rateLimitedMessage = RATE_LIMITED_MESSAGE;
  readonly preview = signal<RemovalPreview | null>(null);
  readonly previewState = signal<'loading' | 'ready' | 'error'>('loading');
  readonly refusal = signal<string | null>(null);
  readonly actionError = signal<string | null>(null);
  readonly rateLimited = signal(false);
  readonly busy = signal(false);

  readonly email = computed(() => (this.change.kind === 'revoke' ? this.change.invitation.email : ''));

  readonly heading = computed(() => this.change.kind === 'remove'
    ? `Remove ${this.change.resident.name} from ${this.change.property.address}?`
    : `Revoke the invitation to ${this.change.invitation.email}?`);

  readonly primaryLabel = computed(() => this.change.kind === 'remove'
    ? (this.busy() ? 'Removing…' : 'Remove resident')
    : (this.busy() ? 'Revoking…' : 'Revoke invitation'));

  readonly removeHappens = computed(() => {
    const p = this.preview();
    const c = this.change;
    if (c.kind !== 'remove' || !p) return [];
    const name = c.resident.name;
    const address = c.property.address;
    const cancels = p.autopay === 'WillCancel';
    const lines = [`${name} loses access to ${address} at once, even if they're signed in right now.`];
    if (cancels) {
      lines.push(p.autopayReason === 'NoEnrollerRecorded'
        ? "This property's autopay is cancelled. It was set up before the app recorded who enrolled it, so removing any resident cancels it."
        : `This property's autopay is cancelled, because ${name} enrolled it. No new automatic payment starts. A payment already in progress finishes normally.`);
    }
    lines.push(cancels ? `${name} is emailed that they were removed and that autopay ended.` : `${name} is emailed that they were removed.`);
    if (cancels && p.otherResidentsEmailed > 0) {
      const others = c.property.residents.filter(r => r.userId !== c.resident.userId).map(r => r.name);
      lines.push(p.otherResidentsEmailed === 1 && others.length === 1
        ? `The other resident, ${others[0]}, is emailed that autopay ended.`
        : `The ${p.otherResidentsEmailed} other residents are emailed that autopay ended.`);
    }
    if (c.property.residents.length === 1) lines.push(`${address} will have no resident and shows “No resident”.`);
    return lines;
  });

  readonly removeUnchanged = computed(() => {
    const p = this.preview();
    if (this.change.kind !== 'remove' || !p) return [];
    const name = this.change.resident.name;
    const address = this.change.property.address;
    const lines: string[] = [];
    if (p.autopay === 'Unaffected') lines.push(`This property's autopay stays on. ${name} didn't enroll it.`);
    if (p.autopay === 'None') lines.push('This property has no active autopay.');
    lines.push(`The owner of record and the balance of ${address} don't change.`);
    lines.push(`${name}'s account, any other homes and roles don't change. They can still sign in.`);
    return lines;
  });

  readonly revokeHappens = computed(() => {
    if (this.change.kind !== 'revoke') return [];
    const inv = this.change.invitation;
    const lines = [
      'The link in the invitation email stops working at once.',
      `${inv.email} can't use it to become a resident of ${this.change.property.address}.`,
    ];
    if ((inv.effectiveState || inv.state) === 'Scheduled') lines.push(`It won't be emailed when ${this.communityName} is marked Active.`);
    return lines;
  });

  ngOnInit(): void {
    if (this.change.kind === 'remove') void this.loadPreview();
  }

  async loadPreview(): Promise<void> {
    if (this.change.kind !== 'remove') return;
    this.previewState.set('loading');
    try {
      this.preview.set(await this.svc.getRemovalPreview(this.communityId, this.change.property.propertyId, this.change.resident.userId));
      this.previewState.set('ready');
    } catch (err) {
      const code = refusalCodeOf(err);
      if (code && code !== 'RATE_LIMITED') this.showRefusal(code);
      else this.previewState.set('error');
    }
  }

  cancel(): void {
    if (!this.busy()) this.cancelled.emit();
  }

  async confirm(): Promise<void> {
    if (this.busy()) return;
    const c = this.change;
    this.busy.set(true);
    this.actionError.set(null);
    this.rateLimited.set(false);
    try {
      if (c.kind === 'remove') {
        const result = await this.svc.removeResident(this.communityId, c.property.propertyId, c.resident.userId);
        this.done.emit({
          propertyId: c.property.propertyId,
          message: result.autopayCancelled
            ? `${c.resident.name} was removed from ${c.property.address}. Its autopay was cancelled.`
            : `${c.resident.name} was removed from ${c.property.address}.`,
        });
      } else {
        await this.svc.revokeInvitation(this.communityId, c.invitation.id);
        this.done.emit({ propertyId: c.property.propertyId, message: `Invitation to ${c.invitation.email} revoked.` });
      }
    } catch (err) {
      this.onError(err);
    } finally {
      this.busy.set(false);
    }
  }

  private onError(err: unknown): void {
    const c = this.change;
    const code = refusalCodeOf(err);
    if (code === 'RATE_LIMITED') {
      this.rateLimited.set(true);
      return;
    }
    if (code === null) {
      this.actionError.set(c.kind === 'remove'
        ? "Couldn't remove the resident. Check your connection and try again."
        : "Couldn't revoke the invitation. Check your connection and try again.");
      this.dialog?.focus('.dlg-refusal');
      return;
    }
    // §4.5 item 12: someone else removed them first.
    if (code === 'NOT_FOUND' && c.kind === 'remove') {
      this.done.emit({ propertyId: c.property.propertyId, message: `${c.resident.name} is no longer a resident of ${c.property.address}. The list is now up to date.` });
      return;
    }
    if (code === 'INVITATION_INVALID') {
      this.done.emit({ propertyId: c.property.propertyId, message: refusalMessage(code, { audience: this.ctx.audience() }) });
      return;
    }
    this.showRefusal(code);
  }

  private showRefusal(code: string): void {
    this.refusal.set(refusalMessage(code, { communityName: this.communityName, audience: this.ctx.audience(), surface: 'action' }));
    this.dialog?.focus('.dlg-refusal');
  }
}
