import type { Meta, StoryObj } from '@storybook/angular';
import { applicationConfig } from '@storybook/angular';
import { provideRouter } from '@angular/router';
import { MyRequestsPageComponent } from './my-requests-page.component';
import { ResidentArchitecturalService } from '../../../core/services/resident-architectural.service';
import { ResidentArcListItem } from '../../../core/models';
import { LIST_ITEMS } from './resident-arc-fixtures';

// 029 T066 — "My architectural requests" (resident). Phone viewport stories cover FR-027.
function withItems(items: ResidentArcListItem[]) {
  return applicationConfig({
    providers: [
      provideRouter([]),
      { provide: ResidentArchitecturalService, useValue: { list: () => Promise.resolve({ items, total: items.length, limit: 25, offset: 0 }) } },
    ],
  });
}

const meta: Meta<MyRequestsPageComponent> = { title: 'Property/Architectural/MyRequests', component: MyRequestsPageComponent };
export default meta;
type Story = StoryObj<MyRequestsPageComponent>;

export const Mixed: Story = { decorators: [withItems(LIST_ITEMS)] };
export const Empty: Story = { decorators: [withItems([])] };
export const Phone: Story = { decorators: [withItems(LIST_ITEMS)], parameters: { viewport: { defaultViewport: 'mobile1' } } };
