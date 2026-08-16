import { Component, OnInit } from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive, Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { AuthService, User } from '../../core/auth/auth.service';

interface MenuItem {
  icon: string;
  label: string;
  route: string;
  roles?: string[];
}

@Component({
  selector: 'app-main-layout',
  templateUrl: './main-layout.component.html',
  styleUrls: ['./main-layout.component.css'],
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive]
})
export class MainLayoutComponent implements OnInit {
  currentUser: User | null = null;
  sidebarOpen = true;

  menuItems: MenuItem[] = [
    { icon: '📊', label: 'Dashboard', route: '/dashboard' },
    { icon: '🔑', label: 'Keys', route: '/keys' },
    { icon: '📜', label: 'Certificates', route: '/certificates' },
    { icon: '🔌', label: 'API Clients', route: '/api-clients', roles: ['Admin'] },
    { icon: '👥', label: 'Users', route: '/users', roles: ['Admin'] },
    { icon: '🛡️', label: 'Roles', route: '/roles', roles: ['Admin'] },
    { icon: '📋', label: 'Audit', route: '/audit' }
  ];

  constructor(
    private authService: AuthService,
    private router: Router
  ) {}

  ngOnInit() {
    this.authService.currentUser$.subscribe(user => this.currentUser = user);
  }

  toggleSidebar() {
    this.sidebarOpen = !this.sidebarOpen;
  }

  hasAccess(item: MenuItem): boolean {
    if (!item.roles || item.roles.length === 0) {
      return true;
    }
    return item.roles.some(role => this.authService.hasRole(role));
  }

  logout() {
    this.authService.logout();
    this.router.navigate(['/auth/login']);
  }
}
