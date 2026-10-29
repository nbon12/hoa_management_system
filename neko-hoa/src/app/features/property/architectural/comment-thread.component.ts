import { Component, EventEmitter, Input, Output, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ResidentArchitecturalService } from '../../../core/services/resident-architectural.service';
import { ResidentArcAttachment, ResidentArcDetail } from '../../../core/models';
import { ACCEPTED_FILE_TYPES, errorMessage, fileSize } from './resident-arc-format';

export const MAX_COMMENT_FILES = 5;

/**
 * 031 US7: the homeowner's comment section on a request — e.g. to answer "revisions requested" — with text
 * and/or up to five files. The board sees these comments under the application's details.
 */
@Component({
  selector: 'app-arc-comment-thread',
  standalone: true,
  imports: [FormsModule, DatePipe],
  styles: [`
    :host { display: block; }
    .thread { list-style: none; padding: 0; margin: 0 0 12px; display: grid; gap: 10px; }
    .entry { border-left: 3px solid var(--lav-2); padding: 4px 0 4px 10px; display: grid; gap: 4px; }
    .body { white-space: pre-wrap; overflow-wrap: anywhere; margin: 0; }
    .files { list-style: none; padding: 0; margin: 0; display: grid; gap: 4px; }
    .files li { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; }
    textarea.field { width: 100%; box-sizing: border-box; min-height: 80px; resize: vertical; }
    .actions { display: flex; flex-wrap: wrap; gap: 10px; align-items: center; margin-top: 10px; }
    .error-text { color: var(--rose, #b42318); font-size: 13px; }
    @media (max-width: 600px) { .actions .btn { flex: 1 1 100%; } }
  `],
  template: `
    <section class="card" aria-labelledby="comments-title">
      <h2 class="section-title" id="comments-title">Comments to the board</h2>
      @if (detail.comments?.length) {
        <ul class="thread">
          @for (c of detail.comments; track c.id) {
            <li class="entry">
              <div class="field-label">You · {{ c.createdAt | date:'MM/dd/yy h:mm a' }}</div>
              @if (c.body) { <p class="body">{{ c.body }}</p> }
              @if (c.attachments.length) {
                <ul class="files">
                  @for (f of c.attachments; track f.id) {
                    <li>
                      <span>📎 {{ f.fileName }} <span class="muted">· {{ size(f.sizeBytes) }}</span></span>
                      <button class="btn btn--ghost" (click)="openFile.emit(f)" [attr.aria-label]="'Open ' + f.fileName">Open</button>
                    </li>
                  }
                </ul>
              }
            </li>
          }
        </ul>
      } @else {
        <p class="muted">No comments yet.</p>
      }

      @if (detail.canComment) {
        <label class="field-label" for="comment-body">Add a comment or revised plans</label>
        <textarea class="field" id="comment-body" maxlength="2000" [(ngModel)]="message" [disabled]="busy()"
                  placeholder="e.g. I lowered the fence to 5ft — revised plan attached."></textarea>
        <div class="muted">{{ message.length }} / 2000</div>

        <div style="margin-top:8px;">
          <label class="field-label" for="comment-files">Attach files (optional, up to {{ maxFiles }})</label>
          <input #picker type="file" multiple id="comment-files" [accept]="accept" [disabled]="busy()"
                 (change)="onFiles(picker.files); picker.value = ''">
          @if (files().length) {
            <ul class="files">
              @for (f of files(); track $index) {
                <li>📎 {{ f.name }} <span class="muted">· {{ size(f.size) }}</span>
                  <button class="btn btn--ghost" type="button" (click)="remove($index)" [attr.aria-label]="'Remove ' + f.name">Remove</button>
                </li>
              }
            </ul>
          }
        </div>

        @if (error()) { <div class="error-text" role="alert">{{ error() }}</div> }
        <div class="actions">
          <button class="btn btn--primary" (click)="post()" [disabled]="busy() || (!message.trim() && !files().length)">
            @if (busy()) { <span class="spinner"></span> } Post comment
          </button>
        </div>
      }
    </section>
  `,
})
export class CommentThreadComponent {
  private api = inject(ResidentArchitecturalService);

  @Input({ required: true }) detail!: ResidentArcDetail;
  @Output() posted = new EventEmitter<ResidentArcDetail>();
  @Output() openFile = new EventEmitter<ResidentArcAttachment>();

  readonly accept = ACCEPTED_FILE_TYPES;
  readonly size = fileSize;
  readonly maxFiles = MAX_COMMENT_FILES;
  message = '';
  files = signal<File[]>([]);
  busy = signal(false);
  error = signal<string | null>(null);

  onFiles(list: FileList | null): void {
    // Copy now: the template clears the input right after this call and a FileList is live.
    const picked = Array.from(list ?? []);
    const next = [...this.files(), ...picked];
    if (next.length > MAX_COMMENT_FILES) {
      this.error.set(`Attach at most ${MAX_COMMENT_FILES} files to one comment.`);
      return;
    }
    this.error.set(null);
    this.files.set(next);
  }

  remove(index: number): void {
    this.files.update(list => list.filter((_, i) => i !== index));
  }

  async post(): Promise<void> {
    const message = this.message.trim();
    if (!message && !this.files().length) return;
    this.busy.set(true);
    this.error.set(null);
    try {
      const detail = await this.api.comment(this.detail.id, message, this.files());
      this.message = '';
      this.files.set([]);
      this.posted.emit(detail);
    } catch (err) {
      this.error.set(errorMessage(err));
    } finally {
      this.busy.set(false);
    }
  }
}
