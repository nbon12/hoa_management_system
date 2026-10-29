import { fireEvent, render, screen } from '@testing-library/angular';
import { CommentThreadComponent } from './comment-thread.component';
import { ResidentArchitecturalService } from '../../../core/services/resident-architectural.service';
import { DETAIL } from './resident-arc-fixtures';
import { until } from '../../board/architectural/arc-testing';

describe('CommentThreadComponent (031 US7)', () => {
  let api: jasmine.SpyObj<ResidentArchitecturalService>;

  async function setup() {
    api = jasmine.createSpyObj<ResidentArchitecturalService>('ResidentArchitecturalService', ['comment']);
    const posted = jasmine.createSpy('posted');
    await render(CommentThreadComponent, {
      componentInputs: { detail: DETAIL },
      componentOutputs: { posted: { emit: posted } as any },
      providers: [{ provide: ResidentArchitecturalService, useValue: api }],
    });
    return { posted };
  }

  const pdf = (name: string) => new File(['%PDF-1.7'], name, { type: 'application/pdf' });

  // 031 US7-S3: nothing to post until there's text or a file.
  it('disables Post comment until there is text or a file', async () => {
    await setup();
    expect((screen.getByRole('button', { name: /Post comment/ }) as HTMLButtonElement).disabled).toBeTrue();
    fireEvent.change(screen.getByLabelText(/Attach files/), { target: { files: [pdf('a.pdf')] } });
    expect((screen.getByRole('button', { name: /Post comment/ }) as HTMLButtonElement).disabled).toBeFalse();
  });

  // 031 FR-022: at most five files per comment; a picked file can be removed.
  it('limits a comment to five files and lets me remove one', async () => {
    await setup();
    const input = screen.getByLabelText(/Attach files/);
    fireEvent.change(input, { target: { files: [1, 2, 3, 4, 5].map(i => pdf(`f${i}.pdf`)) } });
    fireEvent.change(input, { target: { files: [pdf('f6.pdf')] } });
    expect(screen.getByRole('alert').textContent).toContain('at most 5 files');
    fireEvent.click(screen.getByRole('button', { name: 'Remove f1.pdf' }));
    expect(screen.queryByText(/f1\.pdf/)).toBeNull();
  });

  it('shows the server message when posting fails', async () => {
    await setup();
    api.comment.and.rejectWith({ error: { code: 'APPLICATION_CLOSED' } });
    fireEvent.input(screen.getByLabelText('Add a comment or revised plans'), { target: { value: 'Hello' } });
    fireEvent.click(screen.getByRole('button', { name: /Post comment/ }));
    await until(() => !!screen.queryByRole('alert'));
  });

  it('emits the refreshed request after posting', async () => {
    const { posted } = await setup();
    api.comment.and.resolveTo({ ...DETAIL, comments: [{ id: 'c1', body: 'Hello', createdAt: '2026-06-21T15:00:00Z', attachments: [] }] });
    fireEvent.input(screen.getByLabelText('Add a comment or revised plans'), { target: { value: '  Hello  ' } });
    fireEvent.click(screen.getByRole('button', { name: /Post comment/ }));
    await until(() => posted.calls.count() === 1);
    expect(api.comment).toHaveBeenCalledWith('a1', 'Hello', []);
  });
});
