import { inject } from '@angular/core';
import { Router, CanActivateFn } from '@angular/router';
import { AuthService } from '../auth/auth.service';

export const authGuard: CanActivateFn = (route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isAuthenticated()) {
    // Check for required role if specified in route data
    const requiredRoles = route.data['roles'] as string[] | undefined;
    
    if (requiredRoles && requiredRoles.length > 0) {
      const user = authService.getCurrentUser();
      const hasRole = user?.roles?.some(role => requiredRoles.includes(role));
      
      if (!hasRole) {
        router.navigate(['/dashboard']);
        return false;
      }
    }
    
    return true;
  }

  router.navigate(['/login'], { queryParams: { returnUrl: state.url } });
  return false;
};

