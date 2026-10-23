import { AfterViewInit, Component, ElementRef, ViewChild, computed, inject } from '@angular/core';
import { AuthService } from '../../core/services/auth.service';
import { LandingFrameComponent } from './landing-frame.component';

/**
 * 030 FR-036a step 4 / US9-S3 / brief 11 8.11.a: "No home linked to this account". The landing of
 * a person with no usable home, no effective membership and no Company Administrator role. It names
 * no community and offers no claim code.
 */
@Component({
  selector: 'app-no-home',
  standalone: true,
  imports: [LandingFrameComponent],
  template: `
    <app-landing-frame>
      <section class="card lp" aria-labelledby="lp-title">
        <h1 class="page-title" id="lp-title" tabindex="-1" #title>No home linked to this account</h1>
        <p>You're signed in as {{ email() }}, but this account isn't linked to a home.</p>
        <p>If you received an invitation, open the link in that email.</p>
        <p>Otherwise, contact your community manager.</p>
        <button type="button" class="btn lp__btn" (click)="signOut()">Sign out</button>
      </section>
    </app-landing-frame>
  `,
  styles: [`
    .lp { display: flex; flex-direction: column; gap: 10px; align-items: flex-start; padding: 24px; }
    .lp__btn { min-height: 44px; margin-top: 6px; }
  `],
})
export class NoHomeComponent implements AfterViewInit {
  private auth = inject(AuthService);
  @ViewChild('title', { static: true }) private title!: ElementRef<HTMLElement>;

  readonly email = computed(() => this.auth.user()?.email ?? '');

  ngAfterViewInit(): void {
    // Brief 11 §7: the title is the main heading and takes focus on load.
    setTimeout(() => this.title.nativeElement.focus());
  }

  signOut(): void {
    this.auth.logout();
  }
}
