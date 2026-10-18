import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController, TestRequest } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { InvitationService, InvitationPreview } from './invitation.service';
import { AuthService } from './auth.service';
import { environment } from '../../../environments/environment';

const BASE = environment.apiBaseUrl;
/** 43 base64url characters, like a real link token (R6). */
const TOKEN = 'Qm9iIG9uIGEgc2hhcmVkIGxhcHRvcC1fXy0tLS0tLS0';

const PREVIEW: InvitationPreview = {
  kind: 'Residency',
  communityName: 'Sakura Heights',
  offer: { role: null, termEndDate: null, propertyAddress: '711 Keystone Park Dr #29' },
  sentBy: 'Jordan Blake',
  expiresAt: '2026-10-13T20:40:00Z',
  timeZoneId: 'America/New_York',
  invitedEmail: 'kim@example.com',
  hasAccount: false,
};

/** The token may only ever be in the JSON body: never the URL, the query string or a header (R6). */
function expectTokenOnlyInBody(req: TestRequest): void {
  expect(req.request.url).not.toContain(TOKEN);
  expect(req.request.urlWithParams).not.toContain(TOKEN);
  expect(req.request.params.keys()).toEqual([]);
  for (const name of req.request.headers.keys()) {
    expect(req.request.headers.getAll(name)!.join(' ')).not.toContain(TOKEN);
  }
  expect((req.request.body as { token: string }).token).toBe(TOKEN);
}

describe('InvitationService (030 T055)', () => {
  let svc: InvitationService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [provideRouter([])],
    });
    svc = TestBed.inject(InvitationService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('preview() POSTs the token in the body, never in the URL', async () => {
    const p = svc.preview(TOKEN);
    const req = http.expectOne(`${BASE}/invitations/preview`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ token: TOKEN });
    expectTokenOnlyInBody(req);
    req.flush(PREVIEW);
    expect(await p).toEqual(PREVIEW);
  });

  it('preview() rejects with the refusal code for a dead link', async () => {
    const p = svc.preview(TOKEN);
    http.expectOne(`${BASE}/invitations/preview`)
      .flush({ code: 'INVITATION_EXPIRED', message: 'expired' }, { status: 410, statusText: 'Gone' });
    await expectAsync(p).toBeRejectedWith(jasmine.objectContaining({ status: 410, error: jasmine.objectContaining({ code: 'INVITATION_EXPIRED' }) }));
  });

  it('register() POSTs token, names and password in the body, with credentials, and signs in', async () => {
    const p = svc.register(TOKEN, { firstName: 'Kim', lastName: 'Tran', password: 'Sakura#2026' });
    const req = http.expectOne(`${BASE}/invitations/register`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ token: TOKEN, firstName: 'Kim', lastName: 'Tran', password: 'Sakura#2026' });
    expect(req.request.withCredentials).toBeTrue();
    expectTokenOnlyInBody(req);
    req.flush({
      token: 'access.jwt.token', expiresAt: '2099-01-01T00:00:00Z',
      user: {
        id: 'u-kim', firstName: 'Kim', lastName: 'Tran', email: 'kim@example.com', initials: 'KT',
        properties: [{ id: 'p29', accountNumber: 'SAKURA-029', address: '711 Keystone Park Dr #29', communityStatus: 'Active' }],
        lastActiveMode: 'Resident', memberships: [], isCompanyAdministrator: false, landing: 'Resident',
      },
    }, { status: 201, statusText: 'Created' });
    const res = await p;

    expect(res.user.landing).toBe('Resident');
    const auth = TestBed.inject(AuthService);
    expect(auth.isLoggedIn()).toBeTrue();
    expect(auth.user()!.landing).toBe('Resident');
  });

  it('register() leaves the visitor signed out when account creation fails', async () => {
    const p = svc.register(TOKEN, { firstName: 'Ann', lastName: 'Brooks', password: 'Sakura#2026' });
    http.expectOne(`${BASE}/invitations/register`)
      .flush({ code: 'REGISTRATION_FAILED', message: 'x' }, { status: 422, statusText: 'Unprocessable Entity' });
    await expectAsync(p).toBeRejected();
    expect(TestBed.inject(AuthService).isLoggedIn()).toBeFalse();
  });

  it('accept() POSTs only the token in the body', async () => {
    const p = svc.accept(TOKEN);
    const req = http.expectOne(`${BASE}/invitations/accept`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ token: TOKEN });
    expectTokenOnlyInBody(req);
    req.flush({ accepted: true, alreadyHeld: false, landing: 'Board' });
    expect(await p).toEqual({ accepted: true, alreadyHeld: false, landing: 'Board' });
  });
});
