import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Property, Owner, AddressHistory, DirectoryField } from '../models';

interface ApiProperty {
  accountNumber: string; communityId: string; communityName: string;
  address: string; city: string; state: string; zip: string;
  lot: string; phase: string | null; section: string; block: string | null;
  fiscalYear: number; yearBuilt: number; status: string;
  monthlyAssessment: number; annualAssessment: number; assessmentDueDay: number;
  lateFeeAmount: number; lateFeeGraceDays: number; financeChargeRate: number;
}

interface ApiOwner {
  firstName: string; lastName: string; email: string; phone: string | null;
  accountNumber: string; communityName: string; propertyAddress: string;
  votingRights: boolean; mailingToProperty: boolean;
  paperlessStatements: boolean; smsReminders: boolean;
  ownerName2: string | null; memberSince: string | null;
}

interface ApiAddressHistory {
  eventType: string; address: string; effectiveDate: string;
}

interface ApiDirectoryField {
  fieldKey: string; label: string; shared: boolean;
}

@Injectable({ providedIn: 'root' })
export class PropertyService {
  private readonly base = environment.apiBaseUrl;

  constructor(private http: HttpClient) {}

  async getProperty(): Promise<Property> {
    const p = await firstValueFrom(this.http.get<ApiProperty>(`${this.base}/property`));
    return {
      accountNumber:      p.accountNumber,
      communityId:        p.communityId,
      communityName:      p.communityName,
      address:            p.address,
      city:               p.city,
      state:              p.state,
      zip:                p.zip,
      lot:                p.lot,
      phase:              p.phase,
      section:            p.section,
      block:              p.block,
      fiscalYear:         p.fiscalYear,
      yearBuilt:          p.yearBuilt,
      status:             p.status as any,
      monthlyAssessment:  p.monthlyAssessment,
      annualAssessment:   p.annualAssessment,
      assessmentDueDay:   p.assessmentDueDay,
      lateFeeAmount:      p.lateFeeAmount,
      lateFeeGraceDays:   p.lateFeeGraceDays,
      financeChargeRate:  p.financeChargeRate,
    };
  }

  async getOwner(): Promise<Owner> {
    const o = await firstValueFrom(this.http.get<ApiOwner>(`${this.base}/property/owner`));
    return {
      firstName:           o.firstName,
      lastName:            o.lastName,
      ownerName2:          o.ownerName2 ?? null,
      memberSince:         o.memberSince,
      accountNumber:       o.accountNumber,
      communityName:       o.communityName,
      propertyAddress:     o.propertyAddress,
      votingRights:        o.votingRights,
      email:               o.email,
      phone:               o.phone,
      mailingToProperty:   o.mailingToProperty,
      paperlessStatements: o.paperlessStatements,
      smsReminders:        o.smsReminders,
    };
  }

  async getAddressHistory(): Promise<AddressHistory[]> {
    const items = await firstValueFrom(
      this.http.get<ApiAddressHistory[]>(`${this.base}/property/address-history`)
    );
    return items.map(h => ({
      event:   h.eventType as any,
      address: h.address,
      date:    h.effectiveDate,
    }));
  }

  async getDirectoryFields(): Promise<DirectoryField[]> {
    const items = await firstValueFrom(
      this.http.get<ApiDirectoryField[]>(`${this.base}/property/directory-fields`)
    );
    return items.map(f => ({
      key:    f.fieldKey,
      label:  f.label,
      value:  '',
      shared: f.shared,
    }));
  }

  async updateOwner(partial: Partial<Owner>): Promise<Owner> {
    const o = await firstValueFrom(
      this.http.patch<ApiOwner>(`${this.base}/property/owner`, partial)
    );
    return {
      firstName: o.firstName, lastName: o.lastName, ownerName2: null,
      memberSince: null, accountNumber: o.accountNumber,
      communityName: o.communityName, propertyAddress: o.propertyAddress,
      votingRights: o.votingRights, email: o.email, phone: o.phone,
      mailingToProperty: o.mailingToProperty, paperlessStatements: false, smsReminders: false,
    };
  }

  async toggleDirectoryField(key: string, shared: boolean): Promise<void> {
    await firstValueFrom(
      this.http.patch(`${this.base}/property/directory-fields/${key}`, { shared })
    );
  }
}
