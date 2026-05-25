import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
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
  private readonly base = environment.apiBaseUrl;

  constructor(private http: HttpClient) {}

  // ── Announcements ─────────────────────────────────────────────────────────

  async getAnnouncements(category?: AnnouncementCategory): Promise<Announcement[]> {
    let params = new HttpParams().set('pageSize', '50');
    if (category) params = params.set('category', category);
    const res = await firstValueFrom(
      this.http.get<ApiPage<ApiAnnouncement>>(`${this.base}/community/announcements`, { params })
    );
    return res.items.map(a => this._mapAnnouncement(a));
  }

  async getPoll(): Promise<Poll | null> {
    try {
      const p = await firstValueFrom(
        this.http.get<ApiPoll>(`${this.base}/community/poll`)
      );
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
    const res = await firstValueFrom(
      this.http.get<ApiPage<ApiViolation>>(`${this.base}/community/violations`, { params })
    );
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
    const res = await firstValueFrom(
      this.http.get<ApiPage<ApiEvent>>(`${this.base}/community/events`, { params })
    );
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
    const res = await firstValueFrom(
      this.http.get<ApiPage<ApiDocument>>(`${this.base}/community/documents`, { params })
    );
    return res.items.map(d => this._mapDocument(d));
  }

  async searchDocuments(query: string): Promise<HOADocument[]> {
    const params = new HttpParams().set('search', query).set('pageSize', '200');
    const res = await firstValueFrom(
      this.http.get<ApiPage<ApiDocument>>(`${this.base}/community/documents`, { params })
    );
    return res.items.map(d => this._mapDocument(d));
  }

  async getDocumentDownloadUrl(documentId: string): Promise<{ url: string; expiresAt: string }> {
    return firstValueFrom(
      this.http.get<{ url: string; expiresAt: string }>(
        `${this.base}/community/documents/${documentId}/download`,
      ),
    );
  }

  async getCommunityDirectory(): Promise<{ neighbors: any[]; totalSharing: number; totalHouseholds: number }> {
    return firstValueFrom(
      this.http.get<{ neighbors: any[]; totalSharing: number; totalHouseholds: number }>(
        `${this.base}/community/directory`
      )
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
