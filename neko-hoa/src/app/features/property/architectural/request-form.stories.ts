import type { Meta, StoryObj } from '@storybook/angular';
import { applicationConfig } from '@storybook/angular';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { RequestFormComponent } from './request-form.component';
import { ResidentArchitecturalService } from '../../../core/services/resident-architectural.service';
import { DRAFT } from './resident-arc-fixtures';

// 029 T066 — new request, editing a draft with files, and a revise-and-resubmit draft.
function withRoute(params: Record<string, string>, last: string) {
  return applicationConfig({
    providers: [
      provideRouter([]),
      { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap(params), url: [{ path: last }] } } },
      { provide: ResidentArchitecturalService, useValue: {
        getDraft: () => Promise.resolve({ ...DRAFT, attachments: [{ id: 'f1', fileName: 'fence-plan.pdf', sizeBytes: 1_258_291, contentType: 'application/pdf' }] }),
        revise: () => Promise.resolve({ ...DRAFT, id: 'd2', previousRevisionId: 'a1', previousDisplayId: 'ARC-1042',
          carriedAttachments: [{ id: 'c1', fileName: 'old-plan.pdf', sizeBytes: 900_000, contentType: 'application/pdf' }] }),
        createDraft: (b: object) => Promise.resolve({ ...DRAFT, ...b }),
        updateDraft: (_: string, b: object) => Promise.resolve({ ...DRAFT, ...b }),
      } },
    ],
  });
}

const meta: Meta<RequestFormComponent> = { title: 'Property/Architectural/RequestForm', component: RequestFormComponent };
export default meta;
type Story = StoryObj<RequestFormComponent>;

export const NewRequest: Story = { decorators: [withRoute({}, 'new')] };
export const EditDraft: Story = { decorators: [withRoute({ draftId: 'd1' }, 'd1')] };
export const Revision: Story = { decorators: [withRoute({ id: 'a1' }, 'revise')] };
export const NewRequestOnPhone: Story = { decorators: [withRoute({}, 'new')], parameters: { viewport: { defaultViewport: 'mobile1' } } };
