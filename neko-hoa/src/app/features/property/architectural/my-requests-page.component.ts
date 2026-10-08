import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ResidentArchitecturalService } from '../../../core/services/resident-architectural.service';
import { ResidentArcListItem } from '../../../core/models';
import { errorMessage, projectTypeLabel, statusLabel, statusTone } from './resident-arc-format';

/** 029 US2: "My architectural requests" — drafts and submitted requests on the active property. */
@Component({
  selector: 'app-my-arc-requests',
  standalone: true,
  imports: [RouterLink, DatePipe],
  styles: [`
    :host { display: block; }
    .requests { list-style: none; margin: 0; padding: 0; display: grid; gap: 10px; }
    .row { display: grid; grid-template-columns: minmax(0, 2fr) auto minmax(0, 1fr) auto; gap: 12px; align-items: center; text-decoration: none; color: inherit; }
    .row .title { font-weight: 600; overflow-wrap: anywhere; }
    .dates { font-size: 13px; }
    .pager { display: flex; gap: 10px; justify-content: flex-end; margin-top: 14px; }
    @media (max-width: 600px) {
      .row { grid-template-columns: 1fr; gap: 6px; }
      .page-header { flex-wrap: wrap; }
    }
  `],
  template: `
    <div class="page-header">
      <h1 class="page-title">My architectural requests</h1>
      <div class="page-header__actions">
        <a class="btn btn--primary" routerLink="/app/property/architectural/new">New request</a>
      </div>
    </div>
    <p class="muted">Ask the board before changing the outside of your home — fences, solar, paint, sheds and more.</p>

    @if (loading()) {
      <p class="muted"><span class="spinner"></span> Loading…</p>
    } @else if (error()) {
      <div class="alert alert--error" role="alert"><span>⚠</span> {{ error() }}</div>
    } @else if (items().length === 0) {
      <div class="card"><p>You haven't filed any architectural requests yet.</p></div>
    } @else {
      <ul class="requests" aria-label="Architectural requests">
        @for (item of items(); track item.id) {
          <li>
            <a class="card row" [routerLink]="link(item)">
              <div>
                <div class="title">{{ item.projectTitle || 'Untitled draft' }}</div>
                <div class="muted">
                  {{ item.displayId ?? 'Draft' }}@if (item.revision > 1) { <span> · v{{ item.revision }}</span> } · {{ type(item) }}
                </div>
              </div>
              <span class="pill {{ tone(item) }}">{{ label(item) }}</span>
              <div class="dates muted">
                @if (item.receivedDate) {
                  <div>Received {{ item.receivedDate | date:'MM/dd/yy' }}</div>
                  <div>Decision due {{ item.dueDate | date:'MM/dd/yy' }}</div>
                } @else {
                  <div>Not submitted yet</div>
                }
              </div>
              <span class="muted">📎 {{ item.attachmentCount }}</span>
            </a>
          </li>
        }
      </ul>
      @if (total() > limit) {
        <div class="pager">
          <button class="btn btn--ghost" (click)="page(-1)" [disabled]="offset() === 0">Previous</button>
          <button class="btn btn--ghost" (click)="page(1)" [disabled]="offset() + limit >= total()">Next</button>
        </div>
      }
    }
  `,
})
export class MyRequestsPageComponent implements OnInit {
  private api = inject(ResidentArchitecturalService);
  readonly limit = 25;

  items = signal<ResidentArcListItem[]>([]);
  total = signal(0);
  offset = signal(0);
  loading = signal(true);
  error = signal<string | null>(null);

  ngOnInit(): Promise<void> { return this.load(); }

  link(item: ResidentArcListItem): string[] {
    return item.kind === 'Draft'
      ? ['/app/property/architectural/drafts', item.id]
      : ['/app/property/architectural', item.id];
  }

  label(item: ResidentArcListItem): string { return statusLabel(item.status); }
  tone(item: ResidentArcListItem): string { return statusTone(item.status); }
  type(item: ResidentArcListItem): string { return projectTypeLabel(item.projectType); }

  async page(direction: number): Promise<void> {
    this.offset.set(Math.max(0, this.offset() + direction * this.limit));
    await this.load();
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      const res = await this.api.list(this.limit, this.offset());
      this.items.set(res.items);
      this.total.set(res.total);
      this.error.set(null);
    } catch (err) {
      this.error.set(errorMessage(err, 'Your requests could not be loaded.'));
    } finally {
      this.loading.set(false);
    }
  }
}
