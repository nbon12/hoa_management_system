import { Injectable } from '@angular/core';
import { HttpClient, HttpParams, HttpResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

/**
 * The single owner of the API base address and request plumbing (015 US6, FR-020 — previously
 * every service repeated `private base = environment.apiBaseUrl` and hand-built URLs). Services
 * call with contract-relative paths (`/payments/ledger`); error normalization is handled once by
 * the error interceptor (`ApiError`), not per call site.
 */
@Injectable({ providedIn: 'root' })
export class ApiClient {
  private readonly base = environment.apiBaseUrl;

  constructor(private http: HttpClient) {}

  get<T>(path: string, params?: HttpParams): Promise<T> {
    return firstValueFrom(this.http.get<T>(this.url(path), { params }));
  }

  /** GET with the full response (status + body) — for endpoints using 204 as "none". */
  getResponse<T>(path: string): Promise<HttpResponse<T>> {
    return firstValueFrom(this.http.get<T>(this.url(path), { observe: 'response' }));
  }

  post<T>(path: string, body: unknown): Promise<T> {
    return firstValueFrom(this.http.post<T>(this.url(path), body));
  }

  put<T>(path: string, body: unknown): Promise<T> {
    return firstValueFrom(this.http.put<T>(this.url(path), body));
  }

  patch<T>(path: string, body: unknown): Promise<T> {
    return firstValueFrom(this.http.patch<T>(this.url(path), body));
  }

  async delete(path: string): Promise<void> {
    await firstValueFrom(this.http.delete(this.url(path)));
  }

  private url(path: string): string {
    return `${this.base}${path}`;
  }
}
