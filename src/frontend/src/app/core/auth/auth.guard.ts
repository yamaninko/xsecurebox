import { Injectable } from '@angular/core';
import { Router, CanActivate, ActivatedRouteSnapshot, RouterStateSnapshot } from '@angular/router';
import { AuthService } from './auth.service';

@Injectable({
  providedIn: 'root'
})
export class AuthGuard implements CanActivate {
  constructor(
    private authService: AuthService,
    private router: Router
  ) {}

  canActivate(route: ActivatedRouteSnapshot, state: RouterStateSnapshot): boolean {
    if (this.authService.isAuthenticated()) {
      if (this.authService.mustChangePassword() && !state.url.startsWith('/auth/change-password')) {
        this.router.navigate(['/auth/change-password']);
        return false;
      }
      if (this.authService.mustSetupMfa() && !state.url.startsWith('/auth/mfa-setup')) {
        this.router.navigate(['/auth/mfa-setup']);
        return false;
      }

      const requiredRoles = route.data['roles'] as string[];
      if (requiredRoles && requiredRoles.length > 0) {
        const hasRole = requiredRoles.some(role => this.authService.hasRole(role));
        if (!hasRole) {
          this.router.navigate(['/dashboard']);
          return false;
        }
      }
      return true;
    }

    // Not authenticated, redirect to login
    this.router.navigate(['/auth/login'], { queryParams: { returnUrl: state.url } });
    return false;
  }
}

