import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { DatePipe } from '@angular/common';
import { CommunityService } from '../../../core/services/community.service';
import { CalendarEvent, EventCategory } from '../../../core/models';

const EVENT_COLORS: Record<EventCategory, string> = {
  Board:       'var(--lav-2)',
  Amenity:     'var(--pink-2)',
  Social:      'var(--social)',
  Maintenance: 'var(--maintenance)',
};

@Component({
  selector: 'app-calendar',
  standalone: true,
  imports: [DatePipe],
  template: `
    <div class="page-header">
      <h1 class="page-title">Community <span class="hand">calendar</span></h1>
      <div class="page-header__actions">
        <button class="btn btn--ghost" (click)="prevMonth()">‹</button>
        <b style="min-width:100px;text-align:center;">{{ monthLabel() }}</b>
        <button class="btn btn--ghost" (click)="nextMonth()">›</button>
        <span style="border-left:1.5px dashed var(--line);padding-left:8px;margin-left:8px;display:flex;gap:6px;">
          <button class="tab" [class.tab--active]="view() === 'month'" (click)="view.set('month')">Month</button>
          <button class="tab" [class.tab--active]="view() === 'timeline'" (click)="view.set('timeline')">Timeline</button>
        </span>
        <button class="btn btn--primary">+ Subscribe</button>
      </div>
    </div>

    <!-- Category filter pills -->
    <div class="card card--lav" style="display:flex;gap:8px;align-items:center;flex-wrap:wrap;">
      <span class="muted">Filter:</span>
      @for (cat of categories; track cat) {
        <span class="pill" style="cursor:pointer;"
              [style.background]="activeCategories().has(cat) ? EVENT_COLORS[cat] : 'var(--paper)'"
              [style.color]="activeCategories().has(cat) ? 'var(--ink)' : 'var(--ink-mute)'"
              (click)="toggleCategory(cat)">
          {{ activeCategories().has(cat) ? '✓' : '○' }} {{ cat }}
        </span>
      }
      <button class="btn btn--ghost" style="margin-left:auto;font-size:11px;">📅 Subscribe (.ics)</button>
    </div>

    @if (view() === 'month') {
      <!-- Month grid -->
      <div class="grid-2" style="grid-template-columns:2fr 1fr;align-items:start;">
        <div class="card" style="padding:14px;">
          <!-- Day headers -->
          <div style="display:grid;grid-template-columns:repeat(7,1fr);font-size:11px;color:var(--ink-soft);text-transform:uppercase;letter-spacing:.08em;border-bottom:1.5px dashed var(--line);padding-bottom:8px;">
            @for (d of dayHeaders; track d) {
              <div style="text-align:center;">{{ d }}</div>
            }
          </div>

          <!-- Calendar days -->
          <div style="display:grid;grid-template-columns:repeat(7,1fr);gap:4px;margin-top:6px;">
            @for (day of calendarDays(); track day.key) {
              <div style="aspect-ratio:1.1;padding:6px;border-radius:8px;border:1.5px solid;font-size:11px;display:flex;flex-direction:column;gap:4px;min-height:52px;"
                   [style.border-color]="day.isToday ? 'var(--ink)' : 'var(--line-soft)'"
                   [style.background]="day.isToday ? 'var(--pink)' : 'var(--paper)'"
                   [style.opacity]="day.otherMonth ? 0.4 : 1">
                <div [style.font-weight]="day.isToday ? 700 : 500">{{ day.date }}</div>
                @for (ev of day.events; track ev.id) {
                  <div class="pill" style="font-size:10px;padding:2px 6px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;"
                       [style.background]="EVENT_COLORS[ev.category]">
                    {{ ev.title }}
                  </div>
                }
              </div>
            }
          </div>

          <!-- Legend -->
          <div style="display:flex;gap:14px;margin-top:14px;font-size:11px;color:var(--ink-soft);flex-wrap:wrap;">
            @for (cat of categories; track cat) {
              <span>
                <span style="display:inline-block;width:10px;height:10px;border-radius:3px;margin-right:6px;"
                      [style.background]="EVENT_COLORS[cat]"></span>
                {{ cat }}
              </span>
            }
          </div>
        </div>

        <!-- Upcoming list -->
        <div class="card card--lav">
          <div class="section-title">Upcoming · next 60 days</div>
          <div style="display:flex;flex-direction:column;gap:8px;margin-top:8px;">
            @for (ev of upcomingEvents(); track ev.id) {
              <div style="display:flex;gap:10px;padding:8px 10px;border:1.5px dashed var(--line);background:var(--paper);border-radius:10px;">
                <div style="width:48px;text-align:center;flex-shrink:0;">
                  <div style="font-size:10px;color:var(--ink-soft);text-transform:uppercase;">{{ ev.date | date:'MMM' }}</div>
                  <div class="mono" style="font-size:18px;font-weight:600;">{{ ev.date | date:'d' }}</div>
                </div>
                <div style="flex:1;">
                  <div style="font-size:12px;font-weight:500;">{{ ev.title }}</div>
                  <div class="muted" style="font-size:11px;">{{ ev.location }}</div>
                </div>
                <span class="pill" style="align-self:flex-start;font-size:10px;"
                      [style.background]="EVENT_COLORS[ev.category]">
                  {{ ev.category }}
                </span>
              </div>
            }
          </div>
        </div>
      </div>
    }

    @if (view() === 'timeline') {
      <!-- Timeline view -->
      <div class="card" style="padding:0;overflow:hidden;">
        @for (month of timelineMonths(); track month) {
          <div style="display:grid;grid-template-columns:80px 1fr;border-bottom:1.5px dashed var(--line);">
            <div style="padding:18px 14px;border-right:1.5px dashed var(--line);background:var(--lav);">
              <div class="hand" style="font-size:24px;">{{ month }}</div>
              <div class="muted" style="font-size:11px;">2026</div>
            </div>
            <div style="padding:10px 14px;display:flex;flex-direction:column;gap:6px;">
              @for (ev of eventsForMonth(month); track ev.id) {
                <div style="display:flex;gap:14px;padding:8px 10px;border-radius:8px;align-items:center;background:var(--paper);border:1.5px solid var(--line-soft);">
                  <div class="mono" style="font-size:18px;font-weight:600;width:40px;text-align:center;flex-shrink:0;">
                    {{ ev.date | date:'d' }}
                  </div>
                  <div style="width:6px;height:32px;border-radius:3px;flex-shrink:0;"
                       [style.background]="EVENT_COLORS[ev.category]"></div>
                  <div style="flex:1;">
                    <div style="font-size:13px;font-weight:500;">{{ ev.title }}</div>
                    <div class="muted" style="font-size:11px;">{{ ev.location }}</div>
                  </div>
                  <span class="pill" style="font-size:10px;"
                        [style.background]="EVENT_COLORS[ev.category]">{{ ev.category }}</span>
                  @if (ev.rsvpEnabled) {
                    <span class="link" style="font-size:11px;">RSVP</span>
                  }
                </div>
              }
              @if (eventsForMonth(month).length === 0) {
                <div class="muted" style="padding:8px 10px;font-size:11px;">nothing scheduled</div>
              }
            </div>
          </div>
        }
      </div>
    }

    <!-- Subscribe card -->
    <div class="card card--dashed" style="max-width:400px;">
      <div class="section-title">📅 Subscribe</div>
      <p class="muted" style="font-size:11px;">Add Sakura Heights events to Google / Apple calendar.</p>
      <div class="field field--dashed mono" style="font-size:10px;margin-top:6px;color:var(--ink-mute);">
        https://nekohoa.com/cal/sakura.ics
      </div>
      <div style="display:flex;gap:6px;margin-top:8px;flex-wrap:wrap;">
        <button class="btn btn--ghost" style="font-size:11px;">Copy link</button>
        <button class="btn btn--ghost" style="font-size:11px;">+ Google</button>
        <button class="btn btn--ghost" style="font-size:11px;">+ Apple</button>
      </div>
    </div>
  `
})
export class CalendarComponent implements OnInit {
  private svc = inject(CommunityService);

  EVENT_COLORS = EVENT_COLORS;
  categories: EventCategory[] = ['Board', 'Amenity', 'Social', 'Maintenance'];
  dayHeaders = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];

  view       = signal<'month' | 'timeline'>('month');
  currentYear  = signal(new Date().getFullYear());
  currentMonth = signal(new Date().getMonth());
  activeCategories = signal<Set<EventCategory>>(new Set(this.categories));
  private _allEvents = signal<CalendarEvent[]>([]);

  async ngOnInit() {
    this._allEvents.set(await this.svc.getCalendarEvents());
  }

  // Timeline months
  timelineMonths = computed(() => {
    const months = ['May', 'Jun', 'Jul', 'Aug', 'Sep'];
    return months;
  });

  monthLabel = computed(() => {
    const d = new Date(this.currentYear(), this.currentMonth(), 1);
    return d.toLocaleString('default', { month: 'long', year: 'numeric' });
  });

  prevMonth() {
    if (this.currentMonth() === 0) { this.currentYear.set(this.currentYear() - 1); this.currentMonth.set(11); }
    else this.currentMonth.set(this.currentMonth() - 1);
  }
  nextMonth() {
    if (this.currentMonth() === 11) { this.currentYear.set(this.currentYear() + 1); this.currentMonth.set(0); }
    else this.currentMonth.set(this.currentMonth() + 1);
  }

  toggleCategory(cat: EventCategory) {
    const s = new Set(this.activeCategories());
    s.has(cat) ? s.delete(cat) : s.add(cat);
    this.activeCategories.set(s);
  }

  private get visibleEvents(): CalendarEvent[] {
    return this._allEvents().filter(e => this.activeCategories().has(e.category));
  }

  calendarDays = computed(() => {
    const y = this.currentYear(), m = this.currentMonth();
    const firstDay = new Date(y, m, 1).getDay();
    const daysInMonth = new Date(y, m + 1, 0).getDate();
    const daysInPrev  = new Date(y, m, 0).getDate();
    const today = new Date();

    const days: { key: string; date: number; isToday: boolean; otherMonth: boolean; events: CalendarEvent[] }[] = [];

    for (let i = firstDay - 1; i >= 0; i--) {
      const d = daysInPrev - i;
      days.push({ key: `prev-${d}`, date: d, isToday: false, otherMonth: true, events: [] });
    }
    for (let d = 1; d <= daysInMonth; d++) {
      const dateStr = `${y}-${String(m + 1).padStart(2, '0')}-${String(d).padStart(2, '0')}`;
      const isToday = today.getFullYear() === y && today.getMonth() === m && today.getDate() === d;
      const events = this.visibleEvents.filter(e => e.date === dateStr);
      days.push({ key: dateStr, date: d, isToday, otherMonth: false, events });
    }
    const remaining = 42 - days.length;
    for (let d = 1; d <= remaining; d++) {
      days.push({ key: `next-${d}`, date: d, isToday: false, otherMonth: true, events: [] });
    }
    return days;
  });

  upcomingEvents = computed(() => {
    const now = new Date().toISOString().split('T')[0];
    const cutoff = new Date();
    cutoff.setDate(cutoff.getDate() + 60);
    const cutoffStr = cutoff.toISOString().split('T')[0];
    return this.visibleEvents
      .filter(e => e.date >= now && e.date <= cutoffStr)
      .sort((a, b) => a.date.localeCompare(b.date))
      .slice(0, 6);
  });

  eventsForMonth(monthName: string): CalendarEvent[] {
    const idx = ['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec'].indexOf(monthName);
    return this.visibleEvents
      .filter(e => {
        const d = new Date(e.date);
        return d.getMonth() === idx;
      })
      .sort((a, b) => a.date.localeCompare(b.date));
  }
}
