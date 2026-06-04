import { Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { MockDataService } from './mock-data.service';
import { CurrentUser } from '../models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly _user = signal<CurrentUser | null>(null);
  readonly user = this._user.asReadonly();

  constructor(private mock: MockDataService, private router: Router) {
    // Auto-login from session storage (mock persistence)
    const stored = sessionStorage.getItem('neko_user');
    if (stored) this._user.set(JSON.parse(stored));
  }

  isLoggedIn(): boolean {
    return this._user() !== null;
  }

  login(email: string, password: string): Promise<void> {
    return new Promise((resolve, reject) => {
      setTimeout(() => {
        // Mock: accept any non-empty credentials
        if (email && password) {
          const user = this.mock.currentUser;
          this._user.set(user);
          sessionStorage.setItem('neko_user', JSON.stringify(user));
          resolve();
        } else {
          reject(new Error('Invalid credentials'));
        }
      }, 600);
    });
  }

  register(_email: string, _password: string, _firstName: string, _lastName: string): Promise<void> {
    return new Promise((resolve) => {
      setTimeout(() => {
        const user = this.mock.currentUser;
        this._user.set(user);
        sessionStorage.setItem('neko_user', JSON.stringify(user));
        resolve();
      }, 800);
    });
  }

  logout(): void {
    this._user.set(null);
    sessionStorage.removeItem('neko_user');
    this.router.navigate(['/login']);
  }
}
