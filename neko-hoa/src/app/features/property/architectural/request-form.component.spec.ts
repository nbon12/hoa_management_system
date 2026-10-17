import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Location } from '@angular/common';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { fireEvent, render, screen } from '@testing-library/angular';
import { RequestFormComponent } from './request-form.component';
import { ResidentArchitecturalService } from '../../../core/services/resident-architectural.service';
import { DETAIL, DRAFT } from './resident-arc-fixtures';
import { until } from '../../board/architectural/arc-testing';

describe('RequestFormComponent (029 US1/US5/US6)', () => {
  let api: jasmine.SpyObj<ResidentArchitecturalService>;
  let router: Router;
  let location: Location;

  async function setup(params: Record<string, string> = {}, lastSegment = 'new',
                       arrange: (spy: jasmine.SpyObj<ResidentArchitecturalService>) => void = () => {}) {
    api = jasmine.createSpyObj<ResidentArchitecturalService>('ResidentArchitecturalService',
      ['createDraft', 'getDraft', 'updateDraft', 'deleteDraft', 'uploadDraftAttachment', 'deleteDraftAttachment',
        'draftAttachmentUrl', 'submitDraft', 'revise']);
    api.createDraft.and.callFake(async body => ({ ...DRAFT, ...body, id: 'd1' }));
    api.updateDraft.and.callFake(async (id, body) => ({ ...DRAFT, ...body, id }));
    api.getDraft.and.resolveTo(DRAFT);
    api.submitDraft.and.resolveTo(DETAIL);
    arrange(api);
    await render(RequestFormComponent, {
      providers: [
        // A catch-all so the component's replaceUrl navigations succeed, as they do in the app.
        provideRouter([{ path: '**', children: [] }]),
        { provide: ResidentArchitecturalService, useValue: api },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap(params), url: [{ path: lastSegment }] } } },
      ],
    });
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
    location = TestBed.inject(Location);
    spyOn(location, 'replaceState');
  }

  async function fillComplete(acknowledge = true) {
    fireEvent.input(screen.getByLabelText('Project title'), { target: { value: 'Fence replacement' } });
    fireEvent.input(screen.getByLabelText('Description of the work'), { target: { value: 'Rear fence' } });
    fireEvent.input(screen.getByLabelText('Planned start date'), { target: { value: '2026-07-01' } });
    fireEvent.input(screen.getByLabelText('Planned completion date'), { target: { value: '2026-07-20' } });
    if (acknowledge) fireEvent.click(screen.getByLabelText('I understand work may not begin until this request is approved.'));
  }

  // US1 AS5 (UX): without the acknowledgement the Submit button is disabled; the server enforces it too.
  it('disables Submit until the acknowledgement is checked', async () => {
    await setup();
    await fillComplete(false);
    expect((screen.getByRole('button', { name: /Submit request/ }) as HTMLButtonElement).disabled).toBeTrue();
    fireEvent.click(screen.getByLabelText('I understand work may not begin until this request is approved.'));
    await until(() => !(screen.getByRole('button', { name: /Submit request/ }) as HTMLButtonElement).disabled);
  });

  // US1 AS6 (UX): completion before start shows the date error and blocks Submit.
  it('shows the date-order error and blocks Submit', async () => {
    await setup();
    await fillComplete();
    fireEvent.input(screen.getByLabelText('Planned completion date'), { target: { value: '2026-06-01' } });
    await until(() => !!screen.queryByText("The completion date can't be before the start date."));
    expect((screen.getByRole('button', { name: /Submit request/ }) as HTMLButtonElement).disabled).toBeTrue();
    expect(screen.getByLabelText('Planned completion date').getAttribute('aria-invalid')).toBe('true');
  });

  // US1 AS2/AS3: Save draft creates the draft the first time and updates it afterwards.
  it('Save draft creates, then updates', async () => {
    await setup();
    fireEvent.input(screen.getByLabelText('Project title'), { target: { value: 'Half done' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save draft' }));
    await until(() => api.createDraft.calls.count() === 1);
    expect(api.createDraft.calls.mostRecent().args[0].projectTitle).toBe('Half done');
    // The address bar now points at the draft, without rebuilding the component.
    expect(location.replaceState).toHaveBeenCalledWith('/app/property/architectural/drafts/d1');
    expect(router.navigate).not.toHaveBeenCalled();

    fireEvent.click(screen.getByRole('button', { name: 'Save draft' }));
    await until(() => api.updateDraft.calls.count() === 1);
    expect(api.updateDraft.calls.mostRecent().args[0]).toBe('d1');
    expect(screen.getByRole('status').textContent).toContain('Draft saved');
  });

  // US1 AS1: Submit saves the draft, submits it, and goes to the new request.
  it('Submit saves and submits, then navigates to the request', async () => {
    await setup();
    await fillComplete();
    fireEvent.click(screen.getByRole('button', { name: /Submit request/ }));
    await until(() => api.submitDraft.calls.count() === 1);
    expect(api.submitDraft).toHaveBeenCalledWith('d1');
    expect(api.createDraft.calls.mostRecent().args[0]).toEqual(jasmine.objectContaining({
      projectTitle: 'Fence replacement', plannedStartDate: '2026-07-01', plannedCompletionDate: '2026-07-20', acknowledged: true,
    }));
    await until(() => (router.navigate as jasmine.Spy).calls.allArgs().some(a => a[0][1] === DETAIL.id));
  });

  // US1 AS5 (server): a server ACKNOWLEDGEMENT_REQUIRED refusal shows the mapped message.
  it('shows the server refusal message', async () => {
    await setup();
    await fillComplete();
    api.submitDraft.and.rejectWith(new HttpErrorResponse({ status: 422, error: { code: 'ACKNOWLEDGEMENT_REQUIRED' } }));
    fireEvent.click(screen.getByRole('button', { name: /Submit request/ }));
    await until(() => !!screen.queryByRole('alert'));
    expect(screen.getByRole('alert').textContent).toContain('work may not begin until this request is approved');
  });

  // US5 AS1/AS2 (UX): uploads go through the service; a rejected file shows its message; Remove deletes it.
  it('uploads files, shows per-file errors, and removes files', async () => {
    await setup({ draftId: 'd1' }, 'd1');
    await until(() => !!document.querySelector('input[type=file]'));
    expect(api.getDraft).toHaveBeenCalledWith('d1');
    api.uploadDraftAttachment.and.callFake(async (_d, f) => {
      if (f.name === 'bad.pdf') throw new HttpErrorResponse({ status: 422, error: { code: 'UNSUPPORTED_FILE_TYPE' } });
      return { id: 'f9', fileName: f.name, sizeBytes: 10, contentType: 'application/pdf' };
    });
    api.deleteDraftAttachment.and.resolveTo();
    const input = document.querySelector('input[type=file]') as HTMLInputElement;
    const good = new File(['%PDF-'], 'plan.pdf', { type: 'application/pdf' });
    const bad = new File(['MZ'], 'bad.pdf', { type: 'application/pdf' });
    Object.defineProperty(input, 'files', { value: [good, bad] });
    fireEvent.change(input);

    await until(() => !!screen.queryByText(/plan\.pdf/) && !!screen.queryByText(/bad\.pdf: Only PDF/));
    fireEvent.click(screen.getByRole('button', { name: 'Remove plan.pdf' }));
    await until(() => api.deleteDraftAttachment.calls.count() === 1);
    expect(api.deleteDraftAttachment).toHaveBeenCalledWith('d1', 'f9');
  });

  // US6 AS1/AS2: the revise route starts a revision draft (pre-filled, files carried over); removing a carried
  // file sends its id in removedCarriedAttachmentIds — it is never deleted.
  it('revise mode creates the revision draft and removes carried files from it only', async () => {
    const revision = { ...DRAFT, id: 'd2', previousRevisionId: 'a1', previousDisplayId: 'ARC-1042',
      carriedAttachments: [{ id: 'c1', fileName: 'old-plan.pdf', sizeBytes: 10, contentType: 'application/pdf' }] };
    await setup({ id: 'a1' }, 'revise', spy => {
      spy.revise.and.resolveTo(revision);
      spy.updateDraft.and.resolveTo({ ...revision, carriedAttachments: [], removedCarriedAttachmentIds: ['c1'] });
    });

    await until(() => !!screen.queryByText(/This is a revision of ARC-1042/));
    expect(api.revise).toHaveBeenCalledWith('a1');
    expect((screen.getByLabelText('Project title') as HTMLInputElement).value).toBe(DRAFT.projectTitle);
    expect(screen.getByRole('heading', { name: 'Revise ARC-1042' })).toBeTruthy();

    fireEvent.click(screen.getByRole('button', { name: 'Remove old-plan.pdf' }));
    await until(() => api.updateDraft.calls.count() === 1);
    expect(api.updateDraft.calls.mostRecent().args[1].removedCarriedAttachmentIds).toEqual(['c1']);
    expect(api.deleteDraftAttachment).not.toHaveBeenCalled();
    await until(() => !screen.queryByText(/old-plan\.pdf/));
  });

  it('shows why a revise was refused', async () => {
    await setup({ id: 'a1' }, 'revise', spy =>
      spy.revise.and.rejectWith(new HttpErrorResponse({ status: 409, error: { code: 'REVISION_NOT_ALLOWED' } })));
    await until(() => !!screen.queryByRole('alert'));
    expect(screen.getByRole('alert').textContent).toContain('can no longer be revised');
  });

  // Regression (found by Playwright): the input is cleared right after (change), and a real FileList is live.
  // Picking a file on a NEW request (which first creates the draft) must still upload it.
  it('uploads files picked before the draft exists, even though the input is cleared', async () => {
    await setup();
    api.uploadDraftAttachment.and.callFake(async (_d, f) => ({ id: 'f1', fileName: f.name, sizeBytes: 10, contentType: 'application/pdf' }));
    const input = document.querySelector('input[type=file]') as HTMLInputElement;
    const live: File[] = [new File(['%PDF-'], 'plan.pdf', { type: 'application/pdf' })];
    Object.defineProperty(input, 'files', { get: () => live });
    Object.defineProperty(input, 'value', { set: () => { live.length = 0; }, get: () => '' });
    fireEvent.change(input);

    await until(() => api.uploadDraftAttachment.calls.count() === 1);
    expect(api.createDraft).toHaveBeenCalled();
    expect(api.uploadDraftAttachment.calls.mostRecent().args[1].name).toBe('plan.pdf');
  });
});
