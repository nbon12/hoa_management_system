import { BoardService } from '../../core/services/board.service';
import { CompanyService } from '../../core/services/company.service';
import { PickerOption } from '../../shared/combo-picker.component';

export interface CommunityOption {
  id: string;
  communityName: string;
  status: string;
}

/** Picker rows, A–Z, with "Onboarding" as a text chip (Active needs none, conventions §5.1). */
export function toPickerOptions(communities: CommunityOption[]): PickerOption[] {
  return [...communities]
    .sort((a, b) => a.communityName.localeCompare(b.communityName, 'en', { sensitivity: 'base' }))
    .map(c => ({ id: c.id, label: c.communityName, chip: c.status === 'Onboarding' ? 'Onboarding' : null }));
}

/** Pages are read 100 at a time (the API's maximum), up to this many pages. */
const MAX_PAGES = 20;

/**
 * 030 FR-016 / brief 03 §4.1: the communities that may be chosen as a parent.
 * - A Company Administrator: every Onboarding or Active community of the company (`GET /company/communities`).
 * - A Community Manager: only the Onboarding or Active communities they manage (`GET /me/communities`).
 * Someone who is both gets the Company Administrator's list (spec Edge Cases).
 */
export async function loadParentOptions(
  asCompanyAdministrator: boolean, company: CompanyService, board: BoardService,
): Promise<CommunityOption[]> {
  const found = new Map<string, CommunityOption>();
  for (let page = 0, offset = 0; page < MAX_PAGES; page++) {
    if (asCompanyAdministrator) {
      const res = await company.getCommunities({ limit: 100, offset });
      for (const c of res.items) {
        if (c.status !== 'Archived') found.set(c.id, { id: c.id, communityName: c.communityName, status: c.status });
      }
      offset += res.items.length;
      if (!res.items.length || offset >= res.total) break;
    } else {
      const res = await board.getMyCommunities(100, offset);
      for (const c of res.items) {
        if (c.role === 'CommunityManager' && c.status !== 'Archived') {
          found.set(c.id, { id: c.id, communityName: c.communityName, status: c.status });
        }
      }
      offset += res.items.length;
      if (!res.items.length || offset >= res.total) break;
    }
  }
  return Array.from(found.values());
}
