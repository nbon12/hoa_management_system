import { Injectable, signal } from '@angular/core';
import { MockDataService } from './mock-data.service';
import { Owner, DirectoryField } from '../models';

@Injectable({ providedIn: 'root' })
export class PropertyService {
  constructor(private mock: MockDataService) {}

  getProperty() { return this.mock.property; }
  getOwner()    { return this.mock.owner; }
  getAddressHistory() { return this.mock.addressHistory; }
  getDirectoryFields() { return [...this.mock.directoryFields]; }

  updateOwner(partial: Partial<Owner>): Promise<Owner> {
    return new Promise(resolve =>
      setTimeout(() => resolve({ ...this.mock.owner, ...partial }), 500)
    );
  }

  toggleDirectoryField(key: string, fields: DirectoryField[]): DirectoryField[] {
    return fields.map(f => f.key === key ? { ...f, shared: !f.shared } : f);
  }
}
