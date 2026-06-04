import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  // Public auth routes
  { path: '',       redirectTo: '/portal', pathMatch: 'full' },
  { path: 'portal', loadComponent: () => import('./features/auth/portal-select.component').then(m => m.PortalSelectComponent) },
  { path: 'login',  loadComponent: () => import('./features/auth/login.component').then(m => m.LoginComponent) },
  { path: 'register', loadComponent: () => import('./features/auth/register.component').then(m => m.RegisterComponent) },

  // Protected app shell
  {
    path: 'app',
    loadComponent: () => import('./shell/shell.component').then(m => m.ShellComponent),
    canActivate: [authGuard],
    children: [
      { path: '',          redirectTo: 'dashboard', pathMatch: 'full' },
      { path: 'dashboard', loadComponent: () => import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent) },

      // Payments
      { path: 'payments/statement', loadComponent: () => import('./features/payments/statement/statement.component').then(m => m.StatementComponent) },
      { path: 'payments/one-time',  loadComponent: () => import('./features/payments/one-time/one-time.component').then(m => m.OneTimeComponent) },
      { path: 'payments/recurring', loadComponent: () => import('./features/payments/recurring/recurring.component').then(m => m.RecurringComponent) },

      // Property
      { path: 'property/info',      loadComponent: () => import('./features/property/info/property-info.component').then(m => m.PropertyInfoComponent) },
      { path: 'property/owner',     loadComponent: () => import('./features/property/owner/owner.component').then(m => m.OwnerComponent) },
      { path: 'property/directory', loadComponent: () => import('./features/property/directory/directory.component').then(m => m.DirectoryComponent) },

      // Community
      { path: 'community/announcements', loadComponent: () => import('./features/community/announcements/announcements.component').then(m => m.AnnouncementsComponent) },
      { path: 'community/calendar',      loadComponent: () => import('./features/community/calendar/calendar.component').then(m => m.CalendarComponent) },
      { path: 'community/violations',    loadComponent: () => import('./features/community/violations/violations.component').then(m => m.ViolationsComponent) },
      { path: 'community/documents',     loadComponent: () => import('./features/community/documents/documents.component').then(m => m.DocumentsComponent) },
    ]
  },

  // Catch-all
  { path: '**', redirectTo: '/portal' },
];
