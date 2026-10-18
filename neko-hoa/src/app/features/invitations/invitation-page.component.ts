import { AfterViewChecked, Component, ElementRef, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Meta } from '@angular/platform-browser';
import { Router, RouterLink } from '@angular/router';
import { InvitationPreview, InvitationService } from '../../core/services/invitation.service';
import { AuthService } from '../../core/services/auth.service';
import { SessionRefreshService } from '../../core/services/session-refresh';
import { BoardNavigationService } from '../../core/services/board-navigation.service';
import { ToastService } from '../../core/services/toast.service';
import { landingTargetFor } from '../../core/services/landing';
import { CurrentUser, Landing } from '../../core/models';
import {
  RATE_LIMITED_MESSAGE, refusal, refusalCodeOf, refusalMessage, roleLabel, serverMessageOf, withArticle,
} from '../../shared/refusal-messages';
import { formatDateTimeInZone, formatPlainDate, formatTodayUtc } from '../../shared/date-format';

// 030 brief 10 / FR-022, FR-022a / R6: the public page an invitee reaches from an invitation email.
//
// The token arrives in the URL fragment (`/invite#t=…`), which browsers never send to a server.
// The page reads it once, removes it from the address bar with `history.replaceState`, and sends
// it only in request bodies (InvitationService). While shown, the page sets
// `<meta name="referrer" content="no-referrer">`, and OpenTelemetry strips URL fragments from spans.
// Refusal pages show nothing about the invitation; the mismatch page never shows the invited email.

type View =
  | 'loading' | 'loadError' | 'rateLimited'
  | 'landing' | 'create' | 'signin' | 'confirm'
  | 'expired' | 'invalid' | 'mismatch';

type Field = 'firstName' | 'lastName' | 'password';

const FIELD_IDS: Record<Field, string> = { firstName: 'inv-first-name', lastName: 'inv-last-name', password: 'inv-password' };
const PASSWORD_RULE = 'Password must be at least 8 characters, with an uppercase letter, a lowercase letter, a number and a symbol.';

@Component({
  selector: 'app-invitation-page',
  standalone: true,
  imports: [FormsModule, RouterLink, NgTemplateOutlet],
  template: `
    <div class="inv">
      <header class="inv__bar">
        <span class="logo-mark"></span>
        <span class="hand inv__brand">NekoHOA</span>
        <span class="inv__bar-label">Invitation</span>
      </header>

      <main class="inv__main">
        @switch (view()) {
          @case ('loading') {
            <p class="muted inv__center" role="status">Checking your invitation link…</p>
          }

          @case ('landing') {
            <div class="inv__landing">
              <div class="inv__lead">
                <h1 class="page-title" id="inv-heading" tabindex="-1">You're invited</h1>
                <p class="muted">Accept to get access. Invitation links work for 72 hours.</p>
              </div>
              <ng-container *ngTemplateOutlet="card" />
              <div class="inv__actions">
                @if (p()!.hasAccount !== true) {
                  <button type="button" class="btn btn--primary" (click)="openCreate()">Create an account</button>
                }
                @if (p()!.hasAccount !== false) {
                  <button type="button" class="btn" [class.btn--primary]="p()!.hasAccount === true" (click)="openSignIn()">Sign in to accept</button>
                }
              </div>
            </div>
          }

          @case ('create') {
            <section class="inv__narrow">
              <ol class="stepper inv__steps" aria-label="Steps">
                <li class="pill pill--ok">1 invitation ✓</li>
                <li class="pill pill--rose" aria-current="step">2 create account</li>
              </ol>
              <h1 class="page-title" id="inv-heading" tabindex="-1">Create your account</h1>
              <p class="muted">Creating your account accepts the invitation. You don't need a claim code.</p>
              <p class="inv__summary-line">{{ offerSummary() }}</p>
              <ng-container *ngTemplateOutlet="bannerTpl" />
              @if (summary().length) {
                <div class="card inv__errors" id="inv-summary" role="alert" aria-labelledby="inv-summary-title" tabindex="-1">
                  <h2 class="section-title" id="inv-summary-title">Check these fields</h2>
                  <ul>
                    @for (e of summary(); track e.field) {
                      <li><a [href]="'#' + fieldId(e.field)" (click)="focusField($event, e.field)">{{ e.message }}</a></li>
                    }
                  </ul>
                </div>
              }
              <form class="inv__form" (ngSubmit)="createAccount()" novalidate>
                <div>
                  <span class="field-label">Email</span>
                  <p class="inv__email" aria-describedby="inv-email-help">{{ p()!.invitedEmail }}</p>
                  <p class="inv__help" id="inv-email-help">Your invitation link confirms this email address. It can't be changed.</p>
                </div>
                <div>
                  <label class="field-label" for="inv-first-name">First name</label>
                  <input class="field" id="inv-first-name" name="firstName" autocomplete="given-name" [(ngModel)]="firstName"
                         [attr.aria-invalid]="errors().firstName ? 'true' : null"
                         [attr.aria-describedby]="errors().firstName ? 'inv-first-name-error' : null" />
                  @if (errors().firstName; as m) { <p class="inv__error" id="inv-first-name-error">{{ m }}</p> }
                </div>
                <div>
                  <label class="field-label" for="inv-last-name">Last name</label>
                  <input class="field" id="inv-last-name" name="lastName" autocomplete="family-name" [(ngModel)]="lastName"
                         [attr.aria-invalid]="errors().lastName ? 'true' : null"
                         [attr.aria-describedby]="errors().lastName ? 'inv-last-name-error' : null" />
                  @if (errors().lastName; as m) { <p class="inv__error" id="inv-last-name-error">{{ m }}</p> }
                </div>
                <div>
                  <label class="field-label" for="inv-password">Password</label>
                  <div class="inv__pw">
                    <input class="field" id="inv-password" name="password" autocomplete="new-password" [(ngModel)]="password"
                           [type]="showPassword() ? 'text' : 'password'"
                           [attr.aria-invalid]="errors().password ? 'true' : null"
                           [attr.aria-describedby]="errors().password ? 'inv-password-help inv-password-error' : 'inv-password-help'" />
                    <button type="button" class="btn btn--ghost" aria-label="Show password" [attr.aria-pressed]="showPassword()"
                            (click)="showPassword.set(!showPassword())">{{ showPassword() ? 'Hide' : 'Show' }}</button>
                  </div>
                  <p class="inv__help" id="inv-password-help">At least 8 characters, with an uppercase letter, a lowercase letter, a number and a symbol.</p>
                  @if (errors().password; as m) { <p class="inv__error" id="inv-password-error">{{ m }}</p> }
                </div>
                <p class="muted">All fields are required.</p>
                <button type="submit" class="btn btn--primary btn--block inv__submit" [disabled]="busy()">
                  {{ busy() ? 'Creating account…' : 'Create account and accept' }}
                </button>
              </form>
              <div class="inv__links">
                <button type="button" class="link inv__linkbtn" (click)="view.set('landing'); focusAfterRender('inv-heading')">Back to invitation</button>
                <button type="button" class="link inv__linkbtn" (click)="openSignIn()">Already have an account? Sign in to accept</button>
              </div>
            </section>
          }

          @case ('signin') {
            <div class="inv__cols">
              <div class="inv__lead">
                <h1 class="page-title" id="inv-heading" tabindex="-1">Sign in to accept</h1>
                <ng-container *ngTemplateOutlet="card" />
              </div>
              <section class="inv__form" aria-labelledby="inv-signin-label">
                <div class="field-label" id="inv-signin-label">Sign in</div>
                <ol class="stepper inv__steps" aria-label="Steps">
                  <li class="pill pill--ok">1 invitation ✓</li>
                  <li class="pill pill--rose" aria-current="step">2 sign in</li>
                  <li class="pill">3 accept</li>
                </ol>
                <ng-container *ngTemplateOutlet="bannerTpl" />
                <form class="inv__form" (ngSubmit)="signIn()" novalidate>
                  <div>
                    <label class="field-label" for="inv-signin-email">Email</label>
                    <input class="field" id="inv-signin-email" name="signInEmail" type="email" inputmode="email" autocomplete="email"
                           [(ngModel)]="signInEmail" [attr.aria-describedby]="p()!.invitedEmail ? 'inv-signin-email-help' : null" />
                    @if (p()!.invitedEmail) {
                      <p class="inv__help" id="inv-signin-email-help">Use the account for {{ p()!.invitedEmail }}, the address this invitation was sent to.</p>
                    }
                  </div>
                  <div>
                    <label class="field-label" for="inv-signin-password">Password</label>
                    <input class="field" id="inv-signin-password" name="signInPassword" type="password" autocomplete="current-password"
                           [(ngModel)]="signInPassword" />
                  </div>
                  @if (signInError()) { <p class="inv__error" role="alert">{{ signInError() }}</p> }
                  <button type="submit" class="btn btn--primary btn--block inv__submit" [disabled]="busy()">
                    {{ busy() ? 'Signing in…' : 'Sign in' }}
                  </button>
                </form>
                <div class="inv__links">
                  <button type="button" class="link inv__linkbtn" (click)="openCreate()">New here? Create an account</button>
                </div>
              </section>
            </div>
          }

          @case ('confirm') {
            <section class="inv__narrow">
              <ol class="stepper inv__steps" aria-label="Steps">
                <li class="pill pill--ok">1 invitation ✓</li>
                <li class="pill pill--ok">2 sign in ✓</li>
                <li class="pill pill--rose" aria-current="step">3 accept</li>
              </ol>
              <h1 class="page-title" id="inv-heading" tabindex="-1">Accept this invitation?</h1>
              <p>You're signed in as {{ signedInEmail() }}.</p>
              <ng-container *ngTemplateOutlet="card" />
              <p>{{ whatHappens() }}</p>
              <ng-container *ngTemplateOutlet="bannerTpl" />
              <div class="inv__actions">
                <button type="button" class="btn btn--primary" [disabled]="busy()" (click)="accept()">
                  {{ busy() ? 'Accepting…' : 'Accept invitation' }}
                </button>
                <button type="button" class="btn" (click)="notNow()">Not now</button>
              </div>
            </section>
          }

          @default {
            <!-- Refusal and error pages: nothing about the invitation is shown (FR-022a). -->
            <section class="card inv__refusal" aria-labelledby="inv-heading">
              <h1 class="page-title" id="inv-heading" tabindex="-1">{{ pageText().title }}</h1>
              @if (pageText().message) { <p>{{ pageText().message }}</p> }
              @switch (view()) {
                @case ('mismatch') { <button type="button" class="btn btn--primary" (click)="signOut()">Sign out</button> }
                @case ('rateLimited') { <button type="button" class="btn btn--primary" (click)="load()">Try again</button> }
                @case ('loadError') { <button type="button" class="btn btn--primary" (click)="load()">Try again</button> }
                @default { <a class="link" routerLink="/login">Go to sign in</a> }
              }
            </section>
          }
        }
      </main>
    </div>

    <ng-template #card>
      <section class="card card--lav inv__card" aria-label="Your invitation">
        <span class="pill pill--ok">Valid invitation</span>
        @if (p()!.kind === 'Residency') {
          <h2 class="section-title inv__card-title"><span aria-hidden="true">🏠 </span>{{ p()!.offer.propertyAddress }}</h2>
          <div class="inv__chips"><span class="pill">{{ p()!.communityName }}</span><span class="pill">Resident</span></div>
        } @else {
          <h2 class="section-title inv__card-title">{{ p()!.communityName }}</h2>
        }
        <dl class="inv__rows">
          <div><dt>You're invited as</dt><dd>{{ offerLabel() }}</dd></div>
          @if (p()!.sentBy) { <div><dt>Sent by</dt><dd>{{ p()!.sentBy }}</dd></div> }
          @if (p()!.invitedEmail) { <div><dt>For</dt><dd>{{ p()!.invitedEmail }}</dd></div> }
          <div><dt>Expires</dt><dd>{{ expires() }}</dd></div>
        </dl>
      </section>
    </ng-template>

    <ng-template #bannerTpl>
      @if (banner()) {
        <div class="alert alert--error inv__banner" id="inv-banner" role="alert" tabindex="-1">
          <p>{{ banner() }}</p>
          @if (bannerOffersSignIn()) {
            <button type="button" class="btn" (click)="openSignIn()">Sign in to accept</button>
          }
        </div>
      }
    </ng-template>
  `,
  styles: [`
    .inv { min-height: 100vh; display: flex; flex-direction: column; background: var(--paper); }
    .inv__bar { display: flex; align-items: center; gap: 10px; padding: 14px 16px; border-bottom: 1.5px dashed var(--line); }
    .inv__brand { font-size: 22px; font-weight: 700; }
    .inv__bar-label { margin-left: auto; font-size: 12px; color: var(--ink-soft); }
    .inv__main { flex: 1; width: 100%; max-width: 1000px; margin: 0 auto; padding: 24px 16px 40px; overflow-wrap: anywhere; }
    .inv__landing, .inv__cols { display: grid; grid-template-columns: 1fr 1fr; gap: 16px 32px; align-items: start; }
    .inv__landing { grid-template-areas: "lead card" "acts card"; }
    .inv__lead { grid-area: lead; display: flex; flex-direction: column; gap: 12px; }
    .inv__landing > .inv__card { grid-area: card; }
    .inv__landing > .inv__actions { grid-area: acts; }
    .inv__narrow { max-width: 520px; margin: 0 auto; display: flex; flex-direction: column; gap: 12px; }
    .inv__form { display: flex; flex-direction: column; gap: 12px; }
    .inv__steps { list-style: none; flex-wrap: wrap; }
    .inv__actions, .inv__links { display: flex; flex-wrap: wrap; gap: 10px; }
    .inv__actions .btn, .inv__submit, .inv__pw .btn { min-height: 44px; justify-content: center; }
    .inv__linkbtn { background: none; border: none; font: inherit; font-size: 12px; padding: 8px 0; min-height: 44px; }
    .inv__card { display: flex; flex-direction: column; gap: 8px; align-items: flex-start; }
    .inv__chips { display: flex; gap: 6px; flex-wrap: wrap; }
    .inv__rows { display: grid; gap: 6px; width: 100%; }
    .inv__rows dt { font-size: 11px; color: var(--ink-soft); }
    .inv__rows dd { font-weight: 500; }
    .inv__pw { display: flex; gap: 8px; }
    .inv__email { font-weight: 600; }
    .inv__help { font-size: 11px; color: var(--ink-soft); margin-top: 4px; }
    .inv__error { font-size: 12px; color: var(--ink); border-left: 3px solid var(--rose); padding-left: 6px; margin-top: 4px; }
    .inv__errors ul { margin: 6px 0 0 18px; }
    .inv__banner { flex-direction: column; }
    .inv__refusal { max-width: 520px; margin: 32px auto; display: flex; flex-direction: column; gap: 12px; align-items: flex-start; }
    .inv__center { text-align: center; margin-top: 40px; }
    @media (max-width: 719.98px) {
      .inv__landing, .inv__cols { grid-template-columns: 1fr; }
      .inv__landing { grid-template-areas: "lead" "card" "acts"; }
      .inv__actions .btn { width: 100%; }
    }
  `],
})
export class InvitationPageComponent implements OnInit, AfterViewChecked, OnDestroy {
  private invitations = inject(InvitationService);
  private auth = inject(AuthService);
  private session = inject(SessionRefreshService);
  private nav = inject(BoardNavigationService);
  private router = inject(Router);
  private toasts = inject(ToastService);
  private meta = inject(Meta);
  private host = inject<ElementRef<HTMLElement>>(ElementRef);

  readonly view = signal<View>('loading');
  readonly p = signal<InvitationPreview | null>(null);
  readonly busy = signal(false);
  readonly banner = signal<string | null>(null);
  readonly bannerOffersSignIn = signal(false);
  readonly errors = signal<Partial<Record<Field, string>>>({});
  readonly showPassword = signal(false);
  readonly signInError = signal<string | null>(null);

  firstName = '';
  lastName = '';
  password = '';
  signInEmail = '';
  signInPassword = '';

  private token: string | null = null;
  private pendingFocus: string | null = null;

  readonly summary = computed(() =>
    (Object.keys(FIELD_IDS) as Field[])
      .filter(f => !!this.errors()[f])
      .map(f => ({ field: f, message: this.errors()[f]! })));

  readonly expires = computed(() => {
    const p = this.p();
    return p ? formatDateTimeInZone(p.expiresAt, p.timeZoneId) : '';
  });

  readonly offerLabel = computed(() => {
    const p = this.p();
    if (!p) return '';
    if (p.kind === 'Residency') return 'Resident of this home';
    if (p.kind === 'CompanyAdministrator') return 'Company Administrator';
    const role = roleLabel(p.offer.role);
    return p.offer.termEndDate ? `${role}, until ${formatPlainDate(p.offer.termEndDate)}` : role;
  });

  readonly offerSummary = computed(() => {
    const p = this.p();
    if (!p) return '';
    const what = p.kind === 'Residency' ? `Resident of ${p.offer.propertyAddress}`
      : p.kind === 'CompanyAdministrator' ? 'Company Administrator'
      : roleLabel(p.offer.role);
    const parts = [what, p.communityName];
    if (p.sentBy) parts.push(`Sent by ${p.sentBy}`);
    parts.push(`Expires ${this.expires()}`);
    return parts.join(' · ');
  });

  readonly whatHappens = computed(() => {
    const p = this.p();
    if (!p) return '';
    if (p.kind === 'Residency') return `${p.offer.propertyAddress} is added to your homes. Your other homes and roles don't change.`;
    if (p.kind === 'CompanyAdministrator') return `You'll be able to open the company area for ${p.communityName}.`;
    const start = `Your ${roleLabel(p.offer.role)} role in ${p.communityName} starts today, ${formatTodayUtc()}`;
    return p.offer.termEndDate ? `${start}, and ends ${formatPlainDate(p.offer.termEndDate)}.` : `${start}, with no end date.`;
  });

  readonly signedInEmail = computed(() => this.auth.user()?.email ?? '');

  /** Refusal and error page copy (brief 10 §5.1, §5.4). */
  readonly pageText = computed((): { title: string; message: string | null } => {
    switch (this.view()) {
      case 'expired': { const r = refusal('INVITATION_EXPIRED'); return { title: r.title!, message: r.message }; }
      case 'mismatch': {
        const r = refusal('INVITATION_EMAIL_MISMATCH', { signedInEmail: this.signedInEmail() });
        return { title: r.title!, message: r.message };
      }
      case 'rateLimited': { const r = refusal('RATE_LIMITED', { surface: 'page' }); return { title: r.title!, message: r.message }; }
      case 'loadError': return { title: "Couldn't load your invitation.", message: null };
      default: { const r = refusal('INVITATION_INVALID'); return { title: r.title!, message: r.message }; }
    }
  });

  constructor() {
    // Before any request leaves this page (R6).
    this.meta.updateTag({ name: 'referrer', content: 'no-referrer' });
  }

  ngOnInit(): void {
    this.token = this.takeTokenFromAddressBar();
    if (!this.token) {
      this.showPage('invalid');
      return;
    }
    void this.load();
  }

  ngAfterViewChecked(): void {
    if (!this.pendingFocus) return;
    const el = this.host.nativeElement.querySelector<HTMLElement>(`#${this.pendingFocus}`);
    if (el) {
      this.pendingFocus = null;
      // Outside this check: moving focus blurs the previous field, which updates its form state.
      setTimeout(() => el.focus());
    }
  }

  ngOnDestroy(): void {
    this.meta.removeTag("name='referrer'");
  }

  fieldId(field: Field): string {
    return FIELD_IDS[field];
  }

  focusAfterRender(id: string): void {
    this.pendingFocus = id;
  }

  focusField(event: Event, field: Field): void {
    event.preventDefault();
    this.host.nativeElement.querySelector<HTMLElement>(`#${FIELD_IDS[field]}`)?.focus();
  }

  async load(): Promise<void> {
    if (!this.token) return;
    this.view.set('loading');
    try {
      const preview = await this.invitations.preview(this.token);
      this.p.set(preview);
      if (this.auth.isLoggedIn()) {
        // A signed-in visitor goes straight to the confirm step (8.10.n). The preview carries no
        // invited email for them, so a different account is told only when it tries to accept.
        this.view.set('confirm');
      } else {
        this.view.set('landing');
      }
      this.focusAfterRender('inv-heading');
    } catch (err) {
      this.onError(err, 'preview');
    }
  }

  openCreate(): void {
    this.clearMessages();
    this.view.set('create');
    this.focusAfterRender(FIELD_IDS.firstName);
  }

  openSignIn(): void {
    this.clearMessages();
    this.signInEmail = this.signInEmail || this.p()?.invitedEmail || '';
    this.view.set('signin');
    this.focusAfterRender(this.signInEmail ? 'inv-signin-password' : 'inv-signin-email');
  }

  async createAccount(): Promise<void> {
    if (this.busy() || !this.token) return;
    this.banner.set(null);
    const problems = this.validate();
    this.errors.set(problems);
    if (Object.keys(problems).length) {
      this.focusAfterRender('inv-summary');
      return;
    }
    this.busy.set(true);
    try {
      await this.invitations.register(this.token, {
        firstName: this.firstName.trim(), lastName: this.lastName.trim(), password: this.password,
      });
      this.finish(this.successMessage(true, false), null);
    } catch (err) {
      this.onError(err, 'create');
    } finally {
      this.busy.set(false);
    }
  }

  async signIn(): Promise<void> {
    if (this.busy()) return;
    this.banner.set(null);
    this.signInError.set(null);
    this.busy.set(true);
    try {
      await this.auth.login(this.signInEmail.trim(), this.signInPassword);
    } catch (err) {
      if (refusalCodeOf(err) === 'RATE_LIMITED') this.showBanner(RATE_LIMITED_MESSAGE);
      else this.signInError.set('Invalid email or password.');
      return;
    } finally {
      this.busy.set(false);
      this.signInPassword = '';
    }
    // FR-022: bound to the invited email. A different account is refused here, without ever
    // showing the invited address, and stays signed in (8.10.w).
    const invited = this.p()?.invitedEmail;
    if (invited && normalizeEmail(this.signedInEmail()) !== normalizeEmail(invited)) {
      this.showPage('mismatch');
      return;
    }
    this.view.set('confirm');
    this.focusAfterRender('inv-heading');
  }

  async accept(): Promise<void> {
    if (this.busy() || !this.token) return;
    this.banner.set(null);
    this.busy.set(true);
    try {
      const res = await this.invitations.accept(this.token);
      // The new role or home must reach the access token too, so the session is refreshed
      // (`/auth/refresh` returns the user); `/auth/me` is the fallback (contract: "then /auth/me").
      await this.refreshSession();
      this.finish(this.successMessage(false, res.alreadyHeld), res.landing);
    } catch (err) {
      this.onError(err, 'confirm');
    } finally {
      this.busy.set(false);
    }
  }

  notNow(): void {
    this.go(landingTargetFor(this.auth.user()));
  }

  signOut(): void {
    this.auth.logout();
    this.toasts.show("You're signed out.");
  }

  private takeTokenFromAddressBar(): string | null {
    const hash = window.location.hash.startsWith('#') ? window.location.hash.slice(1) : window.location.hash;
    const token = new URLSearchParams(hash).get('t');
    if (hash) {
      history.replaceState(history.state, '', window.location.pathname + window.location.search);
    }
    return token && token.trim() ? token.trim() : null;
  }

  private validate(): Partial<Record<Field, string>> {
    const problems: Partial<Record<Field, string>> = {};
    if (!this.firstName.trim()) problems.firstName = 'Enter your first name.';
    if (!this.lastName.trim()) problems.lastName = 'Enter your last name.';
    if (!this.password) problems.password = 'Enter a password.';
    else if (!isStrongPassword(this.password)) problems.password = PASSWORD_RULE;
    return problems;
  }

  private async refreshSession(): Promise<void> {
    let token: string | null = null;
    try { token = await this.session.refresh(); } catch { token = null; }
    if (token) return;
    try { await this.auth.refreshCurrentUser(); } catch { /* keep the current user */ }
  }

  /** Lands on what the server says, with the toast (brief 10 §6, D5). */
  private finish(message: string, fallbackLanding: Landing | null): void {
    const user = this.auth.user();
    const withLanding: CurrentUser | null = user && !user.landing && fallbackLanding ? { ...user, landing: fallbackLanding } : user;
    this.toasts.show(message);
    this.go(landingTargetFor(withLanding));
  }

  private go(target: ReturnType<typeof landingTargetFor>): void {
    this.nav.setActiveCommunity(target.activeCommunityId);
    void this.router.navigate(target.commands);
  }

  private successMessage(created: boolean, alreadyHeld: boolean): string {
    const p = this.p()!;
    const address = p.offer.propertyAddress ?? '';
    const role = roleLabel(p.offer.role);
    if (alreadyHeld) {
      if (p.kind === 'Residency') return `${address} is already one of your homes. Nothing was added.`;
      if (p.kind === 'CompanyAdministrator') return "You're already a Company Administrator. Nothing was added.";
      return `You're already ${withArticle(role)} of ${p.communityName}. Nothing was added.`;
    }
    const what = p.kind === 'Residency' ? `You're now a resident of ${address}.`
      : p.kind === 'CompanyAdministrator' ? "You're now a Company Administrator."
      : `You're now ${withArticle(role)} of ${p.communityName}.`;
    return created ? `Your account is ready. ${what}` : what;
  }

  private onError(err: unknown, step: 'preview' | 'create' | 'confirm'): void {
    const code = refusalCodeOf(err);
    switch (code) {
      case 'INVITATION_EXPIRED': return this.showPage('expired');
      case 'INVITATION_INVALID':
      case 'COMMUNITY_ARCHIVED': return this.showPage('invalid');
      case 'INVITATION_EMAIL_MISMATCH': return this.showPage('mismatch');
      case 'RATE_LIMITED': return step === 'preview' ? this.showPage('rateLimited') : this.showBanner(RATE_LIMITED_MESSAGE);
    }
    if (step === 'preview') return this.showPage('loadError');
    if (step === 'confirm') return this.showBanner("Couldn't accept the invitation. Check your connection and try again.");

    if (code === 'REGISTRATION_FAILED') {
      // Generic on purpose (spec 017 FR-A5): names are kept, the password is emptied.
      this.password = '';
      return this.showBanner(refusalMessage(code), true);
    }
    if (code === 'VALIDATION_ERROR' && this.applyServerFieldErrors(err)) return;
    if (code === 'VALIDATION_ERROR') return this.showBanner(refusalMessage(code, { serverMessage: serverMessageOf(err) }));
    this.showBanner("Couldn't create your account. Check your connection and try again.");
  }

  /** Puts a 422 `errors[]` entry under its field. Returns false when none matched a field. */
  private applyServerFieldErrors(err: unknown): boolean {
    const list = ((err as { error?: { errors?: unknown } })?.error?.errors ?? []) as { field?: string; message?: string }[];
    const mapped: Partial<Record<Field, string>> = {};
    for (const e of Array.isArray(list) ? list : []) {
      const field = (Object.keys(FIELD_IDS) as Field[]).find(f => f.toLowerCase() === (e?.field ?? '').toLowerCase());
      if (field && e.message) mapped[field] = e.message;
    }
    if (!Object.keys(mapped).length) return false;
    this.errors.set(mapped);
    this.focusAfterRender('inv-summary');
    return true;
  }

  private showPage(view: View): void {
    this.view.set(view);
    this.focusAfterRender('inv-heading');
  }

  private showBanner(message: string, offerSignIn = false): void {
    this.banner.set(message);
    this.bannerOffersSignIn.set(offerSignIn);
    this.focusAfterRender('inv-banner');
  }

  private clearMessages(): void {
    this.banner.set(null);
    this.bannerOffersSignIn.set(false);
    this.signInError.set(null);
  }
}

function normalizeEmail(email: string): string {
  return email.trim().toLowerCase();
}

/** The Identity password rules: 8+ characters, upper and lower case, a digit and a symbol (contract). */
export function isStrongPassword(password: string): boolean {
  return password.length >= 8
    && /[A-Z]/.test(password)
    && /[a-z]/.test(password)
    && /\d/.test(password)
    && /[^A-Za-z0-9]/.test(password);
}
