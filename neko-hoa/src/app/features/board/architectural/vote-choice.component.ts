import { Component, input, output, signal } from '@angular/core';
import { ArcVoteChoice } from '../../../core/services/architectural.service';
import { CHOICE_LABELS } from './arc-format';

/**
 * 031 (items 3 and 5): voting is a two-step action. Picking Approve / Request revisions / Deny only selects it
 * (nothing is pre-selected); a Submit (or Save) button then appears and casts the vote.
 */
@Component({
  selector: 'app-arc-vote-choice',
  standalone: true,
  template: `
    <div class="vch" role="group" [attr.aria-label]="groupLabel()">
      @for (c of choices; track c) {
        <button type="button" class="vote-choice" [attr.aria-pressed]="selected() === c" [disabled]="busy()"
                (click)="pick(c)">{{ label(c) }}</button>
      }
      @if (selected()) {
        <button type="button" class="btn btn--primary vch__submit" [disabled]="busy()" (click)="submit()">{{ submitLabel() }}</button>
      }
    </div>
  `,
  styles: [`
    .vch { display: flex; gap: 5px; flex-wrap: wrap; align-items: center; }
    .vch__submit { padding: 5px 13px; font-size: 11.5px; }
  `]
})
export class VoteChoiceComponent {
  readonly busy = input<boolean>(false);
  readonly submitLabel = input<string>('Submit');
  readonly groupLabel = input<string>('Your vote');
  readonly submitted = output<ArcVoteChoice>();

  readonly choices: ArcVoteChoice[] = ['Approve', 'RevisionsNeeded', 'Deny'];
  readonly selected = signal<ArcVoteChoice | null>(null);
  readonly label = (c: ArcVoteChoice) => CHOICE_LABELS[c];

  pick(choice: ArcVoteChoice): void {
    this.selected.set(this.selected() === choice ? null : choice);
  }

  submit(): void {
    const choice = this.selected();
    if (choice) this.submitted.emit(choice);
  }

  reset(): void {
    this.selected.set(null);
  }
}
