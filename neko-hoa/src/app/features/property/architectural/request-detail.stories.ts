import type { Meta, StoryObj } from '@storybook/angular';
import { applicationConfig } from '@storybook/angular';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { RequestDetailComponent } from './request-detail.component';
import { ResidentArchitecturalService } from '../../../core/services/resident-architectural.service';
import { ResidentArcDetail } from '../../../core/models';
import { APPROVED, DENIED, DETAIL, MORE_INFO } from './resident-arc-fixtures';

// 029 T066 — a resident's request: under review, more info requested, approved, denied.
function withDetail(detail: ResidentArcDetail) {
  return applicationConfig({
    providers: [
      provideRouter([]),
      { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: detail.id }) } } },
      { provide: ResidentArchitecturalService, useValue: {
        detail: () => Promise.resolve(detail),
        attachmentUrl: () => Promise.resolve({ url: 'about:blank', expiresAt: '' }),
        withdraw: () => Promise.resolve({ ...detail, status: 'Withdrawn', canWithdraw: false }),
        reply: () => Promise.resolve({ ...detail, status: 'Submitted' }),
        uploadReplyAttachment: () => Promise.resolve({ id: 'f2', fileName: 'survey.pdf', sizeBytes: 1000, contentType: 'application/pdf' }),
      } },
    ],
  });
}

const meta: Meta<RequestDetailComponent> = { title: 'Property/Architectural/RequestDetail', component: RequestDetailComponent };
export default meta;
type Story = StoryObj<RequestDetailComponent>;

export const UnderReview: Story = { decorators: [withDetail(DETAIL)] };
export const MoreInfoRequested: Story = { decorators: [withDetail(MORE_INFO)] };
export const Approved: Story = { decorators: [withDetail(APPROVED)] };
export const DeniedRevisionsRequested: Story = { decorators: [withDetail(DENIED)] };
export const DeniedOnPhone: Story = { decorators: [withDetail(DENIED)], parameters: { viewport: { defaultViewport: 'mobile1' } } };
