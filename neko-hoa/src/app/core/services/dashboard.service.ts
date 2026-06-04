import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { DashboardSummary, Announcement, CalendarEvent, LedgerEntry } from '../models';

// Shapes returned by the API (camelCase from .NET System.Text.Json)
interface ApiDashboard {
  currentBalance:       number;
  balanceDueDate:       string;
  openViolations:       number;
  documentCount:        number;
  newDocumentsThisMonth: number;
  pinnedAnnouncement:   ApiAnnouncement | null;
  thisWeekEvents:       ApiEvent[];
  nextEvent:            ApiEvent | null;
  recentActivity:       ApiLedger[];
  communityExpenses:    ApiExpense[];
}

interface ApiAnnouncement {
  id: string; title: string; body: string; category: string;
  publishedAt: string; authorName: string;
}

interface ApiEvent {
  id: string; title: string; eventDate: string;
  location: string | null; category: string; rsvpEnabled: boolean;
}

interface ApiLedger {
  id: string; entryDate: string; description: string;
  chargeAmount: number; paymentAmount: number; runningBalance: number; entryType: string;
}

interface ApiExpense { id: string; label: string; color: string; amount: number; }

@Injectable({ providedIn: 'root' })
export class DashboardService {
  private readonly base = environment.apiBaseUrl;

  constructor(private http: HttpClient) {}

  async getSummary(): Promise<DashboardSummary> {
    const api = await firstValueFrom(
      this.http.get<ApiDashboard>(`${this.base}/dashboard`)
    );
    return this._map(api);
  }

  private _map(api: ApiDashboard): DashboardSummary {
    const pinned = api.pinnedAnnouncement;
    const pinnedAnnouncement: Announcement | null = pinned ? {
      id:            pinned.id,
      title:         pinned.title,
      body:          pinned.body,
      date:          pinned.publishedAt.split('T')[0],
      category:      pinned.category as any,
      pinned:        true,
      commentCount:  0,
      likeCount:     0,
      imageUrl:      null,
      authorInitials: pinned.authorName.split(' ').map(w => w[0]).join('').toUpperCase().slice(0, 2),
      authorLabel:   pinned.authorName,
    } : null;

    const thisWeekEvents: CalendarEvent[] = api.thisWeekEvents.map(e => ({
      id:          e.id,
      title:       e.title,
      date:        e.eventDate.split('T')[0],
      location:    e.location ?? '',
      category:    e.category as any,
      rsvpEnabled: e.rsvpEnabled,
    }));

    const recentActivity: LedgerEntry[] = api.recentActivity.map(e => ({
      id:          e.id,
      type:        this._mapEntryType(e.entryType),
      date:        e.entryDate,
      docNumber:   '',
      description: e.description,
      charge:      e.chargeAmount || null,
      payment:     e.paymentAmount || null,
      balance:     e.runningBalance,
    }));

    return {
      currentBalance:       api.currentBalance,
      balanceDueDate:       api.balanceDueDate,
      openViolations:       api.openViolations,
      documentCount:        api.documentCount,
      newDocumentsThisMonth: api.newDocumentsThisMonth,
      pinnedAnnouncement,
      thisWeekEvents,
      nextEvent: api.nextEvent ? {
        title: api.nextEvent.title,
        date:  api.nextEvent.eventDate.split('T')[0],
      } : null,
      recentActivity,
      communityExpenses: api.communityExpenses.map(e => ({
        label:  e.label,
        color:  e.color,
        amount: e.amount,
      })),
    };
  }

  private _mapEntryType(t: string): any {
    const map: Record<string, string> = {
      RegularAssessment: 'Regular Assessment',
      Payment:           'Payment',
      LateFee:           'Late Fee',
      FinanceCharge:     'Finance Charge',
    };
    return map[t] ?? t;
  }
}
