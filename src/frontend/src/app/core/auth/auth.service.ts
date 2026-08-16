import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, of, tap, catchError, switchMap, map } from 'rxjs';
import { Router } from '@angular/router';
import { environment } from '../../../environments/environment';

export interface LoginRequest {
  username: string;
  password: string;
}

export interface AuthResponse {
  accessToken?: string;
  expiresIn: number;
  tokenType: string;
  user?: User;
  requiresMfa?: boolean;
  mfaChallengeId?: string;
}

export interface User {
  userId: string;
  username: string;
  email: string;
  firstName?: string;
  lastName?: string;
  roles: string[];
  permissions: string[];
  mustChangePassword: boolean;
  mfaEnabled?: boolean;
  mustSetupMfa?: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly API_URL = `${environment.apiUrl}/v1/auth`;
  private accessToken: string | null = null;
  private currentUserSubject = new BehaviorSubject<User | null>(null);
  public currentUser$ = this.currentUserSubject.asObservable();

  constructor(
    private http: HttpClient,
    private router: Router
  ) {}

  getAccessToken(): string | null {
    return this.accessToken;
  }

  login(username: string, password: string): Observable<any> {
    return this.http.post<any>(`${this.API_URL}/login`, { username, password }, { withCredentials: true })
      .pipe(
        tap(response => {
          if (response.success && response.data?.accessToken && !response.data.requiresMfa) {
            this.setSession(response.data);
          }
        })
      );
  }

  logout(): void {
    this.http.post(`${this.API_URL}/logout`, {}, { withCredentials: true }).subscribe({
      error: () => undefined
    });
    this.clearSession();
    this.router.navigate(['/auth/login']);
  }

  refreshToken(): Observable<any> {
    return this.http.post<any>(`${this.API_URL}/refresh`, {}, { withCredentials: true })
      .pipe(
        tap(response => {
          if (response.success && response.data?.accessToken) {
            this.accessToken = response.data.accessToken;
          }
        })
      );
  }

  restoreSession(): Observable<boolean> {
    return this.refreshToken().pipe(
      switchMap(() => this.http.get<any>(`${this.API_URL}/me`, { withCredentials: true })),
      tap(response => {
        if (response.success && response.data) {
          this.currentUserSubject.next(response.data);
        }
      }),
      map(() => true),
      catchError(() => {
        this.clearSession();
        return of(false);
      })
    );
  }

  verifyMfa(mfaChallengeId: string, code: string): Observable<any> {
    return this.http.post<any>(`${this.API_URL}/mfa/verify`, { mfaChallengeId, code }, { withCredentials: true })
      .pipe(tap(response => {
        if (response.success && response.data?.accessToken) {
          this.setSession(response.data);
        }
      }));
  }

  setupMfa(): Observable<any> {
    return this.http.post<any>(`${this.API_URL}/mfa/setup`, {}, { withCredentials: true });
  }

  enableMfa(code: string): Observable<any> {
    return this.http.post<any>(`${this.API_URL}/mfa/enable`, { code }, { withCredentials: true });
  }

  mustSetupMfa(): boolean {
    return this.currentUserSubject.value?.mustSetupMfa === true && !this.currentUserSubject.value?.mfaEnabled;
  }

  changePassword(currentPassword: string, newPassword: string, confirmPassword: string): Observable<any> {
    return this.http.post(`${this.API_URL}/change-password`, {
      currentPassword,
      newPassword,
      confirmPassword
    }, { withCredentials: true });
  }

  isAuthenticated(): boolean {
    return !!this.accessToken && !this.isTokenExpired(this.accessToken);
  }

  getCurrentUser(): User | null {
    return this.currentUserSubject.value;
  }

  mustChangePassword(): boolean {
    return this.currentUserSubject.value?.mustChangePassword === true;
  }

  hasRole(role: string): boolean {
    const user = this.currentUserSubject.value;
    return user?.roles?.includes(role) ?? false;
  }

  hasPermission(permission: string): boolean {
    const user = this.currentUserSubject.value;
    if (user?.roles?.includes('Admin')) {
      return true;
    }
    return user?.permissions?.includes(permission) ?? false;
  }

  private setSession(authResult: AuthResponse): void {
    this.accessToken = authResult.accessToken || null;
    this.currentUserSubject.next(authResult.user || null);
  }

  private clearSession(): void {
    this.accessToken = null;
    this.currentUserSubject.next(null);
  }

  private isTokenExpired(token: string): boolean {
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      return Date.now() >= payload.exp * 1000;
    } catch {
      return true;
    }
  }
}
