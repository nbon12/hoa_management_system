import { Component, OnInit, inject } from '@angular/core';
import { Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { DashboardService } from '../../services/dashboard.service';
import { DashboardSummary } from '../../models/dashboard-summary.model';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.css',
})
export class DashboardComponent implements OnInit {
  private dashboardService = inject(DashboardService);
  private router = inject(Router);

  summary: DashboardSummary | null = null;
  violationError: string | null = null;
  loading = true;

  ngOnInit(): void {
    this.dashboardService.getSummary().subscribe((result) => {
      this.loading = false;
      if ('error' in result) {
        this.violationError = result.error;
        return;
      }
      this.summary = result;
    });
  }

  goToMyViolations(): void {
    if (this.loading || this.violationError !== null) return;
    this.router.navigate(['/my-violations']);
  }
}
