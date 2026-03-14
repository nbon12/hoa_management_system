import { Injectable, Inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, catchError, of } from 'rxjs';
import { API_BASE_URL } from '../app.config';
import { MyViolationsResponse } from '../models/my-violations.model';

@Injectable({ providedIn: 'root' })
export class MyViolationsService {
  constructor(
    private http: HttpClient,
    @Inject(API_BASE_URL) private apiBaseUrl: string
  ) {}

  getMine(limit = 10, offset = 0): Observable<MyViolationsResponse | { error: string }> {
    const params = new HttpParams()
      .set('limit', limit.toString())
      .set('offset', offset.toString());
    return this.http
      .get<MyViolationsResponse>(`${this.apiBaseUrl}/violations/mine`, {
        params,
        withCredentials: true,
      })
      .pipe(
        catchError(() => of({ error: 'Failed to load violations' }))
      );
  }
}
