import { Component, ElementRef, input, output, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ArcVoteChoice } from '../../../core/services/architectural.service';
import { infoNotice } from './arc-format';
import { VoteChoiceComponent } from './vote-choice.component';

export interface ArcVoteEvent { choice: ArcVoteChoice; comment: string | null; }

// 027 US2/US4 — wireframe "Cast your vote" card: an optional comment to the board, then
// ✓ Approve / ↻ Revisions needed / ✕ Deny, or Request info (message required, never pauses the clock).
// 031: the choice is picked first and cast with "Submit vote" (items 5, 14); "Request info" is "Ask question"
// (item 11); the comment box shows a hover tint so it reads as typeable (item 6).
@Component({
  selector: 'app-arc-cast-vote-card',
  standalone: true,
  imports: [FormsModule, VoteChoiceComponent],
  template: `
    <div class="card cvc">
      <div class="section-title" style="margin:0 0 8px;">Cast your vote</div>
      <label class="field-label" for="arc-comment">Comment to the board</label>
      <textarea #commentBox id="arc-comment" class="field cvc__comment" rows="3" maxlength="2000"
                [(ngModel)]="comment" name="comment"
                placeholder="Optional — visible to the board and the manager"></textarea>
      <div class="cvc__count muted">{{ comment.length }}/2000</div>

      @if (infoMode()) {
        <p class="cvc__notice" role="note">{{ notice() }}</p>
      }
      @if (error()) { <p class="cvc__error" role="alert">{{ error() }}</p> }

      <div class="cvc__actions">
        <app-arc-vote-choice #choice [busy]="busy()" submitLabel="Submit vote" (submitted)="cast($event)" />
        <button type="button" class="btn btn--ghost cvc__ask" [disabled]="busy()" (click)="info()">Ask question</button>
      </div>
      <p class="muted cvc__rule">{{ ruleText() }}</p>
    </div>
  `,
  styles: [`
    .cvc { background: var(--lav); }
    .cvc__comment { width: 100%; min-height: 64px; resize: vertical; }
    .cvc__count { font-size: 11px; text-align: right; }
    .cvc__actions { display: flex; gap: 8px; margin-top: 12px; flex-wrap: wrap; align-items: center; justify-content: space-between; }
    .cvc__notice { font-size: 12px; background: var(--paper); border: 1.5px dashed var(--line); border-radius: 10px; padding: 8px 10px; }
    .cvc__error { color: var(--warn); font-size: 12px; }
    .cvc__rule { font-size: 11px; margin: 10px 0 0; line-height: 1.55; }
  `]
})
export class CastVoteCardComponent {
  readonly dueDate = input.required<string>();
  readonly ruleText = input<string>('');
  readonly busy = input<boolean>(false);
  readonly voted = output<ArcVoteEvent>();
  readonly infoRequested = output<string>();

  readonly infoMode = signal(false);
  readonly error = signal<string | null>(null);
  private readonly commentBox = viewChild<ElementRef<HTMLTextAreaElement>>('commentBox');
  private readonly choice = viewChild<VoteChoiceComponent>('choice');
  comment = '';

  notice(): string { return infoNotice(this.dueDate()); }

  /** US4-S1: "Ask question" on a row opens the panel with the comment box focused. */
  focusForInfo(): void {
    this.infoMode.set(true);
    this.commentBox()?.nativeElement.focus();
  }

  cast(choice: ArcVoteChoice): void {
    this.error.set(null);
    this.voted.emit({ choice, comment: this.comment.trim() || null });
  }

  info(): void {
    this.infoMode.set(true);
    const message = this.comment.trim();
    if (!message) {
      this.error.set('Type your question for the owner in the box above.');
      this.commentBox()?.nativeElement.focus();
      return;
    }
    this.error.set(null);
    this.infoRequested.emit(message);
  }

  reset(): void {
    this.comment = '';
    this.error.set(null);
    this.choice()?.reset();
  }
}
