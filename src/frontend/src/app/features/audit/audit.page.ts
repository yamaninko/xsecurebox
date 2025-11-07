import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NotificationService } from '../../core/services/notification.service';

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
  
  // Filters
  searchTerm = '';
  selectedAction = '';
  selectedResource = '';
  selectedStatus = '';
  dateFrom = '';
  dateTo = '';
  
  // Pagination
  currentPage = 1;
  pageSize = 20;
  totalPages = 1;
  
  // Unique values for filters
  uniqueActions: string[] = [];
  uniqueResources: string[] = [];
  statuses = ['Success', 'Failed', 'Warning'];

  constructor(private notificationService: NotificationService) {}

  ngOnInit() {
    this.loadAuditLogs();
  }

  loadAuditLogs() {
    // Mock data - Replace with actual API call
    const mockLogs: AuditLog[] = [
      {
        auditLogId: '1',
        action: 'LOGIN',
        resource: 'Auth',
        username: 'admin',
        userId: '1',
        ipAddress: '192.168.1.100',
        userAgent: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64)',
        status: 'Success',
        details: 'User logged in successfully',
        timestamp: new Date(Date.now() - 1000 * 60 * 5)
      },
      {
        auditLogId: '2',
        action: 'CREATE_KEY',
        resource: 'Key',
        resourceId: 'key-123',
        username: 'admin',
        userId: '1',
        ipAddress: '192.168.1.100',
        status: 'Success',
        details: 'Created new API key: production-key',
        timestamp: new Date(Date.now() - 1000 * 60 * 15)
      },
      {
        auditLogId: '3',
        action: 'RETRIEVE_KEY',
        resource: 'Key',
        resourceId: 'key-123',
        username: 'admin',
        userId: '1',
        ipAddress: '192.168.1.100',
        status: 'Success',
        details: 'Retrieved key value',
        timestamp: new Date(Date.now() - 1000 * 60 * 30)
      },
      {
        auditLogId: '4',
        action: 'LOGIN',
        resource: 'Auth',
        username: 'user1',
        userId: '2',
        ipAddress: '192.168.1.101',
        status: 'Failed',
        details: 'Invalid password',
        timestamp: new Date(Date.now() - 1000 * 60 * 45)
      },
      {
        auditLogId: '5',
        action: 'UPLOAD_CERTIFICATE',
        resource: 'Certificate',
        resourceId: 'cert-456',
        username: 'admin',
        userId: '1',
        ipAddress: '192.168.1.100',
        status: 'Success',
        details: 'Uploaded certificate: production-cert.pem',
        timestamp: new Date(Date.now() - 1000 * 60 * 60)
      },
      {
        auditLogId: '6',
        action: 'DELETE_USER',
        resource: 'User',
        resourceId: 'user-789',
        username: 'admin',
        userId: '1',
        ipAddress: '192.168.1.100',
        status: 'Warning',
        details: 'Deleted user: test-user',
        timestamp: new Date(Date.now() - 1000 * 60 * 90)
      },
      {
        auditLogId: '7',
        action: 'UPDATE_ROLE',
        resource: 'Role',
        resourceId: 'role-111',
        username: 'admin',
        userId: '1',
        ipAddress: '192.168.1.100',
        status: 'Success',
        details: 'Updated role permissions',
        timestamp: new Date(Date.now() - 1000 * 60 * 120)
      },
      {
        auditLogId: '8',
        action: 'REVOKE_KEY',
        resource: 'Key',
        resourceId: 'key-222',
        username: 'admin',
        userId: '1',
        ipAddress: '192.168.1.100',
        status: 'Success',
        details: 'Revoked key: old-api-key',
        timestamp: new Date(Date.now() - 1000 * 60 * 180)
      }
    ];
    
    this.auditLogs = mockLogs;
    this.filteredLogs = [...mockLogs];
    
    // Extract unique values for filters
    this.uniqueActions = [...new Set(mockLogs.map(log => log.action))];
    this.uniqueResources = [...new Set(mockLogs.map(log => log.resource))];
    
    this.updatePagination();
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
      if (this.dateFrom) {
        matchesDate = matchesDate && new Date(log.timestamp) >= new Date(this.dateFrom);
      }
      if (this.dateTo) {
        matchesDate = matchesDate && new Date(log.timestamp) <= new Date(this.dateTo);
      }
      
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
    this.totalPages = Math.ceil(this.filteredLogs.length / this.pageSize);
  }

  get paginatedLogs(): AuditLog[] {
    const start = (this.currentPage - 1) * this.pageSize;
    const end = start + this.pageSize;
    return this.filteredLogs.slice(start, end);
  }

  nextPage() {
    if (this.currentPage < this.totalPages) {
      this.currentPage++;
    }
  }

  previousPage() {
    if (this.currentPage > 1) {
      this.currentPage--;
    }
  }

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
    if (action.includes('CREATE')) return '➕';
    if (action.includes('UPDATE')) return '✏️';
    if (action.includes('DELETE')) return '🗑️';
    if (action.includes('RETRIEVE')) return '👁️';
    if (action.includes('REVOKE')) return '🚫';
    if (action.includes('UPLOAD')) return '📤';
    return '📋';
  }

  exportLogs() {
    // TODO: Implement export functionality
    console.log('Exporting audit logs...');
    this.notificationService.info(
      'Yakında Gelecek',
      'Export özelliği şu anda geliştirme aşamasındadır.'
    );
  }

  getSuccessCount(): number {
    return this.filteredLogs.filter(log => log.status === 'Success').length;
  }

  getFailedCount(): number {
    return this.filteredLogs.filter(log => log.status === 'Failed').length;
  }

  getWarningCount(): number {
    return this.filteredLogs.filter(log => log.status === 'Warning').length;
  }
}

