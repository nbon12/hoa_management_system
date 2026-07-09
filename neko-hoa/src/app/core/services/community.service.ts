import { Injectable } from '@angular/core';
import { HttpParams } from '@angular/common/http';
import { ApiClient } from '../api/api-client';
import {
  Announcement, AnnouncementCategory,
  Poll, Violation, ViolationStatus, ViolationCategory,
  CalendarEvent, EventCategory,
  HOADocument, DocumentCategory,
} from '../models';

// ── Paginated API response wrapper ──────────────────────────────────────────
interface ApiPage<T> { items: T[]; totalCount: number; page: number; pageSize: number; }

interface ApiAnnouncement {
  id: string; title: string; body: string; category: string;
  publishedAt: string; pinned: boolean; authorName: string;
  likeCount: number; commentCount: number;
}

interface ApiPoll {
  id: string;
  question: string;
  closingLabel: string;
  totalVotes: number;
  options: { optionIndex: number; optionText: string; voteCount: number; percentage: number }[];
}

interface ApiViolation {
  id: string; title: string; description: string | null;
  category: string; status: string;
  issuedDate: string; resolvedDate: string | null; dueDate: string | null;
  fineAmount: number | null;
}

interface ApiEvent {
  id: string; title: string; description: string | null;
  eventDate: string; location: string | null; category: string;
  rsvpEnabled: boolean; rsvpCount: number;
}

interface ApiDocument {
  id: string; name: string; category: string; effectiveDate: string;
  fileSizeLabel: string; pinned: boolean;
}

@Injectable({ providedIn: 'root' })
export class CommunityService {
  constructor(private api: ApiClient) {}

  // ── Announcements ─────────────────────────────────────────────────────────

  async getAnnouncements(category?: AnnouncementCategory): Promise<Announcement[]> {
    let params = new HttpParams().set('pageSize', '50');
    if (category) params = params.set('category', category);
    const res = await this.api.get<ApiPage<ApiAnnouncement>>('/community/announcements', params);
    return res.items.map(a => this._mapAnnouncement(a));
  }

  async getPoll(): Promise<Poll | null> {
    try {
      const p = await this.api.get<ApiPoll>('/community/poll');
      return {
        question:    p.question,
        options:     p.options.map(o => ({ label: o.optionText, percent: Math.round(Number(o.percentage)) })),
        totalVotes:  p.totalVotes,
        closesLabel: p.closingLabel,
      };
    } catch { return null; }
  }

  // ── Violations ────────────────────────────────────────────────────────────

  async getViolations(status?: 'open' | 'closed'): Promise<Violation[]> {
    let params = new HttpParams().set('pageSize', '100');
    if (status) params = params.set('status', status);
    const res = await this.api.get<ApiPage<ApiViolation>>('/community/violations', params);
    return res.items.map(v => ({
      id:       v.id,
      issue:    v.title,
      date:     v.issuedDate,
      category: v.category as ViolationCategory,
      status:   v.status.toLowerCase() as ViolationStatus,
    }));
  }

  // ── Calendar ──────────────────────────────────────────────────────────────

  async getCalendarEvents(category?: EventCategory): Promise<CalendarEvent[]> {
    let params = new HttpParams().set('pageSize', '100');
    if (category) params = params.set('category', category);
    const res = await this.api.get<ApiPage<ApiEvent>>('/community/events', params);
    return res.items.map(e => ({
      id:          e.id,
      title:       e.title,
      date:        e.eventDate.split('T')[0],
      location:    e.location ?? '',
      category:    e.category as EventCategory,
      rsvpEnabled: e.rsvpEnabled,
    }));
  }

  // ── Documents ─────────────────────────────────────────────────────────────

  async getDocuments(category?: string): Promise<HOADocument[]> {
    let params = new HttpParams().set('pageSize', '200');
    if (category && category !== 'All') {
      if (category === 'Pinned') params = params.set('pinned', 'true');
      else params = params.set('category', category);
    }
    const res = await this.api.get<ApiPage<ApiDocument>>('/community/documents', params);
    return res.items.map(d => this._mapDocument(d));
  }

  async searchDocuments(query: string): Promise<HOADocument[]> {
    const params = new HttpParams().set('search', query).set('pageSize', '200');
    const res = await this.api.get<ApiPage<ApiDocument>>('/community/documents', params);
    return res.items.map(d => this._mapDocument(d));
  }

  async getDocumentDownloadUrl(documentId: string): Promise<{ url: string; expiresAt: string }> {
    return this.api.get<{ url: string; expiresAt: string }>(
      `/community/documents/${documentId}/download`,
    );
  }

  async getCommunityDirectory(): Promise<{ neighbors: any[]; totalSharing: number; totalHouseholds: number }> {
    return this.api.get<{ neighbors: any[]; totalSharing: number; totalHouseholds: number }>(
      '/community/directory'
    );
  }

  // ── Mappers ───────────────────────────────────────────────────────────────

  private _mapAnnouncement(a: ApiAnnouncement): Announcement {
    const nameParts = a.authorName.split(' ');
    const initials = nameParts.map(w => w[0]).join('').toUpperCase().slice(0, 2);
    return {
      id:             a.id,
      title:          a.title,
      body:           a.body,
      date:           a.publishedAt.split('T')[0],
      category:       a.category as AnnouncementCategory,
      pinned:         a.pinned,
      commentCount:   a.commentCount,
      likeCount:      a.likeCount,
      imageUrl:       null,
      authorInitials: initials,
      authorLabel:    a.authorName,
    };
  }

  private _mapDocument(d: ApiDocument): HOADocument {
    return {
      id:            d.id,
      name:          d.name,
      category:      d.category as DocumentCategory,
      effectiveDate: d.effectiveDate,
      fileSizeLabel: d.fileSizeLabel,
      pinned:        d.pinned,
      url:           null,
    };
  }
}
