export interface MyViolationItem {
  id: string;
  description: string;
  occurrenceDate: string;
  violationTypeName: string;
  propertyDisplayName: string;
}

export interface MyViolationsResponse {
  items: MyViolationItem[];
  totalCount: number;
}
