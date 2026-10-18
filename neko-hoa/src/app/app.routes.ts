import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { boardGuard } from './core/guards/board.guard';
import { companyAdminGuard } from './core/guards/company-admin.guard';
import { landingGuard } from './core/guards/landing.guard';

export const routes: Routes = [
  // Public auth routes
  { path: '',       redirectTo: '/portal', pathMatch: 'full' },
  { path: 'portal', loadComponent: () => import('./features/auth/portal-select.component').then(m => m.PortalSelectComponent) },
  { path: 'login',  loadComponent: () => import('./features/auth/login.component').then(m => m.LoginComponent) },
  { path: 'register', loadComponent: () => import('./features/auth/register.component').then(m => m.RegisterComponent) },
  // 030 FR-022a / R6: public invitation acceptance. The token is in the URL fragment (`#t=…`).
  { path: 'invite', loadComponent: () => import('./features/invitations/invitation-page.component').then(m => m.InvitationPageComponent) },
  // 030 FR-036c: the bookmarkable company sign-in page. It grants nothing by itself.
  { path: 'company/sign-in', loadComponent: () => import('./features/auth/company-sign-in.component').then(m => m.CompanySignInComponent) },

  // 030 FR-036a steps 3–4: landings for a person with no usable home. Outside the app shell, so
  // they show no nav (brief 11 D1); matched before the `app` route below.
  { path: 'app/no-home',  canActivate: [authGuard], loadComponent: () => import('./features/landing/no-home.component').then(m => m.NoHomeComponent) },
  { path: 'app/not-live', canActivate: [authGuard], loadComponent: () => import('./features/landing/not-live.component').then(m => m.NotLiveComponent) },

  // Protected app shell
  {
    path: 'app',
    loadComponent: () => import('./shell/shell.component').then(m => m.ShellComponent),
    canActivate: [authGuard],
    children: [
      // 025 FR-026: the default landing is mode-aware — board mode lands on the community's
      // home (or the My Communities list), everyone else on the resident dashboard. Only this
      // empty-path entry is redirected; explicit deep links below are matched as written.
      { path: '',          pathMatch: 'full', canActivate: [landingGuard], children: [] },
      { path: 'dashboard', loadComponent: () => import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent) },

      // Payments
      { path: 'payments/statement', loadComponent: () => import('./features/payments/statement/statement.component').then(m => m.StatementComponent) },
      { path: 'payments/one-time',  loadComponent: () => import('./features/payments/one-time/one-time.component').then(m => m.OneTimeComponent) },
      { path: 'payments/recurring', loadComponent: () => import('./features/payments/recurring/recurring.component').then(m => m.RecurringComponent) },

      // Property
      { path: 'property/info',      loadComponent: () => import('./features/property/info/property-info.component').then(m => m.PropertyInfoComponent) },
      { path: 'property/owner',     loadComponent: () => import('./features/property/owner/owner.component').then(m => m.OwnerComponent) },
      { path: 'property/directory', loadComponent: () => import('./features/property/directory/directory.component').then(m => m.DirectoryComponent) },

      // Architectural requests (029). These paths match the owner links in 027's outcome emails.
      { path: 'property/architectural',                 loadComponent: () => import('./features/property/architectural/my-requests-page.component').then(m => m.MyRequestsPageComponent) },
      { path: 'property/architectural/new',             loadComponent: () => import('./features/property/architectural/request-form.component').then(m => m.RequestFormComponent) },
      { path: 'property/architectural/drafts/:draftId', loadComponent: () => import('./features/property/architectural/request-form.component').then(m => m.RequestFormComponent) },
      { path: 'property/architectural/:id/revise',      loadComponent: () => import('./features/property/architectural/request-form.component').then(m => m.RequestFormComponent) },
      { path: 'property/architectural/:id',             loadComponent: () => import('./features/property/architectural/request-detail.component').then(m => m.RequestDetailComponent) },

      // Community
      { path: 'community/announcements', loadComponent: () => import('./features/community/announcements/announcements.component').then(m => m.AnnouncementsComponent) },
      { path: 'community/calendar',      loadComponent: () => import('./features/community/calendar/calendar.component').then(m => m.CalendarComponent) },
      { path: 'community/violations',    loadComponent: () => import('./features/community/violations/violations.component').then(m => m.ViolationsComponent) },
      { path: 'community/documents',     loadComponent: () => import('./features/community/documents/documents.component').then(m => m.DocumentsComponent) },

      // Board (025) — guarded by authGuard + boardGuard; role-gated routes carry requiredRoles.
      { path: 'board/home',        canActivate: [boardGuard], loadComponent: () => import('./features/board/community-home/community-home.component').then(m => m.CommunityHomeComponent) },
      { path: 'board/communities', canActivate: [boardGuard], loadComponent: () => import('./features/board/communities/communities.component').then(m => m.CommunitiesComponent) },
      { path: 'board/metrics',     canActivate: [boardGuard], loadComponent: () => import('./features/board/metrics/metrics-page.component').then(m => m.BoardMetricsPageComponent) },
      { path: 'board/architectural', canActivate: [boardGuard], data: { requiredRoles: ['BoardMember', 'CommunityManager'] }, loadComponent: () => import('./features/board/architectural/applications-page.component').then(m => m.ApplicationsPageComponent) },
      { path: 'board/arc-settings',  canActivate: [boardGuard], data: { requiredRoles: ['CommunityManager'] }, loadComponent: () => import('./features/board/architectural/arc-settings.component').then(m => m.ArcSettingsComponent) },
      // 030 T091 / R20: the old memberships page is now Community settings › Managers and memberships.
      { path: 'board/memberships', pathMatch: 'full', redirectTo: 'board/settings/managers-and-memberships' },

      // 030 FR-029 / R20: one Community settings page per community, addressed by the community in
      // the company area and by the active community in board mode. Each section has its own address.
      { path: 'board/settings', pathMatch: 'full', redirectTo: 'board/settings/profile' },
      { path: 'board/settings/:section', canActivate: [boardGuard], data: { requiredRoles: ['CommunityManager'], settingsMode: 'board' }, loadComponent: () => import('./features/community-settings/community-settings-page.component').then(m => m.CommunitySettingsPageComponent) },

      // 030 FR-006, FR-036: a Community Manager adds a community from board mode. The role may be
      // held in any community, not just the active one.
      { path: 'board/communities/new', canActivate: [boardGuard], data: { requiredRoles: ['CommunityManager'], roleScope: 'any', variant: 'board' }, loadComponent: () => import('./features/company/add-community.component').then(m => m.AddCommunityComponent) },

      // 030 company area (FR-036): Company Administrators only.
      { path: 'company/portfolio', canActivate: [companyAdminGuard], loadComponent: () => import('./features/company/portfolio.component').then(m => m.PortfolioComponent) },
      { path: 'company/communities/new', canActivate: [companyAdminGuard], data: { variant: 'company' }, loadComponent: () => import('./features/company/add-community.component').then(m => m.AddCommunityComponent) },
      { path: 'company/communities/:communityId/settings', pathMatch: 'full', redirectTo: 'company/communities/:communityId/settings/profile' },
      { path: 'company/communities/:communityId/settings/:section', canActivate: [companyAdminGuard], data: { settingsMode: 'company' }, loadComponent: () => import('./features/community-settings/community-settings-page.component').then(m => m.CommunitySettingsPageComponent) },
    ]
  },

  // Catch-all
  { path: '**', redirectTo: '/portal' },
];
