import { Component, EventEmitter, Input, OnInit, Output, ViewChild, computed, inject, signal } from '@angular/core';
import { CompanyService, CommunityRef } from '../../core/services/company.service';
import { BoardService } from '../../core/services/board.service';
import { DialogComponent } from '../../shared/dialog.component';
import { ComboPickerComponent, PickerOption } from '../../shared/combo-picker.component';
import { RATE_LIMITED_MESSAGE, refusalCodeOf, refusalMessage } from '../../shared/refusal-messages';
import { loadParentOptions, toPickerOptions } from '../company/community-options';

/**
 * 030 FR-016 / brief 04 8.04.w–x, §5.9: "Change the parent of <community>?" for a Company
 * Administrator. "No parent" is the first option (D7), then every non-archived community but this
 * one, A–Z. A loop (`PARENT_CYCLE`) or an archived parent from an out-of-date list
 * (`PARENT_ARCHIVED`) is refused under the field; the person can choose again.
 */
@Component({
  selector: 'app-change-parent-dialog',
  standalone: true,
  imports: [DialogComponent, ComboPickerComponent],
  template: `
    <app-dialog [heading]="'Change the parent of ' + communityName + '?'" [fallbackFocus]="fallbackFocus" (dismiss)="cancel()">
      @if (topError()) { <p class="dlg-refusal field-error" role="alert" tabindex="-1">{{ topError() }}</p> }
      <p>Current parent association: {{ currentParent?.communityName ?? 'None' }}.</p>
      <div class="dlg-form">
        <app-combo-picker label="New parent association" placeholder="Search communities"
                          helper="Archived communities can't be a parent, so they aren't listed."
                          noneLabel="No parent" [noun]="['community', 'communities']"
                          [options]="options()" [value]="value()" [error]="fieldError()" [disabled]="busy()"
                          (valueChange)="choose($event)" />
      </div>
      @if (loadError()) { <p class="field-error" role="alert">Couldn't load the communities. Close this and try again.</p> }
      <p class="dlg-section">{{ communityName }} becomes a sub-association of the community you choose.</p>
      <p class="dlg-section">No one gains or loses access. Access is set for each community on its own, never through the parent.</p>
      <div class="dlg-actions">
        <button type="button" class="btn btn--primary" [disabled]="busy()" (click)="submit()">{{ busy() ? 'Changing…' : 'Change parent' }}</button>
        <button type="button" class="btn" [disabled]="busy()" (click)="cancel()">Cancel</button>
        @if (rateLimited()) { <span class="field-error" role="alert">{{ rateLimitedMessage }}</span> }
      </div>
    </app-dialog>
  `,
})
export class ChangeParentDialogComponent implements OnInit {
  @Input({ required: true }) communityId = '';
  @Input({ required: true }) communityName = '';
  @Input() currentParent: CommunityRef | null = null;
  @Input() fallbackFocus: HTMLElement | null = null;
  /** The success message for the toast. */
  @Output() readonly done = new EventEmitter<string>();
  @Output() readonly cancelled = new EventEmitter<void>();

  @ViewChild(DialogComponent) private dialog?: DialogComponent;

  private company = inject(CompanyService);
  private board = inject(BoardService);

  readonly rateLimitedMessage = RATE_LIMITED_MESSAGE;
  readonly options = signal<PickerOption[]>([]);
  readonly value = signal<string | null>(null);
  readonly fieldError = signal<string | null>(null);
  readonly topError = signal<string | null>(null);
  readonly loadError = signal(false);
  readonly rateLimited = signal(false);
  readonly busy = signal(false);

  private readonly chosenLabel = computed(() => this.options().find(o => o.id === this.value())?.label ?? null);

  ngOnInit(): void {
    this.value.set(this.currentParent?.id ?? null);
    void this.loadOptions();
  }

  choose(id: string | null): void {
    this.value.set(id);
    this.fieldError.set(null);
  }

  cancel(): void {
    if (!this.busy()) this.cancelled.emit();
  }

  async submit(): Promise<void> {
    if (this.busy()) return;
    const parentId = this.value();
    if (parentId === (this.currentParent?.id ?? null)) {
      this.fieldError.set('Choose a new parent association.');
      return;
    }
    const parentName = this.chosenLabel();
    this.busy.set(true);
    this.topError.set(null);
    this.rateLimited.set(false);
    try {
      await this.company.changeParent(this.communityId, parentId);
      this.done.emit(parentId
        ? `${this.communityName} is now a sub-association of ${parentName}.`
        : `${this.communityName} no longer has a parent association.`);
    } catch (err) {
      this.onError(err, parentName);
    } finally {
      this.busy.set(false);
    }
  }

  private onError(err: unknown, parentName: string | null): void {
    const code = refusalCodeOf(err);
    switch (code) {
      case 'PARENT_CYCLE':
        this.fieldError.set(refusalMessage(code, { communityName: this.communityName, parentName }));
        return;
      case 'PARENT_ARCHIVED':
        // 8.04.x: reached only with an out-of-date list. The list reloads, and the field empties.
        this.fieldError.set(refusalMessage(code, { parentName }));
        this.value.set(this.currentParent?.id ?? null);
        void this.loadOptions();
        return;
      case 'RATE_LIMITED':
        this.rateLimited.set(true);
        return;
      case null:
        this.topError.set("Couldn't change the parent. Check your connection and try again.");
        break;
      default:
        this.topError.set(refusalMessage(code, { communityName: this.communityName, audience: 'companyAdmin', surface: 'action' }));
    }
    this.dialog?.focus('.dlg-refusal');
  }

  private async loadOptions(): Promise<void> {
    try {
      const all = await loadParentOptions(true, this.company, this.board);
      this.options.set(toPickerOptions(all.filter(c => c.id !== this.communityId)));
      this.loadError.set(false);
    } catch {
      this.loadError.set(true);
    }
  }
}
