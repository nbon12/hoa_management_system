import { HttpErrorResponse } from '@angular/common/http';
import { ArcProjectType, ResidentArcStatus } from '../../../core/models';

// 029: labels and messages for the resident architectural pages.

export const PROJECT_TYPES: { value: ArcProjectType; label: string }[] = [
  { value: 'Fence', label: 'Fence' },
  { value: 'Solar', label: 'Solar' },
  { value: 'ExteriorPaint', label: 'Exterior paint' },
  { value: 'Outbuilding', label: 'Shed / outbuilding' },
  { value: 'Landscaping', label: 'Landscaping' },
  { value: 'WindowsDoors', label: 'Windows / doors' },
  { value: 'Addition', label: 'Addition' },
  { value: 'Other', label: 'Other' },
];

export function projectTypeLabel(type: ArcProjectType): string {
  return PROJECT_TYPES.find(t => t.value === type)?.label ?? type;
}

const STATUS_LABELS: Record<ResidentArcStatus, string> = {
  Draft: 'Draft',
  Submitted: 'Submitted / Under review',
  MoreInfoRequested: 'More info requested',
  Approved: 'Approved',
  Denied: 'Denied',
  Withdrawn: 'Withdrawn',
};

export function statusLabel(status: ResidentArcStatus): string {
  return STATUS_LABELS[status] ?? status;
}

/** Pill modifier for a status; the label text carries the meaning, so color is never the only signal. */
export function statusTone(status: ResidentArcStatus): string {
  switch (status) {
    case 'Approved': return 'pill--ok';
    case 'Denied': return 'pill--warn';
    case 'MoreInfoRequested': return 'pill--warn';
    default: return '';
  }
}

const ERROR_MESSAGES: Record<string, string> = {
  ACKNOWLEDGEMENT_REQUIRED: 'Please confirm that work may not begin until this request is approved.',
  UNSUPPORTED_FILE_TYPE: 'Only PDF, JPG, PNG and HEIC files can be attached.',
  FILE_TOO_LARGE: 'That file is larger than the upload limit.',
  ATTACHMENT_LIMIT_REACHED: 'This request already has the most files (or total size) allowed.',
  STORAGE_UNAVAILABLE: 'File storage is temporarily unavailable. Please try again in a moment.',
  INFO_ALREADY_ANSWERED: 'This question has already been answered.',
  APPLICATION_DECIDED: 'The board has already reached a decision on this request.',
  APPLICATION_CLOSED: 'This request is closed.',
  REVISION_NOT_ALLOWED: 'This request can no longer be revised.',
  ATTACHMENT_UNAVAILABLE: 'This attachment is unavailable.',
  FORBIDDEN: 'You do not have access to this request.',
};

/** A user-facing message for an API error: the mapped code, else the server message, else a fallback. */
export function errorMessage(err: unknown, fallback = 'Something went wrong. Please try again.'): string {
  if (err instanceof HttpErrorResponse) {
    if (err.status === 429) return 'Too many changes in a short time. Please wait a minute and try again.';
    const code = err.error?.code as string | undefined;
    if (code && ERROR_MESSAGES[code]) return ERROR_MESSAGES[code];
    if (typeof err.error?.message === 'string') return err.error.message;
  }
  return fallback;
}

export function fileSize(bytes: number): string {
  if (bytes >= 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  if (bytes >= 1024) return `${Math.round(bytes / 1024)} KB`;
  return `${bytes} B`;
}

/** Client-side UX hint only — the server enforces the real (environment-configured) limits by content. */
export const ACCEPTED_FILE_TYPES = '.pdf,.jpg,.jpeg,.png,.heic,.heif,application/pdf,image/jpeg,image/png,image/heic';
