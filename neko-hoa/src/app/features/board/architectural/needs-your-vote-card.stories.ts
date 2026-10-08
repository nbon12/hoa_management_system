import type { Meta, StoryObj } from '@storybook/angular';
import { applicationConfig } from '@storybook/angular';
import { signal } from '@angular/core';
import { provideRouter } from '@angular/router';
import { NeedsYourVoteCardComponent } from './needs-your-vote-card.component';
import { ArcListItem, ArchitecturalService } from '../../../core/services/architectural.service';
import { AuthService } from '../../../core/services/auth.service';
import { BoardNavigationService } from '../../../core/services/board-navigation.service';
import { SAMPLE_ROW } from './arc-fixtures';

// 027 T050/T075 — the wireframe NeedsYourVote card on Community Home.
function withItems(items: ArcListItem[]) {
  return applicationConfig({
    providers: [
      provideRouter([]),
      {
        provide: ArchitecturalService,
        useValue: {
          list: () => Promise.resolve({ items, total: items.length, limit: 25, offset: 0, counts: { open: items.length, closed: 0, awaitingMyVote: items.length } }),
          vote: () => Promise.resolve(items[0]),
        },
      },
      { provide: AuthService, useValue: { user: signal({ memberships: [{ communityId: 'c1', communityName: 'Keystone Crossing', role: 'BoardMember' }] }).asReadonly() } },
      { provide: BoardNavigationService, useValue: { activeCommunityId: signal('c1').asReadonly() } },
    ],
  });
}

const meta: Meta<NeedsYourVoteCardComponent> = { title: 'Board/Architectural/NeedsYourVote', component: NeedsYourVoteCardComponent };
export default meta;
type Story = StoryObj<NeedsYourVoteCardComponent>;

export const OneOpen: Story = { decorators: [withItems([SAMPLE_ROW])] };
export const Empty: Story = { decorators: [withItems([])] };
