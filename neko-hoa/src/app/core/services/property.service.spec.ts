import { TestBed } from '@angular/core/testing';
import { PropertyService } from './property.service';
import { MockDataService } from './mock-data.service';

describe('PropertyService', () => {
  let svc: PropertyService;
  let mock: MockDataService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    svc  = TestBed.inject(PropertyService);
    mock = TestBed.inject(MockDataService);
  });

  it('should be created', () => expect(svc).toBeTruthy());

  describe('getProperty()', () => {
    it('returns the mock property', () => {
      expect(svc.getProperty()).toBe(mock.property);
    });
    it('property status is active', () => {
      expect(svc.getProperty().status).toBe('active');
    });
  });

  describe('getOwner()', () => {
    it('returns the mock owner', () => {
      expect(svc.getOwner()).toBe(mock.owner);
    });
    it('owner has a firstName', () => {
      expect(svc.getOwner().firstName.length).toBeGreaterThan(0);
    });
  });

  describe('getAddressHistory()', () => {
    it('returns address history entries', () => {
      expect(svc.getAddressHistory().length).toBeGreaterThan(0);
    });
    it('first entry is most recent (descending order)', () => {
      const history = svc.getAddressHistory();
      if (history.length > 1) {
        expect(history[0].date >= history[1].date).toBeTrue();
      }
    });
  });

  describe('getDirectoryFields()', () => {
    it('returns an array of fields', () => {
      expect(svc.getDirectoryFields().length).toBeGreaterThan(0);
    });
    it('returns a copy, not the original', () => {
      const a = svc.getDirectoryFields();
      const b = svc.getDirectoryFields();
      expect(a).not.toBe(b);
    });
  });

  describe('updateOwner()', () => {
    it('resolves with merged owner data', async () => {
      const result = await svc.updateOwner({ firstName: 'Maria' });
      expect(result.firstName).toBe('Maria');
    });
    it('preserves unchanged fields', async () => {
      const original = svc.getOwner();
      const result = await svc.updateOwner({ firstName: 'Maria' });
      expect(result.lastName).toBe(original.lastName);
    });
  });

  describe('toggleDirectoryField()', () => {
    it('flips the shared flag for the targeted key', () => {
      const fields = svc.getDirectoryFields();
      const key = fields[0].key;
      const original = fields[0].shared;
      const updated = svc.toggleDirectoryField(key, fields);
      expect(updated.find(f => f.key === key)!.shared).toBe(!original);
    });
    it('does not mutate the original array', () => {
      const fields = svc.getDirectoryFields();
      const key = fields[0].key;
      const originalShared = fields[0].shared;
      svc.toggleDirectoryField(key, fields);
      expect(fields[0].shared).toBe(originalShared);
    });
    it('leaves other fields unchanged', () => {
      const fields = svc.getDirectoryFields();
      const updated = svc.toggleDirectoryField(fields[0].key, fields);
      for (let i = 1; i < fields.length; i++) {
        expect(updated[i].shared).toBe(fields[i].shared);
      }
    });
  });
});
