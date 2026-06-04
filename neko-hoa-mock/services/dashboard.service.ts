import { Injectable } from '@angular/core';
import { MockDataService } from './mock-data.service';

@Injectable({ providedIn: 'root' })
export class DashboardService {
  constructor(private mock: MockDataService) {}
  getSummary() { return this.mock.getDashboardSummary(); }
}
