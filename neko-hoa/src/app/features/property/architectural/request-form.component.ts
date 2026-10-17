import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { Location } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ResidentArchitecturalService } from '../../../core/services/resident-architectural.service';
import { ArcProjectType, ResidentArcAttachment, ResidentArcDraft, ResidentArcDraftBody } from '../../../core/models';
import { ACCEPTED_FILE_TYPES, PROJECT_TYPES, errorMessage, fileSize } from './resident-arc-format';

interface FormModel {
  projectType: ArcProjectType;
  projectTitle: string;
  description: string;
  plannedStartDate: string;
  plannedCompletionDate: string;
  contractorName: string;
  contractorContact: string;
  acknowledged: boolean;
}

/**
 * 029 US1/US5/US6: create or edit a draft architectural request, attach files, and submit it.
 * Routes: property/architectural/new, …/drafts/:draftId, and …/:id/revise (which starts a
 * revise-and-resubmit draft and continues here). Client checks are UX only; the server decides.
 */
@Component({
  selector: 'app-arc-request-form',
  standalone: true,
  imports: [FormsModule, RouterLink],
  styles: [`
    :host { display: block; max-width: 760px; }
    .form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 14px 18px; }
    .form-grid .full { grid-column: 1 / -1; }
    .actions { display: flex; flex-wrap: wrap; gap: 10px; margin-top: 18px; }
    .files { list-style: none; padding: 0; margin: 8px 0 0; display: grid; gap: 6px; }
    .files li { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
    .files .name { overflow-wrap: anywhere; flex: 1 1 160px; }
    .error-text { color: var(--rose, #b42318); font-size: 13px; margin-top: 4px; }
    textarea.field { min-height: 110px; resize: vertical; }
    .ack { display: flex; gap: 8px; align-items: flex-start; }
    input.field, select.field, textarea.field { width: 100%; box-sizing: border-box; }
    @media (max-width: 600px) {
      .form-grid { grid-template-columns: 1fr; }
      .actions .btn { flex: 1 1 100%; }
    }
  `],
  template: `
    <div class="page-header">
      <h1 class="page-title">{{ heading() }}</h1>
      <div class="page-header__actions">
        <a class="btn btn--ghost" routerLink="/app/property/architectural">Back to my requests</a>
      </div>
    </div>

    @if (loading()) {
      <p class="muted"><span class="spinner"></span> Loading…</p>
    } @else if (loadError()) {
      <div class="alert alert--error" role="alert"><span>⚠</span> {{ loadError() }}</div>
    } @else {
      @if (draft()?.previousDisplayId) {
        <div class="alert" role="note">This is a revision of {{ draft()!.previousDisplayId }}. Change anything you need, then submit it for a new review.</div>
      }
      <form class="card" (ngSubmit)="submit()" novalidate>
        <div class="form-grid">
          <div>
            <label class="field-label" for="arc-type">Project type</label>
            <select id="arc-type" class="field" name="projectType" [(ngModel)]="model.projectType">
              @for (t of projectTypes; track t.value) { <option [value]="t.value">{{ t.label }}</option> }
            </select>
          </div>
          <div>
            <label class="field-label" for="arc-title">Project title</label>
            <input id="arc-title" class="field" name="projectTitle" maxlength="200" [(ngModel)]="model.projectTitle"
                   [attr.aria-invalid]="showErrors() && !model.projectTitle.trim()"
                   aria-describedby="arc-title-error">
            @if (showErrors() && !model.projectTitle.trim()) {
              <div class="error-text" id="arc-title-error">Enter a project title.</div>
            }
          </div>
          <div class="full">
            <label class="field-label" for="arc-description">Description of the work</label>
            <textarea id="arc-description" class="field" name="description" maxlength="4000" [(ngModel)]="model.description"
                      [attr.aria-invalid]="showErrors() && !model.description.trim()"
                      aria-describedby="arc-description-error"></textarea>
            @if (showErrors() && !model.description.trim()) {
              <div class="error-text" id="arc-description-error">Describe the work you plan to do.</div>
            }
          </div>
          <div>
            <label class="field-label" for="arc-start">Planned start date</label>
            <input id="arc-start" class="field" type="date" name="plannedStartDate" [(ngModel)]="model.plannedStartDate"
                   [attr.aria-invalid]="showErrors() && !model.plannedStartDate" aria-describedby="arc-start-error">
            @if (showErrors() && !model.plannedStartDate) {
              <div class="error-text" id="arc-start-error">Choose a planned start date.</div>
            }
          </div>
          <div>
            <label class="field-label" for="arc-finish">Planned completion date</label>
            <input id="arc-finish" class="field" type="date" name="plannedCompletionDate" [(ngModel)]="model.plannedCompletionDate"
                   [attr.aria-invalid]="datesOutOfOrder() || (showErrors() && !model.plannedCompletionDate)"
                   aria-describedby="arc-finish-error">
            @if (datesOutOfOrder()) {
              <div class="error-text" id="arc-finish-error">The completion date can't be before the start date.</div>
            } @else if (showErrors() && !model.plannedCompletionDate) {
              <div class="error-text" id="arc-finish-error">Choose a planned completion date.</div>
            }
          </div>
          <div>
            <label class="field-label" for="arc-contractor">Contractor (optional)</label>
            <input id="arc-contractor" class="field" name="contractorName" maxlength="200" [(ngModel)]="model.contractorName">
          </div>
          <div>
            <label class="field-label" for="arc-contractor-contact">Contractor phone or email (optional)</label>
            <input id="arc-contractor-contact" class="field" name="contractorContact" maxlength="200" [(ngModel)]="model.contractorContact">
          </div>

          <div class="full">
            <div class="field-label" id="arc-files-label">Plans, photos and surveys</div>
            <p class="muted" style="margin:2px 0 8px;">PDF, JPG, PNG or HEIC. Files are private and open through a short-lived link.</p>
            <input #fileInput type="file" multiple [accept]="accept" aria-labelledby="arc-files-label"
                   (change)="onFiles(fileInput.files); fileInput.value = ''" [disabled]="uploading()">
            @if (uploading()) { <span class="muted"><span class="spinner"></span> Uploading…</span> }
            @if (uploadErrors().length) {
              <ul class="files" role="alert">
                @for (e of uploadErrors(); track e) { <li class="error-text">{{ e }}</li> }
              </ul>
            }
            <ul class="files" aria-label="Attached files">
              @for (f of draft()?.carriedAttachments ?? []; track f.id) {
                <li>
                  <span class="name">📎 {{ f.fileName }} <span class="muted">· {{ size(f.sizeBytes) }} · from the earlier revision</span></span>
                  <button type="button" class="btn btn--ghost" (click)="open(f)">Open</button>
                  <button type="button" class="btn btn--ghost" (click)="removeCarried(f)" [attr.aria-label]="'Remove ' + f.fileName">Remove</button>
                </li>
              }
              @for (f of draft()?.attachments ?? []; track f.id) {
                <li>
                  <span class="name">📎 {{ f.fileName }} <span class="muted">· {{ size(f.sizeBytes) }}</span></span>
                  <button type="button" class="btn btn--ghost" (click)="open(f)">Open</button>
                  <button type="button" class="btn btn--ghost" (click)="removeOwn(f)" [attr.aria-label]="'Remove ' + f.fileName">Remove</button>
                </li>
              }
            </ul>
          </div>

          <div class="full ack">
            <input id="arc-ack" type="checkbox" name="acknowledged" [(ngModel)]="model.acknowledged"
                   [attr.aria-invalid]="showErrors() && !model.acknowledged" aria-describedby="arc-ack-error">
            <label for="arc-ack">I understand work may not begin until this request is approved.</label>
          </div>
          @if (showErrors() && !model.acknowledged) {
            <div class="full error-text" id="arc-ack-error">Please confirm before submitting.</div>
          }
        </div>

        <!-- Feedback sits beside the buttons that caused it, so it's in view after a click near the bottom. -->
        @if (formError()) {
          <div class="alert alert--error" role="alert" id="form-error" style="margin-top:14px;"><span>⚠</span> {{ formError() }}</div>
        }
        @if (savedNote()) {
          <div class="alert alert--success" role="status" style="margin-top:14px;"><span>✓</span> {{ savedNote() }}</div>
        }
        <div class="actions">
          <button type="button" class="btn" (click)="saveDraft()" [disabled]="busy()">Save draft</button>
          <button type="submit" class="btn btn--primary" [disabled]="busy() || !canSubmit()">
            @if (busy()) { <span class="spinner"></span> } Submit request
          </button>
          @if (draft()) {
            @if (confirmingDelete()) {
              <button type="button" class="btn btn--ghost" (click)="deleteDraft()">Yes, delete this draft</button>
              <button type="button" class="btn btn--ghost" (click)="confirmingDelete.set(false)">Keep it</button>
            } @else {
              <button type="button" class="btn btn--ghost" (click)="confirmingDelete.set(true)">Delete draft</button>
            }
          }
        </div>
      </form>
    }
  `,
})
export class RequestFormComponent implements OnInit {
  private api = inject(ResidentArchitecturalService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private location = inject(Location);

  readonly projectTypes = PROJECT_TYPES;
  readonly accept = ACCEPTED_FILE_TYPES;
  readonly size = fileSize;

  model: FormModel = {
    projectType: 'Fence', projectTitle: '', description: '', plannedStartDate: '', plannedCompletionDate: '',
    contractorName: '', contractorContact: '', acknowledged: false,
  };

  draft = signal<ResidentArcDraft | null>(null);
  loading = signal(false);
  loadError = signal<string | null>(null);
  formError = signal<string | null>(null);
  savedNote = signal<string | null>(null);
  busy = signal(false);
  uploading = signal(false);
  uploadErrors = signal<string[]>([]);
  showErrors = signal(false);
  confirmingDelete = signal(false);

  heading = computed(() => this.draft()?.previousDisplayId
    ? `Revise ${this.draft()!.previousDisplayId}`
    : this.draft() ? 'Edit draft request' : 'New architectural request');

  async ngOnInit(): Promise<void> {
    const params = this.route.snapshot.paramMap;
    const draftId = params.get('draftId');
    const reviseOf = this.route.snapshot.url.at(-1)?.path === 'revise' ? params.get('id') : null;
    if (!draftId && !reviseOf) return;

    this.loading.set(true);
    try {
      if (reviseOf) {
        const created = await this.api.revise(reviseOf);
        this.adopt(created);
        this.showDraftUrl(created.id);
      } else {
        this.adopt(await this.api.getDraft(draftId!));
      }
    } catch (err) {
      this.loadError.set(errorMessage(err, 'This request could not be opened.'));
    } finally {
      this.loading.set(false);
    }
  }

  datesOutOfOrder(): boolean {
    return !!this.model.plannedStartDate && !!this.model.plannedCompletionDate
      && this.model.plannedCompletionDate < this.model.plannedStartDate;
  }

  canSubmit(): boolean {
    const m = this.model;
    return !!m.projectTitle.trim() && !!m.description.trim() && !!m.plannedStartDate && !!m.plannedCompletionDate
      && !this.datesOutOfOrder() && m.acknowledged;
  }

  async saveDraft(): Promise<void> {
    if (this.datesOutOfOrder()) {
      this.formError.set("The completion date can't be before the start date.");
      return;
    }
    this.busy.set(true);
    try {
      await this.persist();
      this.savedNote.set('Draft saved. You can come back and finish it any time.');
    } catch (err) {
      this.formError.set(errorMessage(err));
    } finally {
      this.busy.set(false);
    }
  }

  async submit(): Promise<void> {
    this.showErrors.set(true);
    this.savedNote.set(null);
    if (!this.canSubmit()) {
      this.formError.set('Please complete the highlighted fields.');
      return;
    }
    this.busy.set(true);
    try {
      const draft = await this.persist();
      const submitted = await this.api.submitDraft(draft.id);
      await this.router.navigate(['/app/property/architectural', submitted.id]);
    } catch (err) {
      this.formError.set(errorMessage(err));
    } finally {
      this.busy.set(false);
    }
  }

  async onFiles(files: FileList | null): Promise<void> {
    // Copy now: the template clears the input right after this call, and a FileList is live —
    // after the first await it would be empty.
    const picked = Array.from(files ?? []);
    if (!picked.length) return;
    this.uploadErrors.set([]);
    this.uploading.set(true);
    try {
      const draft = this.draft() ?? await this.persist();
      for (const file of picked) {
        try {
          const added = await this.api.uploadDraftAttachment(draft.id, file);
          this.draft.update(d => d ? { ...d, attachments: [...d.attachments, added] } : d);
        } catch (err) {
          this.uploadErrors.update(list => [...list, `${file.name}: ${errorMessage(err)}`]);
        }
      }
    } catch (err) {
      this.uploadErrors.set([errorMessage(err)]);
    } finally {
      this.uploading.set(false);
    }
  }

  async removeOwn(file: ResidentArcAttachment): Promise<void> {
    const draft = this.draft();
    if (!draft) return;
    try {
      await this.api.deleteDraftAttachment(draft.id, file.id);
      this.draft.set({ ...draft, attachments: draft.attachments.filter(a => a.id !== file.id) });
    } catch (err) {
      this.formError.set(errorMessage(err));
    }
  }

  /** Removing a carried-over file only affects this revision; the earlier revision keeps it. */
  async removeCarried(file: ResidentArcAttachment): Promise<void> {
    const draft = this.draft();
    if (!draft) return;
    try {
      this.adopt(await this.api.updateDraft(draft.id, {
        ...this.body(), removedCarriedAttachmentIds: [...draft.removedCarriedAttachmentIds, file.id],
      }));
    } catch (err) {
      this.formError.set(errorMessage(err));
    }
  }

  async open(file: ResidentArcAttachment): Promise<void> {
    const draft = this.draft();
    if (!draft) return;
    try {
      const link = await this.api.draftAttachmentUrl(draft.id, file.id);
      window.open(link.url, '_blank', 'noopener');
    } catch (err) {
      this.formError.set(errorMessage(err));
    }
  }

  async deleteDraft(): Promise<void> {
    const draft = this.draft();
    if (!draft) return;
    try {
      await this.api.deleteDraft(draft.id);
      await this.router.navigate(['/app/property/architectural']);
    } catch (err) {
      this.formError.set(errorMessage(err));
    }
  }

  /** Creates the draft on first save, updates it afterwards. */
  private async persist(): Promise<ResidentArcDraft> {
    this.formError.set(null);
    const existing = this.draft();
    const saved = existing
      ? await this.api.updateDraft(existing.id, { ...this.body(), removedCarriedAttachmentIds: existing.removedCarriedAttachmentIds })
      : await this.api.createDraft(this.body());
    this.adopt(saved);
    if (!existing) this.showDraftUrl(saved.id);
    return saved;
  }

  /**
   * Points the address bar at the draft so a reload reopens it. replaceState, not a router navigation:
   * the drafts route is a different route entry, so navigating would rebuild this component and drop
   * its state (the "Draft saved" note, upload progress).
   */
  private showDraftUrl(draftId: string): void {
    this.location.replaceState(`/app/property/architectural/drafts/${draftId}`);
  }

  private body(): ResidentArcDraftBody {
    const m = this.model;
    return {
      projectType: m.projectType,
      projectTitle: m.projectTitle.trim(),
      description: m.description.trim(),
      plannedStartDate: m.plannedStartDate || null,
      plannedCompletionDate: m.plannedCompletionDate || null,
      contractorName: m.contractorName.trim() || null,
      contractorContact: m.contractorContact.trim() || null,
      acknowledged: m.acknowledged,
    };
  }

  private adopt(d: ResidentArcDraft): void {
    this.draft.set(d);
    this.model = {
      projectType: d.projectType,
      projectTitle: d.projectTitle ?? '',
      description: d.description ?? '',
      plannedStartDate: d.plannedStartDate ?? '',
      plannedCompletionDate: d.plannedCompletionDate ?? '',
      contractorName: d.contractorName ?? '',
      contractorContact: d.contractorContact ?? '',
      acknowledged: d.acknowledged,
    };
  }
}
