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
  expiringKeys30Days: number;
  expiringCertificates30Days: number;
  upcomingExpiries: { kind: string; id: string; name: string; expiresAt: Date; daysLeft: number }[];
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

  ngOnInit() { this.loadDashboardStats(); }

  loadDashboardStats() {
    this.loading = true;
    this.error = null;
    this.http.get<any>(`${environment.apiUrl}/v1/metrics`).subscribe({
      next: (res) => { this.stats = res.data; this.loading = false; },
      error: (err) => {
        this.error = err.error?.error?.message || 'Dashboard yüklenemedi';
        this.loading = false;
      }
    });
  }
}
