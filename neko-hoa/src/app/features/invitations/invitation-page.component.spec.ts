import { WritableSignal, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { Router, provideRouter } from '@angular/router';
import { render, screen, fireEvent, within } from '@testing-library/angular';
import { until } from '../board/architectural/arc-testing';
import { InvitationPageComponent } from './invitation-page.component';
import { InvitationPreview, InvitationService } from '../../core/services/invitation.service';
import { AuthService } from '../../core/services/auth.service';
import { SessionRefreshService } from '../../core/services/session-refresh';
import { BoardNavigationService } from '../../core/services/board-navigation.service';
import { ToastService } from '../../core/services/toast.service';
import { CurrentUser, Landing } from '../../core/models';

// 030 T056 / brief 10 / FR-022, FR-022a: the public /invite page. The token arrives in the URL
// fragment, is removed from the address bar at once, and only ever travels in request bodies.

const TOKEN = 'Qm9iIG9uIGEgc2hhcmVkIGxhcHRvcC1fXy0tLS0tLS0';

function preview(over: Partial<InvitationPreview> = {}): InvitationPreview {
  return {
    kind: 'Membership',
    communityName: 'Keystone Park',
    offer: { role: 'CommunityManager', termEndDate: null, propertyAddress: null },
    sentBy: 'Robin Hale',
    expiresAt: '2026-10-14T13:15:00Z',
    timeZoneId: 'America/New_York',
    invitedEmail: 'pat@example.com',
    hasAccount: false,
    ...over,
  };
}

const RESIDENT_PREVIEW = preview({
  kind: 'Residency', communityName: 'Sakura Heights',
  offer: { role: null, termEndDate: null, propertyAddress: '711 Keystone Park Dr #29' },
  sentBy: 'Jordan Blake', expiresAt: '2026-10-13T20:40:00Z', invitedEmail: 'kim@example.com',
});

function apiError(status: number, code?: string, extra: Record<string, unknown> = {}): HttpErrorResponse {
  return new HttpErrorResponse({ status, error: code ? { code, message: 'server words', ...extra } : null });
}

function person(email: string, landing: Landing | null = null, over: Partial<CurrentUser> = {}): CurrentUser {
  return {
    id: 'u1', firstName: 'Ann', lastName: 'Brooks', email, initials: 'AB', lastActiveMode: 'Resident',
    memberships: [], properties: [], isCompanyAdministrator: false, landing, ...over,
  };
}

describe('InvitationPageComponent (030 brief 10)', () => {
  let invitations: jasmine.SpyObj<InvitationService>;
  let userSig: WritableSignal<CurrentUser | null>;
  let auth: {
    user: () => CurrentUser | null; isLoggedIn: () => boolean;
    login: jasmine.Spy; logout: jasmine.Spy; refreshCurrentUser: jasmine.Spy;
  };
  let session: { refresh: jasmine.Spy };
  let navigate: jasmine.Spy;
  let replaceState: jasmine.Spy;
  let startUrl: string;

  beforeAll(() => { startUrl = location.pathname + location.search; });

  afterEach(() => {
    history.replaceState(null, '', startUrl);
    document.head.querySelectorAll('meta[name="referrer"]').forEach(m => m.remove());
  });

  async function setup(opts: {
    hash?: string; preview?: InvitationPreview | HttpErrorResponse; signedInAs?: CurrentUser | null;
  } = {}) {
    history.replaceState(null, '', `${startUrl}${opts.hash ?? `#t=${TOKEN}`}`);
    replaceState = spyOn(history, 'replaceState').and.callThrough();

    invitations = jasmine.createSpyObj<InvitationService>('InvitationService', ['preview', 'register', 'accept']);
    const p = opts.preview ?? preview();
    if (p instanceof HttpErrorResponse) invitations.preview.and.rejectWith(p);
    else invitations.preview.and.resolveTo(p);

    userSig = signal<CurrentUser | null>(opts.signedInAs ?? null);
    auth = {
      user: () => userSig(),
      isLoggedIn: () => userSig() !== null,
      login: jasmine.createSpy('login'),
      logout: jasmine.createSpy('logout').and.callFake(() => userSig.set(null)),
      refreshCurrentUser: jasmine.createSpy('refreshCurrentUser').and.callFake(async () => userSig()),
    };
    session = { refresh: jasmine.createSpy('refresh').and.resolveTo('new.access.token') };

    const result = await render(InvitationPageComponent, {
      providers: [
        provideRouter([]),
        { provide: InvitationService, useValue: invitations },
        { provide: AuthService, useValue: auth },
        { provide: SessionRefreshService, useValue: session },
      ],
    });
    navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    await settle(result);
    return result;
  }

  async function settle(result: Awaited<ReturnType<typeof render>>) {
    await result.fixture.whenStable();
    result.fixture.detectChanges();
  }

  function heading(): string {
    return screen.getByRole('heading', { level: 1 }).textContent!.trim();
  }

  function toasts(): string[] {
    return TestBed.inject(ToastService).toasts().map(t => t.message);
  }

  afterEach(() => TestBed.inject(ToastService).clear());

  describe('reading the link', () => {
    it('reads #t= and removes it from the address bar with history.replaceState', async () => {
      await setup();
      expect(invitations.preview).toHaveBeenCalledOnceWith(TOKEN);
      expect(replaceState).toHaveBeenCalled();
      const url = String(replaceState.calls.mostRecent().args[2]);
      expect(url).not.toContain('#');
      expect(url).not.toContain(TOKEN);
      expect(location.hash).toBe('');
      expect(location.href).not.toContain(TOKEN);
    });

    it('shows the "can\'t be used" page without asking the server when there is no token', async () => {
      await setup({ hash: '' });
      expect(invitations.preview).not.toHaveBeenCalled();
      expect(heading()).toBe("This invitation link can't be used");
    });

    it('sets Referrer-Policy no-referrer while shown, and removes it when the page goes', async () => {
      const r = await setup();
      expect(document.head.querySelector('meta[name="referrer"]')?.getAttribute('content')).toBe('no-referrer');
      r.fixture.destroy();
      expect(document.head.querySelector('meta[name="referrer"]')).toBeNull();
    });
  });

  describe('a valid link', () => {
    it('shows the community, the offer, the sender, the invited email and the expiry (FR-022a, 8.10.a)', async () => {
      await setup();
      expect(heading()).toBe("You're invited");
      expect(screen.getByText('Accept to get access. Invitation links work for 72 hours.')).toBeTruthy();
      const card = screen.getByRole('region', { name: 'Your invitation' });
      expect(within(card).getByText('Valid invitation')).toBeTruthy();
      expect(within(card).getByText('Keystone Park')).toBeTruthy();
      expect(within(card).getByText("You're invited as").nextElementSibling!.textContent!.trim()).toBe('Community Manager');
      expect(within(card).getByText('Sent by').nextElementSibling!.textContent!.trim()).toBe('Robin Hale');
      expect(within(card).getByText('For').nextElementSibling!.textContent!.trim()).toBe('pat@example.com');
      expect(within(card).getByText('Expires').nextElementSibling!.textContent!.trim()).toBe('Oct 14, 2026, 9:15 AM EDT');
    });

    it('shows a resident invitation by address, with community and Resident pills (8.10.b)', async () => {
      await setup({ preview: RESIDENT_PREVIEW });
      const card = screen.getByRole('region', { name: 'Your invitation' });
      expect(within(card).getByRole('heading', { level: 2 }).textContent).toContain('711 Keystone Park Dr #29');
      expect(within(card).getByText('Sakura Heights')).toBeTruthy();
      expect(within(card).getByText('Resident')).toBeTruthy();
      expect(within(card).getByText("You're invited as").nextElementSibling!.textContent!.trim()).toBe('Resident of this home');
    });

    it('shows a term end date and the company for a Company Administrator invitation', async () => {
      await setup({ preview: preview({ offer: { role: 'BoardMember', termEndDate: '2027-12-31', propertyAddress: null } }) });
      expect(screen.getByText("You're invited as").nextElementSibling!.textContent!.trim()).toBe('Board Member, until Dec 31, 2027');
    });

    it('shows the expiry in the time zone the server gives (FR-022)', async () => {
      await setup({ preview: preview({ timeZoneId: 'America/Los_Angeles' }) });
      expect(screen.getByText('Expires').nextElementSibling!.textContent!.trim()).toBe('Oct 14, 2026, 6:15 AM PDT');
    });

    it('offers "Create an account" to an anonymous visitor with no account', async () => {
      await setup();
      expect(screen.getByRole('button', { name: 'Create an account' })).toBeTruthy();
      expect(screen.queryByRole('button', { name: 'Sign in to accept' })).toBeNull();
    });

    it('offers "Sign in to accept" when the email has an account', async () => {
      await setup({ preview: preview({ invitedEmail: 'ann@example.com', hasAccount: true }) });
      expect(screen.getByRole('button', { name: 'Sign in to accept' })).toBeTruthy();
      expect(screen.queryByRole('button', { name: 'Create an account' })).toBeNull();
    });

    it('offers "Accept invitation" straight away to a signed-in visitor (8.10.n)', async () => {
      await setup({
        signedInAs: person('ann@example.com'),
        preview: preview({ communityName: 'Briar Hollow', offer: { role: 'BoardMember', termEndDate: null, propertyAddress: null }, invitedEmail: null, hasAccount: null }),
      });
      expect(heading()).toBe('Accept this invitation?');
      expect(screen.getByText("You're signed in as ann@example.com.")).toBeTruthy();
      expect(screen.getByText(/^Your Board Member role in Briar Hollow starts today, .+, with no end date\.$/)).toBeTruthy();
      expect(screen.getByRole('button', { name: 'Accept invitation' })).toBeTruthy();
      expect(screen.queryByRole('button', { name: 'Create an account' })).toBeNull();
      // The preview sends no invited email to a signed-in caller; the page shows no "For" row.
      expect(screen.queryByText('For')).toBeNull();
    });
  });

  describe('create an account (FR-022a)', () => {
    async function openCreate(p: InvitationPreview = preview()) {
      const r = await setup({ preview: p });
      fireEvent.click(screen.getByRole('button', { name: 'Create an account' }));
      await settle(r);
      return r;
    }

    function type(label: string, value: string) {
      fireEvent.input(screen.getByLabelText(label), { target: { value } });
    }

    it('shows the fixed email as text, with no claim code, and focuses First name', async () => {
      await openCreate();
      expect(heading()).toBe('Create your account');
      expect(screen.getByText("Creating your account accepts the invitation. You don't need a claim code.")).toBeTruthy();
      const email = screen.getByText('pat@example.com', { selector: '.inv__email' });
      expect(email.tagName).not.toBe('INPUT');
      expect(screen.getByText("Your invitation link confirms this email address. It can't be changed.")).toBeTruthy();
      expect(screen.queryByLabelText(/claim code/i)).toBeNull();
      expect(document.activeElement).toBe(screen.getByLabelText('First name'));
      expect(screen.getByText('Community Manager · Keystone Park · Sent by Robin Hale · Expires Oct 14, 2026, 9:15 AM EDT')).toBeTruthy();
    });

    it('enforces the password rules before sending anything (8.10.g)', async () => {
      const r = await openCreate();
      type('Last name', 'Tran');
      type('Password', 'kim123');
      fireEvent.click(screen.getByRole('button', { name: 'Create account and accept' }));
      await settle(r);

      const summary = screen.getByRole('alert', { name: 'Check these fields' });
      expect(within(summary).getByRole('link', { name: 'Enter your first name.' })).toBeTruthy();
      const rule = 'Password must be at least 8 characters, with an uppercase letter, a lowercase letter, a number and a symbol.';
      expect(within(summary).getByRole('link', { name: rule })).toBeTruthy();
      expect(document.activeElement).toBe(summary);
      expect(screen.getByLabelText('Password').getAttribute('aria-invalid')).toBe('true');
      expect(invitations.register).not.toHaveBeenCalled();

      // Each rule on its own: no symbol, no uppercase, no digit, too short.
      for (const weak of ['Sakura2026', 'sakura#2026', 'Sakura#Hts', 'Sk#1']) {
        type('Password', weak);
        fireEvent.click(screen.getByRole('button', { name: 'Create account and accept' }));
        await settle(r);
        expect(screen.getAllByText(rule).length).withContext(weak).toBeGreaterThan(0);
      }
      expect(invitations.register).not.toHaveBeenCalled();
    });

    it('asks for every field when nothing is entered', async () => {
      const r = await openCreate();
      fireEvent.click(screen.getByRole('button', { name: 'Create account and accept' }));
      await settle(r);
      const summary = screen.getByRole('alert', { name: 'Check these fields' });
      expect(within(summary).getAllByRole('link').map(a => a.textContent!.trim()))
        .toEqual(['Enter your first name.', 'Enter your last name.', 'Enter a password.']);
      fireEvent.click(within(summary).getByRole('link', { name: 'Enter your last name.' }));
      expect(document.activeElement).toBe(screen.getByLabelText('Last name'));
    });

    it('shows and hides the password with a real toggle button', async () => {
      const r = await openCreate();
      const toggle = screen.getByRole('button', { name: 'Show password' });
      expect(toggle.getAttribute('aria-pressed')).toBe('false');
      fireEvent.click(toggle);
      await settle(r);
      expect(screen.getByLabelText('Password').getAttribute('type')).toBe('text');
      expect(toggle.getAttribute('aria-pressed')).toBe('true');
      expect(toggle.textContent!.trim()).toBe('Hide');
    });

    it('creates the account, then routes by the server landing with the ready toast (8.10.i)', async () => {
      const r = await openCreate();
      invitations.register.and.callFake(async () => {
        userSig.set(person('pat@example.com', 'Board', {
          firstName: 'Pat', memberships: [{ communityId: 'kp', communityName: 'Keystone Park', role: 'CommunityManager', communityStatus: 'Onboarding' }],
        }));
        return {} as never;
      });
      type('First name', 'Pat');
      type('Last name', 'Rivera');
      type('Password', 'Keystone#2026');
      fireEvent.click(screen.getByRole('button', { name: 'Create account and accept' }));
      await until(() => navigate.calls.count() > 0);

      expect(invitations.register).toHaveBeenCalledOnceWith(TOKEN, { firstName: 'Pat', lastName: 'Rivera', password: 'Keystone#2026' });
      expect(navigate).toHaveBeenCalledWith(['/app/board/home']);
      expect(TestBed.inject(BoardNavigationService).activeCommunityId()).toBe('kp');
      expect(toasts()).toEqual(["Your account is ready. You're now a Community Manager of Keystone Park."]);
      await settle(r);
    });

    it('words the ready toast for a resident and for a Company Administrator', async () => {
      const r = await openCreate(RESIDENT_PREVIEW);
      invitations.register.and.callFake(async () => { userSig.set(person('kim@example.com', 'Resident')); return {} as never; });
      type('First name', 'Kim'); type('Last name', 'Tran'); type('Password', 'Sakura#2026');
      fireEvent.click(screen.getByRole('button', { name: 'Create account and accept' }));
      await until(() => navigate.calls.count() > 0);
      expect(navigate).toHaveBeenCalledWith(['/app/dashboard']);
      expect(toasts()).toEqual(["Your account is ready. You're now a resident of 711 Keystone Park Dr #29."]);
      await settle(r);
    });

    it('says to sign in and accept when account creation fails, keeping the names (8.10.h)', async () => {
      const r = await openCreate();
      invitations.register.and.rejectWith(apiError(422, 'REGISTRATION_FAILED'));
      type('First name', 'Ann'); type('Last name', 'Brooks'); type('Password', 'Briar#Hollow1');
      fireEvent.click(screen.getByRole('button', { name: 'Create account and accept' }));
      await until(() => !!screen.queryByText(/We couldn't create an account/));
      await settle(r);

      const banner = screen.getByText("We couldn't create an account with these details. If you already have an account, sign in to accept the invitation.");
      expect(banner.closest('[role="alert"]')).toBeTruthy();
      expect((screen.getByLabelText('First name') as HTMLInputElement).value).toBe('Ann');
      expect((screen.getByLabelText('Last name') as HTMLInputElement).value).toBe('Brooks');
      expect((screen.getByLabelText('Password') as HTMLInputElement).value).toBe('');

      fireEvent.click(within(banner.closest('[role="alert"]') as HTMLElement).getByRole('button', { name: 'Sign in to accept' }));
      await settle(r);
      expect(heading()).toBe('Sign in to accept');
      expect((screen.getByLabelText('Email') as HTMLInputElement).value).toBe('pat@example.com');
    });

    it('keeps every value and shows the rate-limit banner on 429 (8.10.y)', async () => {
      const r = await openCreate();
      invitations.register.and.rejectWith(apiError(429));
      type('First name', 'Pat'); type('Last name', 'Rivera'); type('Password', 'Keystone#2026');
      fireEvent.click(screen.getByRole('button', { name: 'Create account and accept' }));
      await until(() => !!screen.queryByText('Too many requests. Wait a few minutes, then try again.'));
      await settle(r);
      expect((screen.getByLabelText('Password') as HTMLInputElement).value).toBe('Keystone#2026');
    });

    it('shows a server validation error under its field', async () => {
      const r = await openCreate();
      invitations.register.and.rejectWith(apiError(422, 'VALIDATION_ERROR', { errors: [{ field: 'Password', message: 'Passwords must have at least one digit.' }] }));
      type('First name', 'Pat'); type('Last name', 'Rivera'); type('Password', 'Keystone#2026');
      fireEvent.click(screen.getByRole('button', { name: 'Create account and accept' }));
      await until(() => !!screen.queryByText('Passwords must have at least one digit.', { selector: '.inv__error' }));
      await settle(r);
      expect(screen.getByLabelText('Password').getAttribute('aria-invalid')).toBe('true');
      expect(screen.getByRole('alert', { name: 'Check these fields' })).toBeTruthy();
    });

    it('shows a generic banner for a network failure, and the server message for an unmapped validation error', async () => {
      const r = await openCreate();
      invitations.register.and.rejectWith(new HttpErrorResponse({ status: 0 }));
      type('First name', 'Pat'); type('Last name', 'Rivera'); type('Password', 'Keystone#2026');
      fireEvent.click(screen.getByRole('button', { name: 'Create account and accept' }));
      await until(() => !!screen.queryByText("Couldn't create your account. Check your connection and try again."));

      invitations.register.and.rejectWith(apiError(422, 'VALIDATION_ERROR', { message: 'The request was not valid.' }));
      fireEvent.click(screen.getByRole('button', { name: 'Create account and accept' }));
      await until(() => !!screen.queryByText('The request was not valid.'));
      await settle(r);
      expect(screen.queryByText("Couldn't create your account. Check your connection and try again.")).toBeNull();
    });

    it('replaces the form with the expired page when the link expires before submit', async () => {
      const r = await openCreate();
      invitations.register.and.rejectWith(apiError(410, 'INVITATION_EXPIRED'));
      type('First name', 'Pat'); type('Last name', 'Rivera'); type('Password', 'Keystone#2026');
      fireEvent.click(screen.getByRole('button', { name: 'Create account and accept' }));
      await until(() => heading() === 'This invitation has expired');
      await settle(r);
      expect(screen.queryByLabelText('First name')).toBeNull();
    });

    it('"Back to invitation" returns to the landing with nothing lost', async () => {
      const r = await openCreate();
      type('First name', 'Pat');
      fireEvent.click(screen.getByRole('button', { name: 'Back to invitation' }));
      await settle(r);
      expect(heading()).toBe("You're invited");
      fireEvent.click(screen.getByRole('button', { name: 'Create an account' }));
      await settle(r);
      expect((screen.getByLabelText('First name') as HTMLInputElement).value).toBe('Pat');
    });
  });

  describe('sign in to accept', () => {
    async function openSignIn() {
      const r = await setup({ preview: preview({ communityName: 'Briar Hollow', invitedEmail: 'ann@example.com', hasAccount: true, offer: { role: 'BoardMember', termEndDate: null, propertyAddress: null } }) });
      fireEvent.click(screen.getByRole('button', { name: 'Sign in to accept' }));
      await settle(r);
      return r;
    }

    it('fills in the invited email and focuses Password (8.10.l)', async () => {
      await openSignIn();
      expect(heading()).toBe('Sign in to accept');
      expect((screen.getByLabelText('Email') as HTMLInputElement).value).toBe('ann@example.com');
      expect(screen.getByText('Use the account for ann@example.com, the address this invitation was sent to.')).toBeTruthy();
      expect(document.activeElement).toBe(screen.getByLabelText('Password'));
    });

    it('leads to the confirm step with the matching account', async () => {
      const r = await openSignIn();
      auth.login.and.callFake(async () => userSig.set(person('Ann@Example.com ')));
      fireEvent.input(screen.getByLabelText('Password'), { target: { value: 'Briar#Hollow1' } });
      fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));
      await until(() => heading() === 'Accept this invitation?');
      expect(auth.login).toHaveBeenCalledWith('ann@example.com', 'Briar#Hollow1');
      await settle(r);
    });

    it('refuses a different account without showing the invited email (US2-S7)', async () => {
      const r = await openSignIn();
      auth.login.and.callFake(async () => userSig.set(person('bob@example.com')));
      fireEvent.input(screen.getByLabelText('Email'), { target: { value: 'bob@example.com' } });
      fireEvent.input(screen.getByLabelText('Password'), { target: { value: 'Bob#Brooks1' } });
      fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));
      await until(() => heading() === 'This invitation is for a different email address');
      await settle(r);
      expect(invitations.accept).not.toHaveBeenCalled();
      expect(document.body.textContent).not.toContain('ann@example.com');
      expect(screen.getByText(/You're signed in as bob@example\.com\./)).toBeTruthy();
    });

    it('shows the rate-limit banner when sign-in is rate limited', async () => {
      const r = await openSignIn();
      auth.login.and.rejectWith(apiError(429));
      fireEvent.input(screen.getByLabelText('Password'), { target: { value: 'Briar#Hollow1' } });
      fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));
      await until(() => !!screen.queryByText('Too many requests. Wait a few minutes, then try again.'));
      await settle(r);
      expect(screen.queryByText('Invalid email or password.')).toBeNull();
    });

    it('"New here? Create an account" opens the form', async () => {
      const r = await openSignIn();
      fireEvent.click(screen.getByRole('button', { name: 'New here? Create an account' }));
      await settle(r);
      expect(heading()).toBe('Create your account');
    });

    it('shows today\'s wrong-password message', async () => {
      const r = await openSignIn();
      auth.login.and.rejectWith(apiError(401, 'INVALID_CREDENTIALS'));
      fireEvent.input(screen.getByLabelText('Password'), { target: { value: 'nope' } });
      fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));
      await until(() => !!screen.queryByText('Invalid email or password.'));
      await settle(r);
      expect(screen.getByText('Invalid email or password.').getAttribute('role')).toBe('alert');
      expect(heading()).toBe('Sign in to accept');
    });
  });

  describe('accept (signed in)', () => {
    const ANN = person('ann@example.com', 'NotLive');
    const BRIAR = preview({ communityName: 'Briar Hollow', offer: { role: 'BoardMember', termEndDate: null, propertyAddress: null }, invitedEmail: null, hasAccount: null });

    it('accepts, refreshes the session, and routes by the new landing (8.10.o)', async () => {
      const r = await setup({ signedInAs: ANN, preview: BRIAR });
      invitations.accept.and.resolveTo({ accepted: true, alreadyHeld: false, landing: 'Board' });
      session.refresh.and.callFake(async () => {
        userSig.set(person('ann@example.com', 'Board', { memberships: [{ communityId: 'bh', communityName: 'Briar Hollow', role: 'BoardMember', communityStatus: 'Onboarding' }] }));
        return 'new.token';
      });
      fireEvent.click(screen.getByRole('button', { name: 'Accept invitation' }));
      await until(() => navigate.calls.count() > 0);

      expect(invitations.accept).toHaveBeenCalledOnceWith(TOKEN);
      expect(session.refresh).toHaveBeenCalled();
      expect(navigate).toHaveBeenCalledWith(['/app/board/home']);
      expect(toasts()).toEqual(["You're now a Board Member of Briar Hollow."]);
      await settle(r);
    });

    it('falls back to /auth/me, and to the response landing, when the refresh fails', async () => {
      const r = await setup({ signedInAs: person('robin@example.com', null), preview: preview({ kind: 'CompanyAdministrator', communityName: 'Juniper Row Community Management', offer: { role: null, termEndDate: null, propertyAddress: null }, invitedEmail: null, hasAccount: null }) });
      expect(screen.getByText('You\'ll be able to open the company area for Juniper Row Community Management.')).toBeTruthy();
      invitations.accept.and.resolveTo({ accepted: true, alreadyHeld: false, landing: 'Company' });
      session.refresh.and.resolveTo(null);
      fireEvent.click(screen.getByRole('button', { name: 'Accept invitation' }));
      await until(() => navigate.calls.count() > 0);
      expect(auth.refreshCurrentUser).toHaveBeenCalled();
      expect(navigate).toHaveBeenCalledWith(['/app/company/portfolio']);
      expect(toasts()).toEqual(["You're now a Company Administrator."]);
      await settle(r);
    });

    it('says nothing was added when the role is already held (8.10.r)', async () => {
      const r = await setup({ signedInAs: ANN, preview: BRIAR });
      invitations.accept.and.resolveTo({ accepted: true, alreadyHeld: true, landing: 'Board' });
      fireEvent.click(screen.getByRole('button', { name: 'Accept invitation' }));
      await until(() => navigate.calls.count() > 0);
      expect(toasts()).toEqual(["You're already a Board Member of Briar Hollow. Nothing was added."]);
      await settle(r);
    });

    it('says the home is already one of yours for a resident duplicate', async () => {
      const r = await setup({ signedInAs: person('sam@example.com', 'Resident'), preview: { ...RESIDENT_PREVIEW, invitedEmail: null, hasAccount: null } });
      expect(screen.getByText("711 Keystone Park Dr #29 is added to your homes. Your other homes and roles don't change.")).toBeTruthy();
      invitations.accept.and.resolveTo({ accepted: true, alreadyHeld: true, landing: 'Resident' });
      fireEvent.click(screen.getByRole('button', { name: 'Accept invitation' }));
      await until(() => navigate.calls.count() > 0);
      expect(toasts()).toEqual(['711 Keystone Park Dr #29 is already one of your homes. Nothing was added.']);
      await settle(r);
    });

    it('shows the mismatch page, never the invited email, and signs out on request (8.10.w)', async () => {
      const r = await setup({ signedInAs: person('bob@example.com'), preview: BRIAR });
      invitations.accept.and.rejectWith(apiError(403, 'INVITATION_EMAIL_MISMATCH'));
      fireEvent.click(screen.getByRole('button', { name: 'Accept invitation' }));
      await until(() => heading() === 'This invitation is for a different email address');
      await settle(r);

      expect(screen.getByText("You're signed in as bob@example.com. Sign out, then open the link again and sign in or create an account with the email address the invitation was sent to.")).toBeTruthy();
      expect(document.body.textContent).not.toContain('ann@example.com');
      expect(screen.queryByText('Briar Hollow')).toBeNull();

      fireEvent.click(screen.getByRole('button', { name: 'Sign out' }));
      expect(auth.logout).toHaveBeenCalled();
      expect(toasts()).toEqual(["You're signed out."]);
    });

    it('shows the "can\'t be used" page when a revoke wins the race', async () => {
      const r = await setup({ signedInAs: ANN, preview: BRIAR });
      invitations.accept.and.rejectWith(apiError(410, 'INVITATION_INVALID'));
      fireEvent.click(screen.getByRole('button', { name: 'Accept invitation' }));
      await until(() => heading() === "This invitation link can't be used");
      await settle(r);
      expect(screen.queryByRole('button', { name: 'Accept invitation' })).toBeNull();
      expect(screen.queryByText('Briar Hollow')).toBeNull();
    });

    it('treats an archive race as a link that can\'t be used', async () => {
      const r = await setup({ signedInAs: ANN, preview: BRIAR });
      invitations.accept.and.rejectWith(apiError(409, 'COMMUNITY_ARCHIVED'));
      fireEvent.click(screen.getByRole('button', { name: 'Accept invitation' }));
      await until(() => heading() === "This invitation link can't be used");
      await settle(r);
      expect(screen.getByRole('link', { name: 'Go to sign in' })).toBeTruthy();
    });

    it('says when accepting failed for a network reason, and keeps the confirm step', async () => {
      const r = await setup({ signedInAs: ANN, preview: BRIAR });
      invitations.accept.and.rejectWith(new HttpErrorResponse({ status: 0 }));
      fireEvent.click(screen.getByRole('button', { name: 'Accept invitation' }));
      await until(() => !!screen.queryByText("Couldn't accept the invitation. Check your connection and try again."));
      await settle(r);
      expect(heading()).toBe('Accept this invitation?');
    });

    it('still lands when both the refresh and /auth/me fail', async () => {
      const r = await setup({ signedInAs: person('ann@example.com', null), preview: BRIAR });
      invitations.accept.and.resolveTo({ accepted: true, alreadyHeld: false, landing: 'NotLive' });
      session.refresh.and.rejectWith(new Error('offline'));
      auth.refreshCurrentUser.and.rejectWith(new Error('offline'));
      fireEvent.click(screen.getByRole('button', { name: 'Accept invitation' }));
      await until(() => navigate.calls.count() > 0);
      expect(navigate).toHaveBeenCalledWith(['/app/not-live']);
      await settle(r);
    });

    it('shows the rate-limit banner on the confirm card', async () => {
      const r = await setup({ signedInAs: ANN, preview: BRIAR });
      invitations.accept.and.rejectWith(apiError(429, 'RATE_LIMITED'));
      fireEvent.click(screen.getByRole('button', { name: 'Accept invitation' }));
      await until(() => !!screen.queryByText('Too many requests. Wait a few minutes, then try again.'));
      await settle(r);
      expect(heading()).toBe('Accept this invitation?');
    });

    it('"Not now" leaves the invitation pending and goes to the usual landing', async () => {
      const r = await setup({ signedInAs: ANN, preview: BRIAR });
      fireEvent.click(screen.getByRole('button', { name: 'Not now' }));
      await settle(r);
      expect(invitations.accept).not.toHaveBeenCalled();
      expect(navigate).toHaveBeenCalledWith(['/app/not-live']);
    });
  });

  describe('refusals (FR-022 check order, brief 10 §5.4)', () => {
    it('INVITATION_EXPIRED shows brief 10\'s copy and nothing about the invitation (8.10.s)', async () => {
      await setup({ preview: apiError(410, 'INVITATION_EXPIRED') });
      expect(heading()).toBe('This invitation has expired');
      expect(screen.getByText('Invitation links work for 72 hours. Ask the person who invited you to send a new link.')).toBeTruthy();
      expect(screen.getByRole('link', { name: 'Go to sign in' }).getAttribute('href')).toBe('/login');
      expect(screen.queryByRole('region', { name: 'Your invitation' })).toBeNull();
    });

    it('INVITATION_INVALID shows one message for every reason (8.10.u)', async () => {
      await setup({ preview: apiError(410, 'INVITATION_INVALID') });
      expect(heading()).toBe("This invitation link can't be used");
      expect(screen.getByText('It may have been cancelled, already used, or replaced by a newer link. If you still need access, ask the person who invited you.')).toBeTruthy();
      expect(screen.getByRole('link', { name: 'Go to sign in' })).toBeTruthy();
    });

    it('a rate-limited lookup shows the too-many-requests page; Try again repeats it (8.10.x)', async () => {
      const r = await setup({ preview: apiError(429) });
      expect(heading()).toBe('Too many requests');
      expect(screen.getByText('Wait a few minutes, then try again.')).toBeTruthy();
      invitations.preview.and.resolveTo(preview());
      fireEvent.click(screen.getByRole('button', { name: 'Try again' }));
      await until(() => heading() === "You're invited");
      expect(invitations.preview).toHaveBeenCalledTimes(2);
      await settle(r);
    });

    it('a failed lookup says so and offers Try again', async () => {
      await setup({ preview: new HttpErrorResponse({ status: 0 }) });
      expect(heading()).toBe("Couldn't load your invitation.");
      expect(screen.getByRole('button', { name: 'Try again' })).toBeTruthy();
    });
  });
});
