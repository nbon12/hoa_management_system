import { HttpErrorResponse } from '@angular/common/http';
import { fireEvent, render, screen } from '@testing-library/angular';
import { InfoReplyComponent } from './info-reply.component';
import { ResidentArchitecturalService } from '../../../core/services/resident-architectural.service';
import { DETAIL, MORE_INFO } from './resident-arc-fixtures';
import { until } from '../../board/architectural/arc-testing';

describe('InfoReplyComponent (029 US3)', () => {
  async function setup() {
    const api = jasmine.createSpyObj<ResidentArchitecturalService>('ResidentArchitecturalService', ['reply', 'uploadReplyAttachment']);
    const replied = jasmine.createSpy('replied');
    await render(InfoReplyComponent, {
      componentInputs: { applicationId: 'a1', request: MORE_INFO.infoRequests[0] },
      componentOutputs: { replied: { emit: replied } as never },
      providers: [{ provide: ResidentArchitecturalService, useValue: api }],
    });
    return { api, replied };
  }

  it('blocks sending a blank reply', async () => {
    const { api } = await setup();
    expect((screen.getByRole('button', { name: /Send reply/ }) as HTMLButtonElement).disabled).toBeTrue();
    fireEvent.input(screen.getByLabelText('Your reply'), { target: { value: '   ' } });
    expect((screen.getByRole('button', { name: /Send reply/ }) as HTMLButtonElement).disabled).toBeTrue();
    expect(api.reply).not.toHaveBeenCalled();
  });

  // US3 AS2: sending the reply (with a file) posts it and hands back the refreshed request.
  it('uploads a file, sends the reply, and emits the refreshed request', async () => {
    const { api, replied } = await setup();
    api.uploadReplyAttachment.and.resolveTo({ id: 'f2', fileName: 'survey.pdf', sizeBytes: 10, contentType: 'application/pdf' });
    api.reply.and.resolveTo({ ...DETAIL, status: 'Submitted' });
    const input = document.querySelector('input[type=file]') as HTMLInputElement;
    Object.defineProperty(input, 'files', { value: [new File(['%PDF-'], 'survey.pdf')] });
    fireEvent.change(input);
    await until(() => !!screen.queryByText(/survey\.pdf/));

    fireEvent.input(screen.getByLabelText('Your reply'), { target: { value: 'Survey attached' } });
    fireEvent.click(screen.getByRole('button', { name: /Send reply/ }));

    await until(() => replied.calls.count() === 1);
    expect(api.uploadReplyAttachment).toHaveBeenCalledWith('a1', 'q1', jasmine.any(File));
    expect(api.reply).toHaveBeenCalledWith('a1', 'q1', 'Survey attached');
    expect(replied.calls.mostRecent().args[0].status).toBe('Submitted');
  });

  it('shows a refusal such as an already-answered question', async () => {
    const { api } = await setup();
    api.reply.and.rejectWith(new HttpErrorResponse({ status: 409, error: { code: 'INFO_ALREADY_ANSWERED' } }));
    fireEvent.input(screen.getByLabelText('Your reply'), { target: { value: 'Again' } });
    fireEvent.click(screen.getByRole('button', { name: /Send reply/ }));
    await until(() => !!screen.queryByRole('alert'));
    expect(screen.getByRole('alert').textContent).toContain('already been answered');
  });
});
