import { Component, inject, signal, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { CommunityService } from '../../../core/services/community.service';
import { DocumentCategory } from '../../../core/models';

const ALL_CATEGORIES: (DocumentCategory | 'All' | 'Pinned')[] = [
  'All', 'Pinned', 'Forms', 'Insurance', 'Budgets', 'Rules', 'Minutes', 'Governing', 'Financials'
];

@Component({
  selector: 'app-documents',
  standalone: true,
  imports: [FormsModule, DatePipe],
  template: `
    <div class="page-header">
      <h1 class="page-title">Documents <span class="hand">library</span></h1>
      <div class="page-header__actions">
        <input class="field field--dashed" style="width:200px;" placeholder="🔍 Search documents…"
               [(ngModel)]="searchTerm" (ngModelChange)="onSearch()">
        <button class="btn btn--ghost">+ Filter</button>
      </div>
    </div>

    <!-- Category chips -->
    <div class="tab-bar" style="flex-wrap:wrap;gap:6px;">
      @for (cat of categories; track cat) {
        <button class="tab" style="font-size:11px;"
                [class.tab--active]="activeCategory() === cat"
                (click)="activeCategory.set(cat)">
          {{ cat }} @if (cat !== 'All') { · {{ countFor(cat) }} }
          @else { · {{ totalCount }} }
        </button>
      }
    </div>

    <!-- Document table -->
    <div class="card" style="padding:0;overflow:hidden;">
      <table class="data-table">
        <thead>
          <tr>
            <th>Name</th><th>Type</th><th>Effective</th><th class="num">Size</th><th></th>
          </tr>
        </thead>
        <tbody>
          @for (doc of visibleDocs(); track doc.id) {
            <tr>
              <td style="display:flex;align-items:center;gap:10px;">
                <div class="ph" style="width:24px;height:30px;border-radius:4px;font-size:8px;flex-shrink:0;">PDF</div>
                <span class="link">{{ doc.name }}</span>
                @if (doc.pinned) { <span class="pill" style="font-size:10px;">📌</span> }
              </td>
              <td>
                <span class="pill" style="background:var(--lav-2);">{{ doc.category }}</span>
              </td>
              <td>{{ doc.effectiveDate | date:'MM/dd/yy' }}</td>
              <td class="num">{{ doc.fileSizeLabel }}</td>
              <td>
                <button class="btn btn--ghost" style="padding:4px 10px;font-size:11px;">⬇</button>
              </td>
            </tr>
          }
          @empty {
            <tr>
              <td colspan="5" style="text-align:center;padding:32px;color:var(--ink-mute);">
                No documents found.
              </td>
            </tr>
          }
        </tbody>
      </table>
    </div>

    <!-- Pinned / grouped cards (shown when in 'Pinned' category) -->
    @if (activeCategory() === 'Pinned') {
      <div class="grid-2">
        @for (group of pinnedGroups; track group.title) {
          <div class="card">
            <div style="display:flex;align-items:baseline;">
              <div class="section-title" style="margin:0;">{{ group.title }}</div>
              <span class="link" style="margin-left:auto;font-size:11px;">see all →</span>
            </div>
            <div style="display:flex;flex-direction:column;gap:8px;margin-top:10px;">
              @for (item of group.items; track item.id) {
                <div style="display:flex;gap:10px;padding:8px 10px;border:1.5px dashed var(--line);border-radius:10px;align-items:center;">
                  <div class="ph" style="width:28px;height:34px;border-radius:4px;font-size:8px;flex-shrink:0;">PDF</div>
                  <div style="flex:1;">
                    <div style="font-size:12px;font-weight:500;">{{ item.name }}</div>
                    <div class="muted" style="font-size:11px;">{{ item.effectiveDate | date:'MMM d' }}</div>
                  </div>
                  <button class="link" style="font-size:11px;background:none;border:none;cursor:pointer;">⬇</button>
                </div>
              }
            </div>
          </div>
        }
      </div>
    }
  `
})
export class DocumentsComponent {
  private svc = inject(CommunityService);

  categories = ALL_CATEGORIES;
  activeCategory = signal<string>('All');
  private _searchTerm = signal('');
  get searchTerm()          { return this._searchTerm(); }
  set searchTerm(v: string) { this._searchTerm.set(v); }

  get totalCount() { return this.svc.getDocuments().length; }
  countFor(cat: string) { return this.svc.getDocuments(cat).length; }

  visibleDocs = computed(() => {
    const cat  = this.activeCategory();
    const term = this._searchTerm();
    if (term) return this.svc.searchDocuments(term);
    return this.svc.getDocuments(cat);
  });

  onSearch() {
    if (this._searchTerm()) this.activeCategory.set('All');
  }

  get pinnedGroups() {
    const all = this.svc.getDocuments();
    return [
      { title: '⭐ Pinned', items: all.filter(d => d.pinned).slice(0, 3) },
      { title: '📋 Forms',  items: all.filter(d => d.category === 'Forms').slice(0, 2) },
    ].filter(g => g.items.length > 0);
  }
}
