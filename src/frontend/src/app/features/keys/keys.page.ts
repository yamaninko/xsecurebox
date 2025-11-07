import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { environment } from '../../../environments/environment';

interface Key {
  keyId: string;
  name: string;
  description?: string;
  keyType: string;
  encryptionAlgorithm: string;
  environmentTag: string;
  tags?: string[];
  status: string;
  version: number;
  validFrom: Date;
  validTo?: Date;
  expiresAt?: Date;
  certificateId: string;
  certificateName: string;
  ownerUsername: string;
  createdAt: Date;
  lastAccessedAt?: Date;
  accessCount: number;
}

@Component({
  selector: 'app-keys-page',
  templateUrl: './keys.page.html',
  styleUrls: ['./keys.page.css'],
  standalone: true,
  imports: [CommonModule, FormsModule]
})
export class KeysPageComponent implements OnInit {
  keys: Key[] = [];
  filteredKeys: Key[] = [];
  loading = true;
  error: string | null = null;

  // Filters
  searchTerm = '';
  selectedEnvironment = '';
  selectedStatus = '';
  selectedKeyType = '';

  // Create Key Modal
  showCreateModal = false;
  newKey = {
    name: '',
    description: '',
    keyType: 'Password',
    value: '',
    certificateId: '',
    encryptionAlgorithm: 'AES256',
    environmentTag: 'DEV',
    tags: [] as string[],
    expiresAt: ''
  };

  // Retrieve Key Modal
  showRetrieveModal = false;
  selectedKey: Key | null = null;
  retrievePassword = '';
  retrievedValue = '';

  constructor(private http: HttpClient) {}

  ngOnInit() {
    this.loadKeys();
  }

  loadKeys() {
    this.loading = true;
    this.error = null;

    // Mock data for now
    setTimeout(() => {
      this.keys = [
        {
          keyId: '1',
          name: 'API_KEY_PRODUCTION',
          description: 'Production API key for external services',
          keyType: 'ApiKey',
          encryptionAlgorithm: 'AES256',
          environmentTag: 'PROD',
          tags: ['api', 'production', 'critical'],
          status: 'Active',
          version: 1,
          validFrom: new Date('2025-01-01'),
          expiresAt: new Date('2026-01-01'),
          certificateId: 'cert-1',
          certificateName: 'Prod Cert 2025',
          ownerUsername: 'admin',
          createdAt: new Date('2025-01-01'),
          lastAccessedAt: new Date('2025-11-07'),
          accessCount: 42
        },
        {
          keyId: '2',
          name: 'DATABASE_PASSWORD_DEV',
          description: 'Development database password',
          keyType: 'Password',
          encryptionAlgorithm: 'AES256',
          environmentTag: 'DEV',
          tags: ['database', 'dev'],
          status: 'Active',
          version: 2,
          validFrom: new Date('2025-11-01'),
          certificateId: 'cert-2',
          certificateName: 'Dev Cert 2025',
          ownerUsername: 'john.doe',
          createdAt: new Date('2025-10-15'),
          lastAccessedAt: new Date('2025-11-06'),
          accessCount: 12
        },
        {
          keyId: '3',
          name: 'STRIPE_SECRET_KEY',
          description: 'Stripe payment gateway secret',
          keyType: 'Secret',
          encryptionAlgorithm: 'AES256',
          environmentTag: 'PROD',
          tags: ['payment', 'stripe', 'production'],
          status: 'Active',
          version: 1,
          validFrom: new Date('2025-09-01'),
          certificateId: 'cert-1',
          certificateName: 'Prod Cert 2025',
          ownerUsername: 'admin',
          createdAt: new Date('2025-09-01'),
          accessCount: 8
        }
      ];
      this.filteredKeys = [...this.keys];
      this.loading = false;
    }, 500);
  }

  applyFilters() {
    this.filteredKeys = this.keys.filter(key => {
      const matchesSearch = !this.searchTerm || 
        key.name.toLowerCase().includes(this.searchTerm.toLowerCase()) ||
        key.description?.toLowerCase().includes(this.searchTerm.toLowerCase());
      
      const matchesEnvironment = !this.selectedEnvironment || 
        key.environmentTag === this.selectedEnvironment;
      
      const matchesStatus = !this.selectedStatus || 
        key.status === this.selectedStatus;
      
      const matchesKeyType = !this.selectedKeyType || 
        key.keyType === this.selectedKeyType;

      return matchesSearch && matchesEnvironment && matchesStatus && matchesKeyType;
    });
  }

  clearFilters() {
    this.searchTerm = '';
    this.selectedEnvironment = '';
    this.selectedStatus = '';
    this.selectedKeyType = '';
    this.filteredKeys = [...this.keys];
  }

  openCreateModal() {
    this.showCreateModal = true;
  }

  closeCreateModal() {
    this.showCreateModal = false;
    this.resetNewKey();
  }

  resetNewKey() {
    this.newKey = {
      name: '',
      description: '',
      keyType: 'Password',
      value: '',
      certificateId: '',
      encryptionAlgorithm: 'AES256',
      environmentTag: 'DEV',
      tags: [],
      expiresAt: ''
    };
  }

  createKey() {
    // TODO: API call
    console.log('Creating key:', this.newKey);
    alert('Key created successfully!');
    this.closeCreateModal();
    this.loadKeys();
  }

  openRetrieveModal(key: Key) {
    this.selectedKey = key;
    this.showRetrieveModal = true;
    this.retrievePassword = '';
    this.retrievedValue = '';
  }

  closeRetrieveModal() {
    this.showRetrieveModal = false;
    this.selectedKey = null;
    this.retrievePassword = '';
    this.retrievedValue = '';
  }

  retrieveKey() {
    // TODO: API call with triple auth
    console.log('Retrieving key:', this.selectedKey?.keyId, 'with password:', this.retrievePassword);
    
    // Mock retrieved value
    this.retrievedValue = 'SuperSecretValue123!';
    setTimeout(() => {
      alert('Key retrieved successfully! Value copied to clipboard.');
    }, 500);
  }

  rotateKey(key: Key) {
    if (confirm(`Are you sure you want to rotate "${key.name}"? This will create a new version.`)) {
      // TODO: API call
      console.log('Rotating key:', key.keyId);
      alert('Key rotated successfully!');
      this.loadKeys();
    }
  }

  revokeKey(key: Key) {
    const reason = prompt(`Enter reason for revoking "${key.name}":`);
    if (reason) {
      // TODO: API call
      console.log('Revoking key:', key.keyId, 'Reason:', reason);
      alert('Key revoked successfully!');
      this.loadKeys();
    }
  }

  deleteKey(key: Key) {
    if (confirm(`Are you sure you want to delete "${key.name}"? This action cannot be undone.`)) {
      // TODO: API call
      console.log('Deleting key:', key.keyId);
      alert('Key deleted successfully!');
      this.loadKeys();
    }
  }

  getStatusBadgeClass(status: string): string {
    const classes: Record<string, string> = {
      'Active': 'badge-success',
      'Expired': 'badge-warning',
      'Revoked': 'badge-danger',
      'Archived': 'badge-secondary'
    };
    return classes[status] || 'badge-secondary';
  }

  getEnvironmentBadgeClass(env: string): string {
    const classes: Record<string, string> = {
      'DEV': 'env-dev',
      'TEST': 'env-test',
      'UAT': 'env-uat',
      'PROD': 'env-prod'
    };
    return classes[env] || 'env-dev';
  }
}

