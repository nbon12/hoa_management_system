import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

// 025: thin data layer for the board-side endpoints (contracts/board-access.md).
// Authorization is server-side; these calls carry no client-derived scope.

export interface Paged<T> {
  items: T[];
  total: number;
  limit: number;
  offset: number;
}

export interface MyCommunity {
  id: string;
  communityName: string;
  role: string;
  status: string;
}

export interface Membership {
  id: string;
  userId: string;
  userDisplayName: string;
  role: string;
  status: string;
  startDate: string;
  endDate: string | null;
}

export interface CreateMembershipRequest {
  userId: string;
  role: string;
  startDate: string;
  endDate?: string | null;
}

export interface UpdateMembershipRequest {
  role?: string;
  status?: string;
  endDate?: string | null;
}

/** Registry-driven metric row (FR-029). Mirrors the backend `MetricDescriptor` projection. */
export interface MetricDescriptor {
  id: string;
  label: string;
  definitionText: string;
  value: string | number | null;
  detail?: string | null;
  status: string;      // e.g. 'ok' | 'warn' | 'Unavailable'
  emphasis: string;    // e.g. 'link' | 'warn' | 'ok' | 'none'
}

@Injectable({ providedIn: 'root' })
export class BoardService {
  private http = inject(HttpClient);
  private readonly base = environment.apiBaseUrl;

  async getMyCommunities(limit = 25, offset = 0): Promise<Paged<MyCommunity>> {
    const params = new HttpParams().set('limit', String(limit)).set('offset', String(offset));
    return firstValueFrom(
      this.http.get<Paged<MyCommunity>>(`${this.base}/me/communities`, { params }));
  }

  async getMemberships(communityId: string, limit = 25, offset = 0): Promise<Paged<Membership>> {
    const params = new HttpParams().set('limit', String(limit)).set('offset', String(offset));
    return firstValueFrom(
      this.http.get<Paged<Membership>>(`${this.base}/communities/${communityId}/memberships`, { params }));
  }

  async createMembership(communityId: string, body: CreateMembershipRequest): Promise<Membership> {
    return firstValueFrom(
      this.http.post<Membership>(`${this.base}/communities/${communityId}/memberships`, body));
  }

  async updateMembership(communityId: string, membershipId: string, body: UpdateMembershipRequest): Promise<Membership> {
    return firstValueFrom(
      this.http.patch<Membership>(`${this.base}/communities/${communityId}/memberships/${membershipId}`, body));
  }

  async getMetrics(communityId: string, surface: string, limit = 25, offset = 0): Promise<Paged<MetricDescriptor>> {
    const params = new HttpParams()
      .set('communityId', communityId)
      .set('surface', surface)
      .set('limit', String(limit))
      .set('offset', String(offset));
    return firstValueFrom(
      this.http.get<Paged<MetricDescriptor>>(`${this.base}/board/metrics`, { params }));
  }
}
