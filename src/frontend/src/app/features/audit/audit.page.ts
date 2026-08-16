import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NotificationService } from '../../core/services/notification.service';
import { AuditService } from '../../core/services/audit.service';

interface AuditLog {
  auditLogId: string;
  action: string;
  resource: string;
  resourceId?: string;
  username: string;
  userId: string;
  ipAddress: string;
  userAgent?: string;
  status: 'Success' | 'Failed' | 'Warning';
  details?: string;
  timestamp: Date;
}

@Component({
  selector: 'app-audit-page',
  templateUrl: './audit.page.html',
  styleUrls: ['./audit.page.css'],
  standalone: true,
  imports: [CommonModule, FormsModule]
})
export class AuditPageComponent implements OnInit {
  auditLogs: AuditLog[] = [];
  filteredLogs: AuditLog[] = [];
  searchTerm = '';
  selectedAction = '';
  selectedResource = '';
  selectedStatus = '';
  dateFrom = '';
  dateTo = '';
  currentPage = 1;
  pageSize = 20;
  totalPages = 1;
  uniqueActions: string[] = [];
  uniqueResources: string[] = [];
  statuses = ['Success', 'Failed', 'Warning'];

  constructor(
    private notificationService: NotificationService,
    private auditService: AuditService
  ) {}

  ngOnInit() {
    this.loadAuditLogs();
  }

  loadAuditLogs() {
    this.auditService.getTrails({ pageSize: '100' }).subscribe({
      next: (res) => {
        this.auditLogs = (res.data || []).map((row: any) => ({
          auditLogId: row.auditId,
          action: row.action,
          resource: row.resource,
          resourceId: row.resourceId,
          username: row.username,
          userId: row.userId,
          ipAddress: row.ipAddress || '',
          userAgent: row.userAgent,
          status: row.severity === 'Critical' ? 'Failed' : row.severity === 'Warning' ? 'Warning' : 'Success',
          details: row.details,
          timestamp: row.timestamp
        }));
        this.uniqueActions = [...new Set(this.auditLogs.map(log => log.action))];
        this.uniqueResources = [...new Set(this.auditLogs.map(log => log.resource))];
        this.applyFilters();
      },
      error: () => this.notificationService.error('Hata', 'Audit kayıtları yüklenemedi')
    });
  }

  applyFilters() {
    this.filteredLogs = this.auditLogs.filter(log => {
      const matchesSearch = !this.searchTerm ||
        log.username.toLowerCase().includes(this.searchTerm.toLowerCase()) ||
        log.action.toLowerCase().includes(this.searchTerm.toLowerCase()) ||
        log.details?.toLowerCase().includes(this.searchTerm.toLowerCase());
      const matchesAction = !this.selectedAction || log.action === this.selectedAction;
      const matchesResource = !this.selectedResource || log.resource === this.selectedResource;
      const matchesStatus = !this.selectedStatus || log.status === this.selectedStatus;
      let matchesDate = true;
      if (this.dateFrom) matchesDate = matchesDate && new Date(log.timestamp) >= new Date(this.dateFrom);
      if (this.dateTo) matchesDate = matchesDate && new Date(log.timestamp) <= new Date(this.dateTo);
      return matchesSearch && matchesAction && matchesResource && matchesStatus && matchesDate;
    });
    this.currentPage = 1;
    this.updatePagination();
  }

  clearFilters() {
    this.searchTerm = '';
    this.selectedAction = '';
    this.selectedResource = '';
    this.selectedStatus = '';
    this.dateFrom = '';
    this.dateTo = '';
    this.filteredLogs = [...this.auditLogs];
    this.currentPage = 1;
    this.updatePagination();
  }

  updatePagination() {
    this.totalPages = Math.max(1, Math.ceil(this.filteredLogs.length / this.pageSize));
  }

  get paginatedLogs(): AuditLog[] {
    const start = (this.currentPage - 1) * this.pageSize;
    return this.filteredLogs.slice(start, start + this.pageSize);
  }

  nextPage() { if (this.currentPage < this.totalPages) this.currentPage++; }
  previousPage() { if (this.currentPage > 1) this.currentPage--; }

  getStatusClass(status: string): string {
    switch (status) {
      case 'Success': return 'status-success';
      case 'Failed': return 'status-failed';
      case 'Warning': return 'status-warning';
      default: return '';
    }
  }

  getActionIcon(action: string): string {
    if (action.includes('LOGIN')) return '🔐';
    if (action.includes('CREATE') || action.includes('Create')) return '➕';
    if (action.includes('UPDATE')) return '✏️';
    if (action.includes('DELETE') || action.includes('Delete')) return '🗑️';
    if (action.includes('Retrieve')) return '👁️';
    if (action.includes('Revoke')) return '🚫';
    if (action.includes('Upload')) return '📤';
    return '📋';
  }

  exportLogs() {
    const header = 'timestamp,action,resource,username,status,details\n';
    const rows = this.filteredLogs.map(l =>
      `${l.timestamp},${l.action},${l.resource},${l.username},${l.status},"${(l.details || '').replace(/"/g, '""')}"`
    ).join('\n');
    const blob = new Blob([header + rows], { type: 'text/csv' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'audit-logs.csv';
    a.click();
    URL.revokeObjectURL(url);
  }

  getSuccessCount(): number { return this.filteredLogs.filter(log => log.status === 'Success').length; }
  getFailedCount(): number { return this.filteredLogs.filter(log => log.status === 'Failed').length; }
  getWarningCount(): number { return this.filteredLogs.filter(log => log.status === 'Warning').length; }
}
