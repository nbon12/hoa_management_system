import { Injectable } from '@angular/core';
import { MockDataService } from './mock-data.service';
import { AnnouncementCategory, EventCategory } from '../models';

@Injectable({ providedIn: 'root' })
export class CommunityService {
  constructor(private mock: MockDataService) {}

  getAnnouncements(category?: AnnouncementCategory) {
    if (!category) return [...this.mock.announcements];
    return this.mock.announcements.filter(a => a.category === category);
  }
  getPoll() { return this.mock.poll; }

  getViolations(status?: 'open' | 'closed') {
    if (!status) return [...this.mock.violations];
    return this.mock.violations.filter(v => v.status === status);
  }

  getCalendarEvents(category?: EventCategory) {
    if (!category) return [...this.mock.calendarEvents];
    return this.mock.calendarEvents.filter(e => e.category === category);
  }

  getDocuments(category?: string) {
    if (!category || category === 'All') return [...this.mock.documents];
    if (category === 'Pinned') return this.mock.documents.filter(d => d.pinned);
    return this.mock.documents.filter(d => d.category === category);
  }

  searchDocuments(query: string) {
    const q = query.toLowerCase();
    return this.mock.documents.filter(d => d.name.toLowerCase().includes(q));
  }
}
