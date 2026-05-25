import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { PropertyService } from './property.service';
import { environment } from '../../../environments/environment';

const BASE = environment.apiBaseUrl;

describe('PropertyService', () => {
  let svc: PropertyService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HttpClientTestingModule] });
    svc  = TestBed.inject(PropertyService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('should be created', () => expect(svc).toBeTruthy());

  describe('getProperty()', () => {
    it('calls /property and returns a property', async () => {
      const promise = svc.getProperty();
      http.expectOne(`${BASE}/property`).flush({
        id: 'p1', accountNumber: 'SAKURA-001', communityId: 'SAKURA',
        communityName: 'Sakura Heights HOA', address: '1 Sakura Drive',
        city: 'San Jose', state: 'CA', zip: '95101',
        lot: 'A1', section: '1', fiscalYear: 2026, yearBuilt: 2005,
        status: 'active', monthlyAssessment: 250, annualAssessment: 3000,
        assessmentDueDay: 1, lateFeeAmount: 50, lateFeGraceDays: 15, financeChargeRate: 0.015,
      });
      const prop = await promise;
      expect(prop.status).toBe('active');
      expect(prop.accountNumber).toBe('SAKURA-001');
    });
  });

  describe('getOwner()', () => {
    it('calls /property/owner and returns owner with property context', async () => {
      const promise = svc.getOwner();
      http.expectOne(`${BASE}/property/owner`).flush({
        id: 'o1', firstName: 'Jane', lastName: 'Resident',
        email: 'resident@nekohoa.dev', phone: '408-555-0101',
        accountNumber: 'SAKURA-001', communityName: 'Sakura Heights HOA',
        propertyAddress: '1 Sakura Drive, San Jose, CA 95101',
        memberSince: '2005-06-01',
        votingRights: true, mailingToProperty: true,
        paperlessStatements: true, smsReminders: false,
      });
      const owner = await promise;
      expect(owner.firstName).toBe('Jane');
      expect(owner.accountNumber).toBe('SAKURA-001');
      expect(owner.communityName).toBe('Sakura Heights HOA');
    });
  });

  describe('getAddressHistory()', () => {
    it('calls /property/address-history and returns entries', async () => {
      const promise = svc.getAddressHistory();
      http.expectOne(`${BASE}/property/address-history`).flush([
        { id: 'h1', eventType: 'created', address: '1 Sakura Drive', effectiveDate: '2005-06-01' },
        { id: 'h2', eventType: 'change',  address: '1 Sakura Drive Apt 2', effectiveDate: '2022-03-15' },
      ]);
      const history = await promise;
      expect(history.length).toBe(2);
      expect(history[0].date).toBe('2005-06-01');
    });
  });

  describe('getDirectoryFields()', () => {
    it('calls /property/directory-fields and returns fields', async () => {
      const promise = svc.getDirectoryFields();
      http.expectOne(`${BASE}/property/directory-fields`).flush([
        { id: 'f1', fieldKey: 'name',    label: 'Full Name', shared: true  },
        { id: 'f2', fieldKey: 'email',   label: 'Email',     shared: false },
        { id: 'f3', fieldKey: 'phone',   label: 'Phone',     shared: false },
        { id: 'f4', fieldKey: 'address', label: 'Address',   shared: true  },
      ]);
      const fields = await promise;
      expect(fields.length).toBe(4);
      expect(fields.find(f => f.key === 'name')?.shared).toBeTrue();
      expect(fields.find(f => f.key === 'email')?.shared).toBeFalse();
    });
  });
});
