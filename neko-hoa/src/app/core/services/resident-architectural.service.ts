import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ResidentArcAttachment, ResidentArcDetail, ResidentArcDraft, ResidentArcDraftBody, ResidentArcLink,
  ResidentArcListResponse,
} from '../models';

// 029: data layer for resident architectural requests. Every route is scoped server-side to the
// caller's active property; nothing here carries a client-chosen property or community.
@Injectable({ providedIn: 'root' })
export class ResidentArchitecturalService {
  private http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/property/architectural-applications`;

  list(limit = 25, offset = 0): Promise<ResidentArcListResponse> {
    const params = new HttpParams().set('limit', String(limit)).set('offset', String(offset));
    return firstValueFrom(this.http.get<ResidentArcListResponse>(this.base, { params }));
  }

  detail(id: string): Promise<ResidentArcDetail> {
    return firstValueFrom(this.http.get<ResidentArcDetail>(`${this.base}/${id}`));
  }

  attachmentUrl(id: string, attachmentId: string): Promise<ResidentArcLink> {
    return firstValueFrom(this.http.get<ResidentArcLink>(`${this.base}/${id}/attachments/${attachmentId}/url`));
  }

  // ── Drafts ────────────────────────────────────────────────────────────────
  createDraft(body: ResidentArcDraftBody): Promise<ResidentArcDraft> {
    return firstValueFrom(this.http.post<ResidentArcDraft>(`${this.base}/drafts`, body));
  }

  getDraft(draftId: string): Promise<ResidentArcDraft> {
    return firstValueFrom(this.http.get<ResidentArcDraft>(`${this.base}/drafts/${draftId}`));
  }

  updateDraft(draftId: string, body: ResidentArcDraftBody): Promise<ResidentArcDraft> {
    return firstValueFrom(this.http.put<ResidentArcDraft>(`${this.base}/drafts/${draftId}`, body));
  }

  deleteDraft(draftId: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.base}/drafts/${draftId}`));
  }

  uploadDraftAttachment(draftId: string, file: File): Promise<ResidentArcAttachment> {
    return firstValueFrom(this.http.post<ResidentArcAttachment>(`${this.base}/drafts/${draftId}/attachments`, this.form(file)));
  }

  deleteDraftAttachment(draftId: string, attachmentId: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.base}/drafts/${draftId}/attachments/${attachmentId}`));
  }

  draftAttachmentUrl(draftId: string, attachmentId: string): Promise<ResidentArcLink> {
    return firstValueFrom(this.http.get<ResidentArcLink>(`${this.base}/drafts/${draftId}/attachments/${attachmentId}/url`));
  }

  submitDraft(draftId: string): Promise<ResidentArcDetail> {
    return firstValueFrom(this.http.post<ResidentArcDetail>(`${this.base}/drafts/${draftId}/submit`, null));
  }

  // ── Actions on a submitted request ────────────────────────────────────────
  uploadReplyAttachment(id: string, infoRequestId: string, file: File): Promise<ResidentArcAttachment> {
    return firstValueFrom(this.http.post<ResidentArcAttachment>(
      `${this.base}/${id}/info-requests/${infoRequestId}/attachments`, this.form(file)));
  }

  reply(id: string, infoRequestId: string, responseMessage: string): Promise<ResidentArcDetail> {
    return firstValueFrom(this.http.post<ResidentArcDetail>(
      `${this.base}/${id}/info-requests/${infoRequestId}/reply`, { responseMessage }));
  }

  withdraw(id: string): Promise<ResidentArcDetail> {
    return firstValueFrom(this.http.post<ResidentArcDetail>(`${this.base}/${id}/withdraw`, null));
  }

  revise(id: string): Promise<ResidentArcDraft> {
    return firstValueFrom(this.http.post<ResidentArcDraft>(`${this.base}/${id}/revise`, null));
  }

  private form(file: File): FormData {
    const data = new FormData();
    data.append('file', file, file.name);
    return data;
  }
}
