import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { Router, provideRouter } from '@angular/router';
import { render, screen, fireEvent, within } from '@testing-library/angular';
import { until } from '../board/architectural/arc-testing';
import { CompanyAdministratorsComponent } from './company-administrators.component';
import { CompanyAdministrator, CompanyInvitation, CompanyService } from '../../core/services/company.service';
import { AuthService } from '../../core/services/auth.service';
import { ToastService } from '../../core/services/toast.service';
import { CurrentUser } from '../../core/models';
import { Paged } from '../../core/services/board.service';
import { formatDateTimeInZone } from '../../shared/date-format';

// 030 T137 / US7 / brief 09: the Company administrators page. Administrators and their invitations,
// each paged; an email-only invite form; Revoke role asks first, the last administrator's Revoke is
// disabled with its reason, and revoking yourself says you lose access.

@Component({ selector: 'app-landing-stub', standalone: true, template: '<h1>Landing</h1>' })
class LandingStubComponent {}

const TZ = 'America/New_York';
const ROBIN = admin('u-robin', 'Robin Hale', 'owner@example.com', { source: 'Setup', grantedBy: null, grantedAt: '2026-10-01T14:00:00Z' });
const CASEY = admin('u-casey', 'Casey Novak', 'casey@example.com', { source: 'Invitation', grantedBy: 'Robin Hale', grantedAt: '2026-10-02T14:00:00Z' });
const OPS: CompanyInvitation = {
  id: 'i-ops', email: 'ops@example.com', state: 'Pending', effectiveState: 'Pending', revokeReason: null, sentBy: 'Robin Hale',
  sentAt: '2026-10-12T12:30:00Z', expiresAt: '2099-10-15T12:30:00Z', canResend: true, canRevoke: true,
};

function admin(userId: string, name: string, email: string, over: Partial<CompanyAdministrator>): CompanyAdministrator {
  return { userId, name, email, grantedAt: '2026-10-01T14:00:00Z', grantedBy: null, source: 'Setup', ...over };
}

function paged<T>(items: T[], total = items.length, offset = 0): Paged<T> {
  return { items, total, limit: 25, offset };
}

function me(userId = 'u-robin'): CurrentUser {
  return {
    id: userId, firstName: 'Robin', lastName: 'Hale', email: 'owner@example.com', initials: 'RH', lastActiveMode: 'Resident',
    memberships: [], properties: [], isCompanyAdministrator: true, landing: 'Company',
  };
}

function httpError(status: number, error: unknown = null): HttpErrorResponse {
  return new HttpErrorResponse({ status, error });
}

describe('CompanyAdministratorsComponent (030 T137)', () => {
  let company: jasmine.SpyObj<CompanyService>;
  let refreshCurrentUser: jasmine.Spy;

  async function setup(opts: { admins?: Paged<CompanyAdministrator> | HttpErrorResponse; invitations?: Paged<CompanyInvitation> | HttpErrorResponse; user?: CurrentUser } = {}) {
    company = jasmine.createSpyObj<CompanyService>('CompanyService', [
      'getAdministrators', 'getInvitations', 'inviteAdministrator', 'revokeAdministrator', 'resendInvitation', 'revokeInvitation', 'getDefaults',
    ]);
    const a = opts.admins ?? paged([ROBIN, CASEY]);
    if (a instanceof HttpErrorResponse) company.getAdministrators.and.rejectWith(a);
    else company.getAdministrators.and.resolveTo(a);
    const i = opts.invitations ?? paged([OPS]);
    if (i instanceof HttpErrorResponse) company.getInvitations.and.rejectWith(i);
    else company.getInvitations.and.resolveTo(i);
    company.getDefaults.and.resolveTo({
      reviewPeriodDays: 30, reminderDays: 7, lapseRule: 'FlagOverdueOnly', decisionRule: 'MajorityOfMembers', timeZoneId: TZ,
      formalDisapprovalStatement: 'x', updatedAt: null, canEdit: true,
    });
    const user = opts.user ?? me();
    refreshCurrentUser = jasmine.createSpy('refreshCurrentUser').and.resolveTo({ ...user, isCompanyAdministrator: false, landing: 'NoHome' });
    const r = await render(CompanyAdministratorsComponent, {
      providers: [
        provideRouter([{ path: '**', component: LandingStubComponent }]),
        { provide: CompanyService, useValue: company },
        { provide: AuthService, useValue: { user: signal(user).asReadonly(), refreshCurrentUser } },
      ],
    });
    await settle(r);
    return r;
  }

  async function settle(r: Awaited<ReturnType<typeof setup>>) {
    await r.fixture.whenStable();
    r.fixture.detectChanges();
    await r.fixture.whenStable();
    r.fixture.detectChanges();
  }

  function adminsTable(): HTMLElement {
    return screen.getByRole('table', { name: 'Current Company Administrators' });
  }

  function invitationsTable(): HTMLElement {
    return screen.getByRole('table', { name: 'Company Administrator invitations' });
  }

  function rows(table: HTMLElement): HTMLElement[] {
    return within(table).getAllByRole('row').slice(1);
  }

  function toasts(): string[] {
    return TestBed.inject(ToastService).toasts().map(t => t.message);
  }

  afterEach(() => TestBed.inject(ToastService).clear());

  describe('the lists', () => {
    it('lists the administrators, with "(you)" and "Setup step", and what this role is (8.09.a)', async () => {
      await setup();
      expect(screen.getByRole('heading', { level: 1, name: 'Company administrators' })).toBeTruthy();
      expect(screen.getByRole('region', { name: 'What this role is' }).textContent).toContain('It gives no board powers in any community');
      expect(within(adminsTable()).getAllByRole('columnheader').map(h => h.textContent!.trim())).toEqual(['Name', 'Email', 'Granted by', 'Granted', 'Actions']);
      const [robin, casey] = rows(adminsTable());
      expect(within(robin).getByRole('rowheader').textContent!.trim()).toBe('Robin Hale (you)');
      expect(robin.textContent).toContain('owner@example.com');
      expect(robin.textContent).toContain('Setup step');
      expect(robin.textContent).toContain('Oct 1, 2026');
      expect(casey.textContent).toContain('Robin Hale');
      expect(screen.getByText('The company always needs at least one Company Administrator. The last one can\'t be revoked.')).toBeTruthy();
      expect(within(robin).getByRole('button', { name: 'Revoke your own Company Administrator role' })).toBeTruthy();
      expect(within(casey).getByRole('button', { name: "Revoke Casey Novak's Company Administrator role" })).toBeTruthy();
      expect(company.getAdministrators).toHaveBeenCalledOnceWith(25, 0);
    });

    it('lists the invitations with sender, times in the company zone, status, Resend and Revoke', async () => {
      await setup();
      expect(within(invitationsTable()).getAllByRole('columnheader').map(h => h.textContent!.trim())).toEqual(['Email', 'Sent by', 'Sent', 'Expires', 'Status', 'Actions']);
      const [ops] = rows(invitationsTable());
      expect(ops.textContent).toContain('ops@example.com');
      expect(ops.textContent).toContain(formatDateTimeInZone('2026-10-12T12:30:00Z', TZ));
      expect(ops.textContent).toContain(formatDateTimeInZone('2099-10-15T12:30:00Z', TZ));
      expect(within(ops).getByText('Pending')).toBeTruthy();
      expect(within(ops).getByRole('button', { name: 'Resend invitation to ops@example.com' })).toBeTruthy();
      expect(within(ops).getByRole('button', { name: 'Revoke invitation to ops@example.com' })).toBeTruthy();
      expect(company.getInvitations).toHaveBeenCalledOnceWith({ limit: 25, offset: 0 });
    });

    it('pages each list on its own', async () => {
      const r = await setup({ admins: paged([ROBIN, CASEY], 30), invitations: paged([OPS], 40) });
      const pages = screen.getAllByRole('navigation', { name: 'Pages' });
      expect(pages.length).toBe(2);
      company.getAdministrators.and.resolveTo(paged([CASEY], 30, 25));
      fireEvent.click(within(pages[0]).getByRole('button', { name: 'Next page' }));
      await settle(r);
      expect(company.getAdministrators).toHaveBeenCalledWith(25, 25);
      company.getInvitations.and.resolveTo(paged([OPS], 40, 25));
      fireEvent.click(within(screen.getAllByRole('navigation', { name: 'Pages' })[1]).getByRole('button', { name: 'Next page' }));
      await settle(r);
      expect(company.getInvitations).toHaveBeenCalledWith({ limit: 25, offset: 25 });
    });

    it('says "No invitations." when there are none', async () => {
      await setup({ invitations: paged([]) });
      expect(screen.getByText('No invitations.')).toBeTruthy();
    });

    it('says when a list could not load, in that card, and tries again', async () => {
      const r = await setup({ admins: httpError(0), invitations: httpError(0) });
      expect(screen.getByText("Couldn't load Company Administrators.")).toBeTruthy();
      expect(screen.getByText("Couldn't load invitations.")).toBeTruthy();
      company.getAdministrators.and.resolveTo(paged([ROBIN, CASEY]));
      fireEvent.click(screen.getAllByRole('button', { name: 'Try again' })[0]);
      await settle(r);
      expect(rows(adminsTable()).length).toBe(2);
    });

    it('a page refusal goes to the landing with the page message (US7-S5)', async () => {
      await setup({ admins: httpError(403, { code: 'FORBIDDEN', message: 'No.' }) });
      expect(toasts()).toContain("You don't have access to that page.");
    });

    it('Resend sends again with no confirmation (FR-022)', async () => {
      const r = await setup();
      company.resendInvitation.and.resolveTo(OPS);
      fireEvent.click(screen.getByRole('button', { name: 'Resend invitation to ops@example.com' }));
      await settle(r);
      expect(company.resendInvitation).toHaveBeenCalledOnceWith('i-ops');
      expect(toasts()).toEqual(['Invitation sent again to ops@example.com. The earlier link no longer works.']);
      expect(company.getInvitations).toHaveBeenCalledTimes(2);
    });

    it('a resend of an invitation accepted meanwhile says the list is up to date; a rate limit shows by the button', async () => {
      const r = await setup();
      company.resendInvitation.and.rejectWith(httpError(409, { code: 'INVITATION_INVALID', message: 'Invalid.' }));
      fireEvent.click(screen.getByRole('button', { name: 'Resend invitation to ops@example.com' }));
      await settle(r);
      expect(toasts()).toEqual(['This invitation was already accepted or revoked. The list is now up to date.']);
      company.resendInvitation.and.rejectWith(httpError(429));
      fireEvent.click(screen.getByRole('button', { name: 'Resend invitation to ops@example.com' }));
      await settle(r);
      expect(within(rows(invitationsTable())[0]).getByText('Too many requests. Wait a few minutes, then try again.')).toBeTruthy();
    });

    it('revoking an invitation: accepted meanwhile, a network failure, a rate limit, and FORBIDDEN', async () => {
      const r = await setup();
      fireEvent.click(screen.getByRole('button', { name: 'Revoke invitation to ops@example.com' }));
      await settle(r);
      const dlg = screen.getByRole('dialog');
      company.revokeInvitation.and.rejectWith(httpError(0));
      fireEvent.click(within(dlg).getByRole('button', { name: 'Revoke invitation' }));
      await settle(r);
      expect(within(dlg).getByText("Couldn't save. Check your connection and try again.")).toBeTruthy();
      company.revokeInvitation.and.rejectWith(httpError(429));
      fireEvent.click(within(dlg).getByRole('button', { name: 'Revoke invitation' }));
      await settle(r);
      expect(within(dlg).getByText('Too many requests. Wait a few minutes, then try again.')).toBeTruthy();
      company.revokeInvitation.and.rejectWith(httpError(409, { code: 'INVITATION_INVALID', message: 'Invalid.' }));
      fireEvent.click(within(dlg).getByRole('button', { name: 'Revoke invitation' }));
      await settle(r);
      expect(screen.queryByRole('dialog')).toBeNull();
      expect(toasts()).toEqual(['This invitation was already accepted or revoked. The list is now up to date.']);

      fireEvent.click(screen.getByRole('button', { name: 'Revoke invitation to ops@example.com' }));
      await settle(r);
      company.revokeInvitation.and.rejectWith(httpError(403, { code: 'FORBIDDEN', message: 'No.' }));
      fireEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Revoke invitation' }));
      await settle(r);
      fireEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Close' }));
      await settle(r);
      expect(screen.queryByRole('dialog')).toBeNull();
    });

    it('a resend that fails for a network reason says so', async () => {
      const r = await setup();
      company.resendInvitation.and.rejectWith(httpError(0));
      fireEvent.click(screen.getByRole('button', { name: 'Resend invitation to ops@example.com' }));
      await settle(r);
      expect(toasts()).toEqual(["Couldn't resend the invitation. Check your connection and try again."]);
    });

    it('Revoke on an invitation asks first (8.09.f)', async () => {
      const r = await setup();
      fireEvent.click(screen.getByRole('button', { name: 'Revoke invitation to ops@example.com' }));
      await settle(r);
      const dlg = screen.getByRole('dialog', { name: 'Revoke the invitation to ops@example.com?' });
      const text = dlg.textContent!.replace(/\s+/g, ' ');
      expect(text).toContain('The link in the invitation email stops working at once.');
      expect(text).toContain("ops@example.com can't use it to become a Company Administrator.");
      expect(text).toContain('You can send ops@example.com a new invitation later.');
      company.revokeInvitation.and.resolveTo({ ...OPS, state: 'Revoked', effectiveState: 'Revoked' });
      company.getInvitations.and.resolveTo(paged([]));
      fireEvent.click(within(dlg).getByRole('button', { name: 'Revoke invitation' }));
      await settle(r);
      expect(company.revokeInvitation).toHaveBeenCalledOnceWith('i-ops');
      expect(toasts()).toEqual(['Invitation to ops@example.com revoked.']);
      expect(screen.getByText('No invitations.')).toBeTruthy();
      await until(() => document.activeElement === screen.getByRole('heading', { name: 'Invitations' }));
    });
  });

  describe('the invite form', () => {
    async function openInvite(r: Awaited<ReturnType<typeof setup>>) {
      fireEvent.click(screen.getByRole('button', { name: 'Invite a Company Administrator' }));
      await settle(r);
      return screen.getByRole('dialog', { name: 'Invite a Company Administrator' });
    }

    async function send(r: Awaited<ReturnType<typeof setup>>, dlg: HTMLElement, email: string) {
      const field = within(dlg).getByLabelText('Email address (required)') as HTMLInputElement;
      fireEvent.input(field, { target: { value: email } });
      r.fixture.detectChanges();
      fireEvent.click(within(dlg).getByRole('button', { name: 'Send invitation' }));
      await settle(r);
      return field;
    }

    function errorFor(field: HTMLElement): string {
      return (field.getAttribute('aria-describedby') ?? '').split(/\s+/).map(id => document.getElementById(id))
        .filter(n => n?.classList.contains('field-error')).map(n => n!.textContent!.trim()).join(' ');
    }

    it('invites by email only, with no lookup, and shows the Pending row (US7-S1, 8.09.b, 8.09.c)', async () => {
      const r = await setup({ invitations: paged([]) });
      const dlg = await openInvite(r);
      const text = () => dlg.textContent!.replace(/\s+/g, ' ');
      expect(text()).toContain("A Company Administrator sets up the company's business in this app. The role gives no board powers in any community.");
      expect(text()).toContain('We always send an invitation, even if this email already has an account. They become a Company Administrator only if they accept.');
      expect(within(dlg).getByText('Until they accept, the invitation shows only this email address.')).toBeTruthy();
      expect(within(dlg).queryByRole('combobox')).toBeNull();
      const field = within(dlg).getByLabelText('Email address (required)') as HTMLInputElement;
      fireEvent.input(field, { target: { value: 'ops@example.com' } });
      r.fixture.detectChanges();
      expect(text()).toContain("We'll email ops@example.com an invitation to be a Company Administrator of the management company. The link works once, for 72 hours, and only for that email address.");

      company.inviteAdministrator.and.resolveTo({ id: 'i-ops', state: 'Pending', expiresAt: OPS.expiresAt });
      company.getInvitations.and.resolveTo(paged([OPS]));
      fireEvent.click(within(dlg).getByRole('button', { name: 'Send invitation' }));
      await settle(r);
      expect(company.inviteAdministrator).toHaveBeenCalledOnceWith('ops@example.com');
      expect(screen.queryByRole('dialog')).toBeNull();
      expect(toasts()).toEqual(['Invitation sent to ops@example.com.']);
      expect(rows(invitationsTable()).length).toBe(1);
      await until(() => document.activeElement === screen.getByRole('heading', { name: 'Invitations' }));
    });

    it('checks the email format first', async () => {
      const r = await setup();
      const dlg = await openInvite(r);
      const field = await send(r, dlg, 'ops@');
      expect(errorFor(field)).toBe('Enter a valid email address.');
      expect(company.inviteAdministrator).not.toHaveBeenCalled();
    });

    it('refuses your own email under the field (SELF_GRANT_NOT_ALLOWED, US7-S10, 8.09.d)', async () => {
      const r = await setup();
      const dlg = await openInvite(r);
      company.inviteAdministrator.and.rejectWith(httpError(422, { code: 'SELF_GRANT_NOT_ALLOWED', message: 'Self.' }));
      const field = await send(r, dlg, 'owner@example.com');
      expect(errorFor(field)).toBe("You can't invite yourself. Ask another Company Administrator.");
      expect(field.getAttribute('aria-invalid')).toBe('true');
      expect(field.value).toBe('owner@example.com');
    });

    it('refuses a current administrator (ALREADY_MEMBER)', async () => {
      const r = await setup();
      const dlg = await openInvite(r);
      company.inviteAdministrator.and.rejectWith(httpError(409, { code: 'ALREADY_MEMBER', message: 'Already.' }));
      const field = await send(r, dlg, 'casey@example.com');
      expect(errorFor(field)).toBe('casey@example.com is already a Company Administrator.');
    });

    it('offers "Resend invitation" for a pending one (INVITATION_PENDING, 8.09.e)', async () => {
      const r = await setup();
      const dlg = await openInvite(r);
      company.inviteAdministrator.and.rejectWith(httpError(409, { code: 'INVITATION_PENDING', message: 'Pending.', invitationId: 'i-ops' }));
      const field = await send(r, dlg, 'ops@example.com');
      expect(errorFor(field)).toBe('ops@example.com already has a pending Company Administrator invitation. Resend it instead?');
      expect(within(dlg).getByRole('button', { name: 'Send invitation' })).toBeTruthy();
      company.resendInvitation.and.resolveTo(OPS);
      fireEvent.click(within(dlg).getByRole('button', { name: 'Resend invitation' }));
      await settle(r);
      expect(company.resendInvitation).toHaveBeenCalledOnceWith('i-ops');
      expect(screen.queryByRole('dialog')).toBeNull();
      expect(toasts()).toEqual(['Invitation sent again to ops@example.com. The earlier link no longer works.']);
    });

    it('a server email problem shows under the field; FORBIDDEN shows at the top with "Close", which re-reads the page', async () => {
      const r = await setup();
      const dlg = await openInvite(r);
      company.inviteAdministrator.and.rejectWith(httpError(422, { code: 'VALIDATION_ERROR', message: 'Email can be at most 254 characters.' }));
      const field = await send(r, dlg, 'ops@example.com');
      expect(errorFor(field)).toBe('Email can be at most 254 characters.');
      company.inviteAdministrator.and.rejectWith(httpError(403, { code: 'FORBIDDEN', message: 'No.' }));
      await send(r, dlg, 'ops@example.com');
      expect(within(dlg).getByText("You don't have permission to do that.")).toBeTruthy();
      fireEvent.click(within(dlg).getByRole('button', { name: 'Close' }));
      await settle(r);
      expect(screen.queryByRole('dialog')).toBeNull();
      expect(company.getAdministrators).toHaveBeenCalledTimes(2);
    });

    it('says when it failed, or was rate limited; Cancel closes', async () => {
      const r = await setup();
      const dlg = await openInvite(r);
      company.inviteAdministrator.and.rejectWith(httpError(0));
      await send(r, dlg, 'ops@example.com');
      expect(within(dlg).getByText("Couldn't save. Check your connection and try again.")).toBeTruthy();
      company.inviteAdministrator.and.rejectWith(httpError(429));
      await send(r, dlg, 'ops@example.com');
      expect(within(dlg).getByText('Too many requests. Wait a few minutes, then try again.')).toBeTruthy();
      fireEvent.click(within(dlg).getByRole('button', { name: 'Cancel' }));
      await settle(r);
      expect(screen.queryByRole('dialog')).toBeNull();
    });
  });

  describe('revoking the role', () => {
    it("asks for confirmation before revoking another person's role (US7-S2, 8.09.g, 8.09.h)", async () => {
      const r = await setup();
      fireEvent.click(screen.getByRole('button', { name: "Revoke Casey Novak's Company Administrator role" }));
      await settle(r);
      const dlg = screen.getByRole('dialog', { name: "Revoke Casey Novak's Company Administrator role?" });
      const text = dlg.textContent!.replace(/\s+/g, ' ');
      expect(text).toContain('Casey Novak loses the Company Administrator role on their next request.');
      expect(text).toContain("After that, Casey Novak can't open the company area.");
      expect(text).toContain('The company still has a Company Administrator: Robin Hale (you).');
      expect(company.revokeAdministrator).not.toHaveBeenCalled();

      company.revokeAdministrator.and.resolveTo({ revoked: true, invitationsRevoked: 0 });
      company.getAdministrators.and.resolveTo(paged([ROBIN]));
      fireEvent.click(within(dlg).getByRole('button', { name: 'Revoke role' }));
      await settle(r);
      expect(company.revokeAdministrator).toHaveBeenCalledOnceWith('u-casey');
      expect(toasts()).toEqual(['Casey Novak is no longer a Company Administrator.']);
      expect(rows(adminsTable()).length).toBe(1);
      await until(() => document.activeElement === screen.getByRole('heading', { name: 'Current Company Administrators' }));
    });

    it("revoking yourself warns that you'll lose access, then takes you to your landing (8.09 'Revoke your own role')", async () => {
      const r = await setup();
      fireEvent.click(screen.getByRole('button', { name: 'Revoke your own Company Administrator role' }));
      await settle(r);
      const dlg = screen.getByRole('dialog', { name: 'Revoke your own Company Administrator role?' });
      const text = dlg.textContent!.replace(/\s+/g, ' ');
      expect(text).toContain('You lose the Company Administrator role on your next request.');
      expect(text).toContain("After that, you can't open the company area.");
      expect(text).toContain("Your account isn't deleted.");
      expect(text).toContain('The company still has a Company Administrator: Casey Novak.');

      company.revokeAdministrator.and.resolveTo({ revoked: true, invitationsRevoked: 1 });
      fireEvent.click(within(dlg).getByRole('button', { name: 'Revoke my role' }));
      await settle(r);
      expect(company.revokeAdministrator).toHaveBeenCalledOnceWith('u-robin');
      expect(refreshCurrentUser).toHaveBeenCalled();
      expect(TestBed.inject(Router).url).toBe('/app/no-home');
      expect(toasts()).toEqual(["You're no longer a Company Administrator."]);
    });

    it("the last administrator's Revoke is disabled, with the LAST_COMPANY_ADMIN reason (US7-S3, D1)", async () => {
      await setup({ admins: paged([ROBIN]) });
      const revoke = screen.getByRole('button', { name: 'Revoke your own Company Administrator role' });
      expect(revoke.getAttribute('aria-disabled')).toBe('true');
      const tip = document.getElementById(revoke.getAttribute('aria-describedby')!.split(' ').pop()!)!;
      expect(tip.textContent!.trim()).toBe("You're the only Company Administrator, so your role can't be revoked. Invite another Company Administrator first. Once they accept, your role can be revoked.");
      fireEvent.click(revoke);
      expect(screen.queryByRole('dialog')).toBeNull();
    });

    it('a race refused with LAST_COMPANY_ADMIN shows at the top with "Close", which reloads the page (US7-S4, 8.09.j)', async () => {
      const r = await setup({ user: me('u-casey') });
      fireEvent.click(screen.getByRole('button', { name: "Revoke Robin Hale's Company Administrator role" }));
      await settle(r);
      const dlg = screen.getByRole('dialog', { name: "Revoke Robin Hale's Company Administrator role?" });
      company.revokeAdministrator.and.rejectWith(httpError(422, { code: 'LAST_COMPANY_ADMIN', message: 'Last.' }));
      fireEvent.click(within(dlg).getByRole('button', { name: 'Revoke role' }));
      await settle(r);
      expect(within(dlg).getByText("Robin Hale is now the only Company Administrator, so the role can't be revoked.")).toBeTruthy();
      expect(within(dlg).queryByRole('button', { name: 'Revoke role' })).toBeNull();
      fireEvent.click(within(dlg).getByRole('button', { name: 'Close' }));
      await settle(r);
      expect(screen.queryByRole('dialog')).toBeNull();
      expect(company.getAdministrators).toHaveBeenCalledTimes(2);
    });

    it('your own revoke refused as the last one says so (US7-S3, 8.09.i)', async () => {
      const r = await setup();
      fireEvent.click(screen.getByRole('button', { name: 'Revoke your own Company Administrator role' }));
      await settle(r);
      const dlg = screen.getByRole('dialog', { name: 'Revoke your own Company Administrator role?' });
      company.revokeAdministrator.and.rejectWith(httpError(422, { code: 'LAST_COMPANY_ADMIN', message: 'Last.' }));
      fireEvent.click(within(dlg).getByRole('button', { name: 'Revoke my role' }));
      await settle(r);
      expect(within(dlg).getByText("You're the only Company Administrator, so your role can't be revoked. Invite another Company Administrator first. Once they accept, your role can be revoked.")).toBeTruthy();
      expect(within(dlg).getByRole('button', { name: 'Close' })).toBeTruthy();
    });

    it('says when revoking failed for a network reason; FORBIDDEN shows at the top', async () => {
      const r = await setup();
      fireEvent.click(screen.getByRole('button', { name: "Revoke Casey Novak's Company Administrator role" }));
      await settle(r);
      const dlg = screen.getByRole('dialog');
      company.revokeAdministrator.and.rejectWith(httpError(0));
      fireEvent.click(within(dlg).getByRole('button', { name: 'Revoke role' }));
      await settle(r);
      expect(within(dlg).getByText("Couldn't save. Check your connection and try again.")).toBeTruthy();
      company.revokeAdministrator.and.rejectWith(httpError(403, { code: 'FORBIDDEN', message: 'No.' }));
      fireEvent.click(within(dlg).getByRole('button', { name: 'Revoke role' }));
      await settle(r);
      expect(within(dlg).getByText("You don't have permission to do that.")).toBeTruthy();
      fireEvent.click(within(dlg).getByRole('button', { name: 'Close' }));
      await settle(r);
      expect(screen.queryByRole('dialog')).toBeNull();
      expect(company.getAdministrators).toHaveBeenCalledTimes(2);
    });
  });
});
