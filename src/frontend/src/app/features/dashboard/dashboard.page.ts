import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';

interface DashboardStats {
  totalKeys: number;
  activeKeys: number;
  expiredKeys: number;
  revokedKeys: number;
  totalCertificates: number;
  totalUsers: number;
  keysByEnvironment: { environment: string; count: number }[];
  recentActivity: { action: string; resource: string; timestamp: Date; username: string }[];
}

@Component({
  selector: 'app-dashboard-page',
  templateUrl: './dashboard.page.html',
  styleUrls: ['./dashboard.page.css'],
  standalone: true,
  imports: [CommonModule]
})
export class DashboardPageComponent implements OnInit {
  stats: DashboardStats | null = null;
  loading = true;
  error: string | null = null;

  constructor(private http: HttpClient) {}

  ngOnInit() {
    this.loadDashboardStats();
  }

  loadDashboardStats() {
    this.loading = true;
    this.error = null;
    
    // Mock data for now - replace with real API call
    setTimeout(() => {
      this.stats = {
        totalKeys: 42,
        activeKeys: 38,
        expiredKeys: 2,
        revokedKeys: 2,
        totalCertificates: 5,
        totalUsers: 12,
        keysByEnvironment: [
          { environment: 'DEV', count: 15 },
          { environment: 'TEST', count: 10 },
          { environment: 'UAT', count: 8 },
          { environment: 'PROD', count: 9 }
        ],
        recentActivity: [
          { action: 'Key Retrieved', resource: 'API_KEY_PROD', timestamp: new Date(Date.now() - 5 * 60000), username: 'john.doe' },
          { action: 'Key Created', resource: 'DB_PASSWORD_DEV', timestamp: new Date(Date.now() - 15 * 60000), username: 'admin' },
          { action: 'User Created', resource: 'jane.smith', timestamp: new Date(Date.now() - 30 * 60000), username: 'admin' },
          { action: 'Certificate Uploaded', resource: 'Prod Cert 2026', timestamp: new Date(Date.now() - 60 * 60000), username: 'admin' },
          { action: 'Key Rotated', resource: 'API_KEY_PROD', timestamp: new Date(Date.now() - 120 * 60000), username: 'admin' }
        ]
      };
      this.loading = false;
    }, 500);
  }

  getStatusColor(status: string): string {
    const colors: Record<string, string> = {
      'Active': 'green',
      'Expired': 'orange',
      'Revoked': 'red'
    };
    return colors[status] || 'gray';
  }
}

