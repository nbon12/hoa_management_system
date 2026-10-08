import { Component, EventEmitter, Input, Output, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ResidentArchitecturalService } from '../../../core/services/resident-architectural.service';
import { ResidentArcAttachment, ResidentArcDetail, ResidentArcInfoRequest } from '../../../core/models';
import { ACCEPTED_FILE_TYPES, errorMessage, fileSize } from './resident-arc-format';

/** 029 US3: answer one of the board's unanswered questions, optionally with files. */
@Component({
  selector: 'app-arc-info-reply',
  standalone: true,
  imports: [FormsModule, DatePipe],
  styles: [`
    :host { display: block; }
    textarea.field { width: 100%; box-sizing: border-box; min-height: 90px; resize: vertical; }
    .actions { display: flex; flex-wrap: wrap; gap: 10px; align-items: center; margin-top: 10px; }
    .files { list-style: none; padding: 0; margin: 6px 0 0; }
    .error-text { color: var(--rose, #b42318); font-size: 13px; }
    @media (max-width: 600px) { .actions .btn { flex: 1 1 100%; } }
  `],
  template: `
    <div class="card card--rose">
      <div class="section-title" style="margin:0;">The board asked for more information</div>
      <p class="muted" style="margin:2px 0 6px;">Asked {{ request.requestedAt | date:'MM/dd/yy' }}</p>
      <blockquote style="margin:0 0 10px;">{{ request.message }}</blockquote>

      <label class="field-label" [for]="'reply-' + request.id">Your reply</label>
      <textarea class="field" [id]="'reply-' + request.id" maxlength="2000" [(ngModel)]="message"
                [attr.aria-describedby]="'reply-count-' + request.id + (error() ? ' reply-error-' + request.id : '')"></textarea>
      <div class="muted" [id]="'reply-count-' + request.id">{{ message.length }} / 2000</div>

      <div style="margin-top:8px;">
        <label class="field-label" [for]="'reply-files-' + request.id">Add files (optional)</label>
        <input #files type="file" multiple [id]="'reply-files-' + request.id" [accept]="accept"
               (change)="onFiles(files.files); files.value = ''" [disabled]="busy()">
        <ul class="files">
          @for (f of uploaded(); track f.id) { <li>📎 {{ f.fileName }} <span class="muted">· {{ size(f.sizeBytes) }}</span></li> }
        </ul>
      </div>

      @if (error()) {
        <div class="error-text" role="alert" [id]="'reply-error-' + request.id">{{ error() }}</div>
      }
      <div class="actions">
        <button class="btn btn--primary" (click)="send()" [disabled]="busy() || !message.trim()">
          @if (busy()) { <span class="spinner"></span> } Send reply
        </button>
      </div>
    </div>
  `,
})
export class InfoReplyComponent {
  private api = inject(ResidentArchitecturalService);

  @Input({ required: true }) applicationId!: string;
  @Input({ required: true }) request!: ResidentArcInfoRequest;
  @Output() replied = new EventEmitter<ResidentArcDetail>();

  readonly accept = ACCEPTED_FILE_TYPES;
  readonly size = fileSize;
  message = '';
  uploaded = signal<ResidentArcAttachment[]>([]);
  busy = signal(false);
  error = signal<string | null>(null);

  async onFiles(files: FileList | null): Promise<void> {
    // Copy now: the template clears the input right after this call and a FileList is live.
    const picked = Array.from(files ?? []);
    if (!picked.length) return;
    this.busy.set(true);
    this.error.set(null);
    for (const file of picked) {
      try {
        const added = await this.api.uploadReplyAttachment(this.applicationId, this.request.id, file);
        this.uploaded.update(list => [...list, added]);
      } catch (err) {
        this.error.set(`${file.name}: ${errorMessage(err)}`);
      }
    }
    this.busy.set(false);
  }

  async send(): Promise<void> {
    if (!this.message.trim()) return;
    this.busy.set(true);
    this.error.set(null);
    try {
      this.replied.emit(await this.api.reply(this.applicationId, this.request.id, this.message.trim()));
    } catch (err) {
      this.error.set(errorMessage(err));
    } finally {
      this.busy.set(false);
    }
  }
}
