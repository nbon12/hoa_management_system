import { AfterViewInit, Component, ElementRef, ViewChild, inject } from '@angular/core';
import { AuthService } from '../../core/services/auth.service';
import { LandingFrameComponent } from './landing-frame.component';

/**
 * 030 FR-036a step 3 / US9-S5 / brief 11 8.11.b: "Your community isn't live yet". The landing of a
 * person whose only homes are in Onboarding communities. It names neither the home nor the
 * community, uses no error styling, and never shows a status word (D7).
 */
@Component({
  selector: 'app-not-live',
  standalone: true,
  imports: [LandingFrameComponent],
  template: `
    <app-landing-frame>
      <section class="card lp" aria-labelledby="lp-title">
        <h1 class="page-title" id="lp-title" tabindex="-1" #title>Your community isn't live yet</h1>
        <p>Your management company is still setting up your community. You'll see your home here once it's live.</p>
        <p>There's nothing you need to do now.</p>
        <p>If you received an invitation, open the link in that email.</p>
        <button type="button" class="btn lp__btn" (click)="signOut()">Sign out</button>
      </section>
    </app-landing-frame>
  `,
  styles: [`
    .lp { display: flex; flex-direction: column; gap: 10px; align-items: flex-start; padding: 24px; }
    .lp__btn { min-height: 44px; margin-top: 6px; }
  `],
})
export class NotLiveComponent implements AfterViewInit {
  private auth = inject(AuthService);
  @ViewChild('title', { static: true }) private title!: ElementRef<HTMLElement>;

  ngAfterViewInit(): void {
    setTimeout(() => this.title.nativeElement.focus());
  }

  signOut(): void {
    this.auth.logout();
  }
}
