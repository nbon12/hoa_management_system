import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MyViolationsService } from '../../services/my-violations.service';
import { MyViolationItem, MyViolationsResponse } from '../../models/my-violations.model';

const PAGE_SIZE = 10;

@Component({
  selector: 'app-my-violations',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './my-violations.component.html',
  styleUrl: './my-violations.component.css',
})
export class MyViolationsComponent implements OnInit {
  private myViolationsService = inject(MyViolationsService);

  items: MyViolationItem[] = [];
  totalCount = 0;
  loading = true;
  error: string | null = null;
  offset = 0;
  limit = PAGE_SIZE;

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    this.error = null;
    this.myViolationsService.getMine(this.limit, this.offset).subscribe((result) => {
      this.loading = false;
      if ('error' in result) {
        this.error = result.error;
        return;
      }
      const res = result as MyViolationsResponse;
      this.items = res.items;
      this.totalCount = res.totalCount;
    });
  }

  get hasNext(): boolean {
    return this.offset + this.items.length < this.totalCount;
  }

  get hasPrev(): boolean {
    return this.offset > 0;
  }

  nextPage(): void {
    if (!this.hasNext) return;
    this.offset += this.limit;
    this.load();
  }

  prevPage(): void {
    if (!this.hasPrev) return;
    this.offset = Math.max(0, this.offset - this.limit);
    this.load();
  }

  formatDate(iso: string): string {
    try {
      return new Date(iso).toLocaleDateString();
    } catch {
      return iso;
    }
  }
}
