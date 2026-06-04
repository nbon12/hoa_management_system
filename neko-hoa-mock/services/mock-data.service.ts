import { Injectable } from '@angular/core';
import {
  CurrentUser, Property, Owner, AddressHistory, DirectoryField,
  LedgerEntry, RecurringPayment, DraftEntry,
  Announcement, Poll, Violation, CalendarEvent, HOADocument,
  DashboardSummary
} from '../models';

@Injectable({ providedIn: 'root' })
export class MockDataService {

  // ── Auth ──────────────────────────────────────────────────────────────────
  readonly currentUser: CurrentUser = {
    id: 'usr-001',
    firstName: 'Nicholas',
    lastName: 'Bonilla',
    email: 'nicholas@example.com',
    initials: 'NB',
  };

  // ── Property ─────────────────────────────────────────────────────────────
  readonly property: Property = {
    accountNumber: 'R0670853L0541192',
    communityId: 'SAKURA',
    communityName: 'Sakura Heights',
    address: '714 Keystone Park Dr',
    city: 'Morrisville',
    state: 'NC',
    zip: '27560',
    lot: '151',
    phase: null,
    section: 'SF',
    block: null,
    fiscalYear: 2026,
    yearBuilt: 2014,
    status: 'active',
    monthlyAssessment: 35.00,
    annualAssessment: 420.00,
    assessmentDueDay: 1,
    lateFeeAmount: 20.00,
    lateFeeGraceDays: 30,
    financeChargeRate: 18.00,
  };

  readonly owner: Owner = {
    firstName: 'Nicholas',
    lastName: 'Bonilla',
    ownerName2: null,
    memberSince: null,
    accountNumber: 'R0670853L0541192',
    communityName: 'Sakura Heights',
    propertyAddress: '714 Keystone Park Dr',
    votingRights: true,
    email: 'nicholas@example.com',
    phone: null,
    mailingToProperty: true,
    paperlessStatements: true,
    smsReminders: false,
  };

  readonly addressHistory: AddressHistory[] = [
    { event: 'change',  address: '139 Henry\'s Watch Ln, Pittsboro NC 27312', date: '2023-07-03' },
    { event: 'change',  address: '714 Keystone Park Dr, Morrisville NC 27560', date: '2021-12-14' },
    { event: 'created', address: '714 Keystone Park Dr, Morrisville NC 27560', date: '2021-12-08' },
  ];

  readonly directoryFields: DirectoryField[] = [
    { key: 'name',       label: 'Name',             value: 'Nicholas Bonilla',          shared: false },
    { key: 'address',    label: 'Property address',  value: '714 Keystone Park Dr',      shared: true  },
    { key: 'email',      label: 'Email',             value: 'nicholas@example.com',      shared: false },
    { key: 'phone',      label: 'Phone',             value: '(919) ___-____',            shared: false },
    { key: 'moveInDate', label: 'Move-in date',      value: 'Dec 2021',                  shared: false },
  ];

  // ── Ledger ────────────────────────────────────────────────────────────────
  readonly ledger: LedgerEntry[] = [
    { id: 'e01', type: 'Regular Assessment', date: '2025-06-01', docNumber: 'RAS-2025M6-7716699-56',   description: 'Assessment for June 2025',      charge: 35, payment: null, balance: 35   },
    { id: 'e02', type: 'Payment',            date: '2025-06-02', docNumber: '91831926',                description: 'Payment received',              charge: null, payment: 35, balance: 0    },
    { id: 'e03', type: 'Regular Assessment', date: '2025-07-01', docNumber: 'RAS-2025M7-7827136-154',  description: 'Assessment for July 2025',      charge: 35, payment: null, balance: 35   },
    { id: 'e04', type: 'Payment',            date: '2025-07-02', docNumber: '92330089',                description: 'Payment received',              charge: null, payment: 35, balance: 0    },
    { id: 'e05', type: 'Regular Assessment', date: '2025-08-01', docNumber: 'RAS-2025M8-7934446-14',   description: 'Assessment for August 2025',    charge: 35, payment: null, balance: 35   },
    { id: 'e06', type: 'Payment',            date: '2025-08-04', docNumber: '92867139',                description: 'Payment received',              charge: null, payment: 35, balance: 0    },
    { id: 'e07', type: 'Regular Assessment', date: '2025-09-01', docNumber: 'RAS-2025M9-8037887-76',   description: 'Assessment for September 2025', charge: 35, payment: null, balance: 35   },
    { id: 'e08', type: 'Payment',            date: '2025-09-02', docNumber: '93292684',                description: 'Payment received',              charge: null, payment: 35, balance: 0    },
    { id: 'e09', type: 'Regular Assessment', date: '2025-10-01', docNumber: 'RAS-2025M10-8166899-300', description: 'Assessment for October 2025',   charge: 35, payment: null, balance: 35   },
    { id: 'e10', type: 'Payment',            date: '2025-10-02', docNumber: '93787455',                description: 'Payment received',              charge: null, payment: 35, balance: 0    },
    { id: 'e11', type: 'Regular Assessment', date: '2025-11-01', docNumber: 'RAS-2025M11-8273129-327', description: 'Assessment for November 2025',  charge: 35, payment: null, balance: 35   },
    { id: 'e12', type: 'Payment',            date: '2025-11-04', docNumber: '94396006',                description: 'Payment received',              charge: null, payment: 35, balance: 0    },
    { id: 'e13', type: 'Regular Assessment', date: '2026-05-01', docNumber: 'RAS-2026M5-9017522-121',  description: 'Assessment for May 2026',       charge: 35, payment: null, balance: 35   },
    { id: 'e14', type: 'Regular Assessment', date: '2026-06-01', docNumber: 'RAS-2026M6-9146058-248',  description: 'Assessment for June 2026',      charge: 35, payment: null, balance: 70   },
  ];

  get currentBalance(): number {
    const last = this.ledger[this.ledger.length - 1];
    return last ? last.balance : 0;
  }

  // ── Recurring Payment ─────────────────────────────────────────────────────
  readonly recurringPayment: RecurringPayment = {
    status: 'active',
    amountType: 'assessment',
    fixedAmount: null,
    method: 'ach',
    draftDay: 2,
    bankName: 'Fidelity Investments',
    routingLast4: '681',
    accountLast4: '747',
    accountType: 'checking',
    cardholderName: null,
    cardLast4: null,
    cardExpiry: null,
    cardZip: null,
    processingFee: 1.95,
  };

  readonly drafts: DraftEntry[] = [
    { date: '2026-05-02', source: 'Fidelity ••747', amount: 36.95, status: 'paid' },
    { date: '2026-06-02', source: 'Fidelity ••747', amount: 36.95, status: 'scheduled' },
    { date: '2026-07-02', source: 'Fidelity ••747', amount: 36.95, status: 'scheduled' },
    { date: '2026-08-02', source: 'Fidelity ••747', amount: 36.95, status: 'scheduled' },
  ];

  // ── Violations ────────────────────────────────────────────────────────────
  readonly violations: Violation[] = [
    { id: 'v01', issue: 'Trash bins out past pickup', date: '2026-04-12', category: 'Maintenance', status: 'closed' },
    { id: 'v02', issue: 'Lawn height exceeded 6"',    date: '2026-03-02', category: 'Landscape',   status: 'closed' },
    { id: 'v03', issue: 'Holiday lights past Jan 15', date: '2026-01-22', category: 'Architectural', status: 'closed' },
    { id: 'v04', issue: 'Vehicle parked on grass',    date: '2025-11-04', category: 'Parking',     status: 'closed' },
  ];

  // ── Announcements ─────────────────────────────────────────────────────────
  readonly announcements: Announcement[] = [
    {
      id: 'a01', title: 'Community pool opening!',
      body: 'The pool reopens Saturday May 24th at 10am. New seasonal hours posted in Documents.',
      date: '2026-05-04', category: 'Events', pinned: true,
      commentCount: 12, likeCount: 38,
      imageUrl: null, authorInitials: 'BM', authorLabel: 'Board · Mary R.',
    },
    {
      id: 'a02', title: 'Landscape walkthrough this Friday',
      body: 'Maintenance crew will be on common areas. Please move trash bins after pickup.',
      date: '2026-04-28', category: 'Maintenance', pinned: false,
      commentCount: 4, likeCount: 9,
      imageUrl: null, authorInitials: 'BM', authorLabel: 'Board · Mary R.',
    },
    {
      id: 'a03', title: 'Reminder · ACC submission deadline',
      body: 'Architectural change requests for May review are due April 30.',
      date: '2026-04-12', category: 'Board', pinned: false,
      commentCount: 2, likeCount: 5,
      imageUrl: null, authorInitials: 'BT', authorLabel: 'Board · Treasurer',
    },
    {
      id: 'a04', title: 'Annual meeting recap',
      body: 'Board reviewed FY25 financials and elected new treasurer. Minutes in Documents.',
      date: '2026-03-18', category: 'Board', pinned: false,
      commentCount: 7, likeCount: 14,
      imageUrl: null, authorInitials: 'BM', authorLabel: 'Board · Mary R.',
    },
  ];

  readonly poll: Poll = {
    question: 'Should we extend pool hours to 9pm?',
    options: [
      { label: 'Yes, all summer', percent: 62 },
      { label: 'Weekends only',   percent: 24 },
      { label: 'Keep current',    percent: 14 },
    ],
    totalVotes: 132,
    closesLabel: 'closes Friday',
  };

  // ── Calendar ──────────────────────────────────────────────────────────────
  readonly calendarEvents: CalendarEvent[] = [
    { id: 'c01', title: 'Board meeting',           date: '2026-05-20', location: 'Clubhouse · 7pm',     category: 'Board',    rsvpEnabled: false },
    { id: 'c02', title: '🏊 Pool opens for season', date: '2026-05-24', location: 'Sakura pool · 10am',  category: 'Amenity',  rsvpEnabled: true  },
    { id: 'c03', title: 'Community potluck',        date: '2026-06-14', location: 'Park pavilion · 5pm', category: 'Social',   rsvpEnabled: true  },
    { id: 'c04', title: 'Board meeting',            date: '2026-06-17', location: 'Clubhouse · 7pm',     category: 'Board',    rsvpEnabled: false },
    { id: 'c05', title: 'July 4th party',           date: '2026-07-04', location: 'Park · all day',      category: 'Social',   rsvpEnabled: true  },
    { id: 'c06', title: 'Board meeting',            date: '2026-07-15', location: 'Clubhouse · 7pm',     category: 'Board',    rsvpEnabled: false },
    { id: 'c07', title: 'Board meeting',            date: '2026-08-19', location: 'Clubhouse · 7pm',     category: 'Board',    rsvpEnabled: false },
    { id: 'c08', title: 'Pool closes for season',   date: '2026-09-08', location: 'Sakura pool',         category: 'Amenity',  rsvpEnabled: false },
    { id: 'c09', title: 'Board meeting',            date: '2026-09-16', location: 'Clubhouse · 7pm',     category: 'Board',    rsvpEnabled: false },
    { id: 'c10', title: 'Annual board meeting',     date: '2026-10-21', location: 'Clubhouse · 7pm',     category: 'Board',    rsvpEnabled: true  },
  ];

  // ── Documents ─────────────────────────────────────────────────────────────
  readonly documents: HOADocument[] = [
    { id: 'd01', name: 'ACC Form RM-Update.pdf',            category: 'Forms',      effectiveDate: '2026-04-01', fileSizeLabel: '142 KB', pinned: false, url: null },
    { id: 'd02', name: 'Pool rules & hours.pdf',            category: 'Rules',      effectiveDate: '2026-04-15', fileSizeLabel: '95 KB',  pinned: true,  url: null },
    { id: 'd03', name: 'Annual meeting minutes.pdf',         category: 'Minutes',    effectiveDate: '2026-03-12', fileSizeLabel: '210 KB', pinned: true,  url: null },
    { id: 'd04', name: 'Sakura 2024-2025 Insurance.pdf',    category: 'Insurance',  effectiveDate: '2024-07-07', fileSizeLabel: '1.2 MB', pinned: false, url: null },
    { id: 'd05', name: 'Sakura Crossing Budget.pdf',        category: 'Budgets',    effectiveDate: '2024-01-01', fileSizeLabel: '380 KB', pinned: false, url: null },
    { id: 'd06', name: 'Maintenance request form.pdf',      category: 'Forms',      effectiveDate: '2026-01-08', fileSizeLabel: '88 KB',  pinned: false, url: null },
    { id: 'd07', name: '2026 Budget.pdf',                   category: 'Financials', effectiveDate: '2026-01-01', fileSizeLabel: '450 KB', pinned: false, url: null },
    { id: 'd08', name: 'Reserve study summary.pdf',         category: 'Financials', effectiveDate: '2025-11-01', fileSizeLabel: '710 KB', pinned: false, url: null },
    { id: 'd09', name: 'CC&R restated.pdf',                 category: 'Governing',  effectiveDate: '2022-06-01', fileSizeLabel: '2.8 MB', pinned: false, url: null },
    { id: 'd10', name: 'Bylaws.pdf',                        category: 'Governing',  effectiveDate: '2022-06-01', fileSizeLabel: '1.1 MB', pinned: false, url: null },
    { id: 'd11', name: 'Architectural guidelines.pdf',      category: 'Rules',      effectiveDate: '2025-02-01', fileSizeLabel: '420 KB', pinned: false, url: null },
    { id: 'd12', name: 'Audit · FY24.pdf',                  category: 'Financials', effectiveDate: '2025-08-14', fileSizeLabel: '640 KB', pinned: false, url: null },
  ];

  // ── Dashboard ─────────────────────────────────────────────────────────────
  getDashboardSummary(): DashboardSummary {
    return {
      currentBalance: this.currentBalance,
      balanceDueDate: '2026-06-01',
      openViolations: this.violations.filter(v => v.status === 'open').length,
      nextEvent: { title: 'Pool opens', date: '2026-05-24' },
      documentCount: this.documents.length,
      newDocumentsThisMonth: 3,
      pinnedAnnouncement: this.announcements.find(a => a.pinned) ?? null,
      thisWeekEvents: [
        { id: 'tw1', title: 'Board meeting',  date: '2026-05-20', location: 'Clubhouse · 7pm',    category: 'Board',   rsvpEnabled: false },
        { id: 'tw2', title: 'Landscape walkthrough', date: '2026-05-23', location: 'Common areas', category: 'Maintenance', rsvpEnabled: false },
        { id: 'tw3', title: 'Pool opens',     date: '2026-05-24', location: 'Sakura pool · 10am', category: 'Amenity', rsvpEnabled: true  },
      ],
      recentActivity: this.ledger.slice(-4).reverse(),
      communityExpenses: [
        { label: 'Pool ops',   color: 'var(--rose)',        amount: 57640 },
        { label: 'Landscape',  color: 'var(--violet)',      amount: 52684 },
        { label: 'Mgmt fee',   color: 'var(--lav-2)',       amount: 12758 },
        { label: 'Insurance',  color: 'var(--pink-2)',      amount: 10803 },
        { label: 'Repairs',    color: 'var(--social)',      amount: 21640 },
        { label: 'Other',      color: 'var(--maintenance)', amount: 25506 },
      ],
    };
  }
}
