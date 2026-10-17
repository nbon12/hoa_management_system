import { AfterViewInit, Component, ElementRef, EventEmitter, HostListener, Input, Output, ViewChild, inject, signal } from '@angular/core';
import { ResidentArchitecturalService } from '../../../core/services/resident-architectural.service';
import { ResidentArcDetail } from '../../../core/models';
import { errorMessage } from './resident-arc-format';

/**
 * 029 US4: confirm withdrawing an undecided request. A modal dialog that moves focus inside, keeps
 * Tab within it, and closes on Escape (constitution §6: dialogs must be keyboard-accessible).
 */
@Component({
  selector: 'app-arc-withdraw-dialog',
  standalone: true,
  styles: [`
    .backdrop { position: fixed; inset: 0; background: rgba(0,0,0,.35); display: grid; place-items: center; padding: 16px; z-index: 50; }
    .dialog { max-width: 440px; width: 100%; box-sizing: border-box; }
    .actions { display: flex; flex-wrap: wrap; gap: 10px; justify-content: flex-end; margin-top: 14px; }
    @media (max-width: 600px) { .actions .btn { flex: 1 1 100%; } }
  `],
  template: `
    <div class="backdrop">
      <div #dialog class="card dialog" role="dialog" aria-modal="true" aria-labelledby="withdraw-title" aria-describedby="withdraw-body">
        <h2 class="section-title" id="withdraw-title">Withdraw {{ displayId }}?</h2>
        <p id="withdraw-body">The board will stop reviewing this request. You can't undo this — to go ahead later, file a new request.</p>
        @if (error()) { <div class="alert alert--error" role="alert">{{ error() }}</div> }
        <div class="actions">
          <button #cancelButton class="btn btn--ghost" (click)="cancelled.emit()">Keep request</button>
          <button class="btn btn--primary" (click)="confirm()" [disabled]="busy()">
            @if (busy()) { <span class="spinner"></span> } Withdraw request
          </button>
        </div>
      </div>
    </div>
  `,
})
export class WithdrawDialogComponent implements AfterViewInit {
  private api = inject(ResidentArchitecturalService);

  @Input({ required: true }) applicationId!: string;
  @Input({ required: true }) displayId!: string;
  @Output() withdrawn = new EventEmitter<ResidentArcDetail>();
  @Output() cancelled = new EventEmitter<void>();
  @ViewChild('dialog') dialog!: ElementRef<HTMLElement>;
  @ViewChild('cancelButton') cancelButton!: ElementRef<HTMLButtonElement>;

  busy = signal(false);
  error = signal<string | null>(null);

  ngAfterViewInit(): void {
    this.cancelButton.nativeElement.focus();
  }

  @HostListener('document:keydown', ['$event'])
  onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      event.preventDefault();
      this.cancelled.emit();
      return;
    }
    if (event.key !== 'Tab') return;
    const focusable = Array.from(this.dialog.nativeElement.querySelectorAll<HTMLElement>('button:not([disabled])'));
    if (focusable.length === 0) return;
    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  }

  async confirm(): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      this.withdrawn.emit(await this.api.withdraw(this.applicationId));
    } catch (err) {
      this.error.set(errorMessage(err));
    } finally {
      this.busy.set(false);
    }
  }
}
