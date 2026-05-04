import { Injectable, Inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, of } from 'rxjs';
import { API_BASE_URL } from '../app.config';
import { DashboardSummary } from '../models/dashboard-summary.model';

@Injectable({ providedIn: 'root' })
export class DashboardService {
  constructor(
    private http: HttpClient,
    @Inject(API_BASE_URL) private apiBaseUrl: string
  ) {}

  getSummary(): Observable<DashboardSummary | { error: string }> {
    return this.http
      .get<DashboardSummary>(`${this.apiBaseUrl}/dashboard/summary`, {
        withCredentials: true,
      })
      .pipe(
        catchError((err) => {
          const message =
            err.error?.message ?? 'Failed to load violation count';
          return of({ error: message });
        })
      );
  }
}
