import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { DatePipe } from '@angular/common';
import { CommunityService } from '../../../core/services/community.service';
import { AnnouncementCategory, Announcement, Poll } from '../../../core/models';

const CATEGORIES: (AnnouncementCategory | 'All')[] = ['All', 'Board', 'Maintenance', 'Events', 'Emergencies'];

@Component({
  selector: 'app-announcements',
  standalone: true,
  imports: [DatePipe],
  template: `
    <div class="page-header">
      <h1 class="page-title">Community <span class="hand">feed</span></h1>
      <div class="page-header__actions">
        <button class="btn btn--ghost">📧 Subscribe</button>
      </div>
    </div>

    <!-- Category tabs -->
    <div class="tab-bar">
      @for (cat of categories; track cat) {
        <button class="tab" [class.tab--active]="activeCategory() === cat"
                (click)="activeCategory.set(cat)">
          {{ cat }}
        </button>
      }
    </div>

    <!-- Feed + sidebar -->
    <div class="grid-2" style="grid-template-columns:2fr 1fr;align-items:start;">

      <!-- Announcement cards -->
      <div class="flex-col">
        @for (a of filtered(); track a.id) {
          <div class="card" [class.card--pink]="a.pinned">
            <div style="display:flex;align-items:center;gap:8px;flex-wrap:wrap;">
              <div class="avatar">{{ a.authorInitials }}</div>
              <div>
                <div style="font-size:13px;font-weight:500;">{{ a.authorLabel }}</div>
                <div class="muted" style="font-size:11px;">{{ a.date | date:'MMM d · h:mm a' }}</div>
              </div>
              @if (a.pinned) { <span class="pill pill--warn" style="margin-left:auto;">📌 pinned</span> }
              <span class="pill" style="margin-left:auto;background:{{ categoryColor(a.category) }}">{{ a.category }}</span>
            </div>
            <div style="font-size:16px;font-weight:600;margin-top:8px;">{{ a.title }}</div>
            <p class="muted" style="margin:6px 0 0;font-size:12px;">{{ a.body }}</p>
            <div style="display:flex;gap:14px;margin-top:10px;font-size:11px;">
              <span class="link">Read more</span>
              <span class="muted">·</span>
              <span class="link">💬 {{ a.commentCount }}</span>
              <span class="link">❤ {{ a.likeCount }}</span>
            </div>
          </div>
        }
        @empty {
          <div class="card" style="text-align:center;padding:40px;color:var(--ink-mute);">
            No announcements in this category.
          </div>
        }
      </div>

      <!-- Sidebar: poll -->
      <div class="flex-col">
        <div class="card card--lav">
          <div class="section-title">📊 Quick poll</div>
          <p style="font-size:13px;font-weight:500;margin-top:4px;">{{ poll()?.question }}</p>
          <div style="display:flex;flex-direction:column;gap:6px;margin-top:8px;font-size:12px;">
            @for (opt of (poll()?.options ?? []); track opt.label) {
              <div style="position:relative;padding:8px 10px;border:1.5px solid var(--line);border-radius:8px;overflow:hidden;">
                <div style="position:absolute;inset:0;background:var(--pink);"
                     [style.width]="opt.percent + '%'"></div>
                <div style="position:relative;display:flex;">
                  <span>{{ opt.label }}</span>
                  <span style="margin-left:auto;font-family:'Geist Mono',monospace;">{{ opt.percent }}%</span>
                </div>
              </div>
            }
          </div>
          <div class="muted" style="font-size:11px;margin-top:6px;">{{ poll()?.totalVotes }} votes · {{ poll()?.closesLabel }}</div>
        </div>
      </div>
    </div>
  `
})
export class AnnouncementsComponent implements OnInit {
  private svc = inject(CommunityService);

  categories = CATEGORIES;
  activeCategory = signal<AnnouncementCategory | 'All'>('All');
  poll = signal<Poll | null>(null);
  private _allAnnouncements = signal<Announcement[]>([]);

  async ngOnInit() {
    const [ann, poll] = await Promise.all([
      this.svc.getAnnouncements(),
      this.svc.getPoll(),
    ]);
    this._allAnnouncements.set(ann);
    this.poll.set(poll);
  }

  filtered = computed(() => {
    const cat = this.activeCategory();
    const all = this._allAnnouncements();
    return cat === 'All' ? all : all.filter(a => a.category === cat);
  });

  categoryColor(cat: string): string {
    const map: Record<string, string> = {
      Board: 'var(--lav-2)', Events: 'var(--pink-2)',
      Maintenance: 'var(--maintenance)', Emergencies: 'var(--warn-bg)',
    };
    return map[cat] ?? 'var(--lav-2)';
  }
}
