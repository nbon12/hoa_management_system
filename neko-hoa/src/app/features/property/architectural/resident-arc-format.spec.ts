import { HttpErrorResponse } from '@angular/common/http';
import { errorMessage, fileSize, projectTypeLabel, statusLabel, statusTone } from './resident-arc-format';
import { ResidentArcStatus } from '../../../core/models';

describe('resident-arc-format (029)', () => {
  it('labels every resident status (data-model status projection)', () => {
    const expected: Record<ResidentArcStatus, string> = {
      Draft: 'Draft', Submitted: 'Submitted / Under review', MoreInfoRequested: 'More info requested',
      Approved: 'Approved', Denied: 'Denied', Withdrawn: 'Withdrawn',
    };
    for (const [status, label] of Object.entries(expected))
      expect(statusLabel(status as ResidentArcStatus)).toBe(label);
  });

  it('gives tone classes but never relies on them alone', () => {
    expect(statusTone('Approved')).toBe('pill--ok');
    expect(statusTone('Denied')).toBe('pill--warn');
    expect(statusTone('Draft')).toBe('');
  });

  it('uses the spec display names for project types (FR-002)', () => {
    expect(projectTypeLabel('ExteriorPaint')).toBe('Exterior paint');
    expect(projectTypeLabel('Outbuilding')).toBe('Shed / outbuilding');
    expect(projectTypeLabel('WindowsDoors')).toBe('Windows / doors');
  });

  it('maps every contract error code to a message', () => {
    const codes = ['ACKNOWLEDGEMENT_REQUIRED', 'UNSUPPORTED_FILE_TYPE', 'FILE_TOO_LARGE', 'ATTACHMENT_LIMIT_REACHED',
      'STORAGE_UNAVAILABLE', 'INFO_ALREADY_ANSWERED', 'APPLICATION_DECIDED', 'APPLICATION_CLOSED',
      'REVISION_NOT_ALLOWED', 'ATTACHMENT_UNAVAILABLE', 'FORBIDDEN'];
    for (const code of codes) {
      const msg = errorMessage(new HttpErrorResponse({ status: 422, error: { code, message: 'raw' } }));
      expect(msg).not.toBe('raw');
      expect(msg.length).toBeGreaterThan(10);
    }
  });

  it('falls back to the server message, then to a generic one; 429 has its own message', () => {
    expect(errorMessage(new HttpErrorResponse({ status: 422, error: { code: 'VALIDATION_ERROR', message: 'title too long' } })))
      .toBe('title too long');
    expect(errorMessage(new Error('boom'), 'fallback')).toBe('fallback');
    expect(errorMessage(new HttpErrorResponse({ status: 429 }))).toContain('wait a minute');
  });

  it('formats file sizes', () => {
    expect(fileSize(840)).toBe('840 B');
    expect(fileSize(860_160)).toBe('840 KB');
    expect(fileSize(1_258_291)).toBe('1.2 MB');
  });
});
