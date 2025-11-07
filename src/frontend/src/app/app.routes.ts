import { Routes } from '@angular/router';
import { AuthGuard } from './core/auth/auth.guard';
import { MainLayoutComponent } from './layout/main-layout/main-layout.component';
import { AuthLayoutComponent } from './layout/auth-layout/auth-layout.component';

export const routes: Routes = [
  {
    path: '',
    component: MainLayoutComponent,
    canActivate: [AuthGuard],
    children: [
      { path: '', redirectTo: '/dashboard', pathMatch: 'full' },
      { path: 'dashboard', loadChildren: () => import('./features/dashboard/dashboard.module').then(m => m.DashboardModule) },
      { path: 'keys', loadChildren: () => import('./features/keys/keys.module').then(m => m.KeysModule) },
      { path: 'certificates', loadChildren: () => import('./features/certificates/certificates.module').then(m => m.CertificatesModule) },
      { path: 'users', loadChildren: () => import('./features/users/users.module').then(m => m.UsersModule) },
      { path: 'roles', loadChildren: () => import('./features/roles/roles.module').then(m => m.RolesModule) },
      { path: 'api-clients', loadChildren: () => import('./features/api-clients/api-clients.module').then(m => m.ApiClientsModule) },
      { path: 'audit', loadChildren: () => import('./features/audit/audit.module').then(m => m.AuditModule) }
    ]
  },
  {
    path: 'auth',
    component: AuthLayoutComponent,
    loadChildren: () => import('./features/auth/auth.module').then(m => m.AuthModule)
  },
  { path: '**', redirectTo: '/dashboard' }
];

