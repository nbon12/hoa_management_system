// ─── Shared primitives ───────────────────────────────────────────────────────
export type ISODate = string; // "2026-05-01"

// ─── Auth ────────────────────────────────────────────────────────────────────
export interface CurrentUser {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  initials: string;
}

// ─── Property ────────────────────────────────────────────────────────────────
export interface Property {
  accountNumber: string;
  communityId: string;
  communityName: string;
  address: string;
  city: string;
  state: string;
  zip: string;
  lot: string;
  phase: string | null;
  section: string;
  block: string | null;
  fiscalYear: number;
  yearBuilt: number;
  status: 'active' | 'inactive';
  monthlyAssessment: number;
  annualAssessment: number;
  assessmentDueDay: number;
  lateFeeAmount: number;
  lateFeeGraceDays: number;
  financeChargeRate: number; // percent per annum
}

export interface Owner {
  firstName: string;
  lastName: string;
  ownerName2: string | null;
  memberSince: string | null;
  accountNumber: string;
  communityName: string;
  propertyAddress: string;
  votingRights: boolean;
  email: string;
  phone: string | null;
  mailingToProperty: boolean;
  paperlessStatements: boolean;
  smsReminders: boolean;
}

export interface AddressHistory {
  event: 'created' | 'change';
  address: string;
  date: ISODate;
}

export interface DirectoryField {
  key: string;
  label: string;
  value: string;
  shared: boolean;
}

// ─── Payments / Ledger ───────────────────────────────────────────────────────
export type LedgerEntryType = 'Regular Assessment' | 'Payment' | 'Late Fee' | 'Finance Charge';

export interface LedgerEntry {
  id: string;
  type: LedgerEntryType;
  date: ISODate;
  docNumber: string;
  description: string;
  charge: number | null;
  payment: number | null;
  balance: number;
}

export interface RecurringPayment {
  status: 'active' | 'inactive';
  amountType: 'assessment' | 'balance' | 'fixed';
  fixedAmount: number | null;
  method: 'ach' | 'card';
  draftDay: number;
  // ACH fields
  bankName: string | null;
  routingLast4: string | null;
  accountLast4: string | null;
  accountType: 'checking' | 'savings' | null;
  // Card fields
  cardholderName: string | null;
  cardLast4: string | null;
  cardExpiry: string | null;
  cardZip: string | null;
  processingFee: number;
}

export interface DraftEntry {
  date: ISODate;
  source: string;
  amount: number;
  status: 'paid' | 'scheduled' | 'failed';
}

// ─── Community ───────────────────────────────────────────────────────────────
export type AnnouncementCategory = 'Board' | 'Maintenance' | 'Events' | 'Emergencies';

export interface Announcement {
  id: string;
  title: string;
  body: string;
  date: ISODate;
  category: AnnouncementCategory;
  pinned: boolean;
  commentCount: number;
  likeCount: number;
  imageUrl: string | null;
  authorInitials: string;
  authorLabel: string;
}

export interface Poll {
  question: string;
  options: { label: string; percent: number }[];
  totalVotes: number;
  closesLabel: string;
}

export type ViolationStatus = 'open' | 'closed';
export type ViolationCategory = 'Maintenance' | 'Landscape' | 'Architectural' | 'Parking' | 'Noise' | 'Other';

export interface Violation {
  id: string;
  issue: string;
  date: ISODate;
  category: ViolationCategory;
  status: ViolationStatus;
}

export type EventCategory = 'Board' | 'Amenity' | 'Social' | 'Maintenance';

export interface CalendarEvent {
  id: string;
  title: string;
  date: ISODate; // "2026-05-20"
  location: string;
  category: EventCategory;
  rsvpEnabled: boolean;
}

export type DocumentCategory = 'Forms' | 'Insurance' | 'Budgets' | 'Rules' | 'Minutes' | 'Governing' | 'Financials' | 'Pinned';

export interface HOADocument {
  id: string;
  name: string;
  category: DocumentCategory;
  effectiveDate: ISODate;
  fileSizeLabel: string;
  pinned: boolean;
  url: string | null;
}

// ─── Dashboard summary ───────────────────────────────────────────────────────
export interface DashboardSummary {
  currentBalance: number;
  balanceDueDate: ISODate;
  openViolations: number;
  nextEvent: { title: string; date: ISODate } | null;
  documentCount: number;
  newDocumentsThisMonth: number;
  pinnedAnnouncement: Announcement | null;
  thisWeekEvents: CalendarEvent[];
  recentActivity: LedgerEntry[];
  communityExpenses: { label: string; color: string; amount: number }[];
}
