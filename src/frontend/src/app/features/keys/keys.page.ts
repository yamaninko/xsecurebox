import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { KeyService } from '../../core/services/key.service';
import { CertificateService } from '../../core/services/certificate.service';
import { NotificationService } from '../../core/services/notification.service';

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
  certificates: { certificateId: string; name: string }[] = [];
  loading = true;
  error: string | null = null;
  searchTerm = '';
  selectedEnvironment = '';
  selectedStatus = '';
  selectedKeyType = '';
  showCreateModal = false;
  newKey = this.emptyKey();
  showRetrieveModal = false;
  selectedKey: Key | null = null;
  retrievePassword = '';
  retrieveReason = '';
  retrievedValue = '';
  clipboardTimer: ReturnType<typeof setTimeout> | null = null;

  constructor(
    private keyService: KeyService,
    private certificateService: CertificateService,
    private notify: NotificationService
  ) {}

  ngOnInit() {
    this.loadKeys();
    this.certificateService.getCertificates().subscribe({
      next: (res) => this.certificates = res.data || [],
      error: () => undefined
    });
  }

  emptyKey() {
    return {
      name: '',
      description: '',
      keyType: 'Password',
      value: '',
      certificateId: '',
      encryptionAlgorithm: 'AES256',
      environmentTag: 'DEV',
      tags: [] as string[],
      tagText: '',
      expiresAt: ''
    };
  }

  loadKeys() {
    this.loading = true;
    this.error = null;
    this.keyService.getKeys().subscribe({
      next: (res) => {
        this.keys = res.data || [];
        this.filteredKeys = [...this.keys];
        this.loading = false;
      },
      error: (err) => {
        this.error = err.error?.error?.message || 'Anahtarlar yüklenemedi';
        this.loading = false;
      }
    });
  }

  applyFilters() {
    this.filteredKeys = this.keys.filter(key => {
      const matchesSearch = !this.searchTerm ||
        key.name.toLowerCase().includes(this.searchTerm.toLowerCase()) ||
        key.description?.toLowerCase().includes(this.searchTerm.toLowerCase());
      return matchesSearch &&
        (!this.selectedEnvironment || key.environmentTag === this.selectedEnvironment) &&
        (!this.selectedStatus || key.status === this.selectedStatus) &&
        (!this.selectedKeyType || key.keyType === this.selectedKeyType);
    });
  }

  clearFilters() {
    this.searchTerm = '';
    this.selectedEnvironment = '';
    this.selectedStatus = '';
    this.selectedKeyType = '';
    this.filteredKeys = [...this.keys];
  }

  openCreateModal() { this.showCreateModal = true; }
  closeCreateModal() { this.showCreateModal = false; this.newKey = this.emptyKey(); }

  createKey() {
    this.keyService.createKey({
      name: this.newKey.name,
      description: this.newKey.description,
      keyType: this.newKey.keyType,
      value: this.newKey.value,
      certificateId: this.newKey.certificateId,
      encryptionAlgorithm: this.newKey.encryptionAlgorithm,
      environmentTag: this.newKey.environmentTag,
      tags: this.newKey.tagText ? this.newKey.tagText.split(',').map(t => t.trim()).filter(Boolean) : [],
      expiresAt: this.newKey.expiresAt || undefined
    }).subscribe({
      next: () => {
        this.notify.success('Anahtar oluşturuldu', this.newKey.name);
        this.closeCreateModal();
        this.loadKeys();
      },
      error: (err) => this.notify.error('Hata', err.error?.error?.message || 'Anahtar oluşturulamadı')
    });
  }

  openRetrieveModal(key: Key) {
    this.selectedKey = key;
    this.showRetrieveModal = true;
    this.retrievePassword = '';
    this.retrieveReason = '';
    this.retrievedValue = '';
  }

  closeRetrieveModal() {
    this.showRetrieveModal = false;
    this.selectedKey = null;
    this.retrievePassword = '';
    this.retrievedValue = '';
    if (this.clipboardTimer) {
      clearTimeout(this.clipboardTimer);
    }
  }

  retrieveKey() {
    if (!this.selectedKey) return;
    this.keyService.retrieveKey(this.selectedKey.keyId, this.retrievePassword, this.retrieveReason).subscribe({
      next: (res) => {
        this.retrievedValue = res.data.value;
        this.notify.success('Anahtar alındı', 'Bu işlem audit kaydına yazıldı.');
      },
      error: (err) => this.notify.error('Hata', err.error?.error?.message || 'Anahtar alınamadı')
    });
  }

  copyRetrievedValue() {
    if (!this.retrievedValue) return;
    navigator.clipboard.writeText(this.retrievedValue).then(() => {
      this.notify.success('Kopyalandı', '30 saniye sonra panodan silinecek.');
      if (this.clipboardTimer) clearTimeout(this.clipboardTimer);
      this.clipboardTimer = setTimeout(() => {
        navigator.clipboard.writeText('').catch(() => undefined);
      }, 30000);
    });
  }

  rotateTarget: Key | null = null;
  rotateValue = '';
  revokeTarget: Key | null = null;
  revokeReason = '';

  rotateKey(key: Key) {
    this.rotateTarget = key;
    this.rotateValue = '';
  }

  confirmRotate() {
    if (!this.rotateTarget || !this.rotateValue) return;
    this.keyService.rotateKey(this.rotateTarget.keyId, this.rotateValue, 'manual-rotate').subscribe({
      next: () => {
        this.notify.success('Döndürüldü', this.rotateTarget!.name);
        this.rotateTarget = null;
        this.loadKeys();
      },
      error: (err) => this.notify.error('Hata', err.error?.error?.message || 'Rotate başarısız')
    });
  }

  revokeKey(key: Key) {
    this.revokeTarget = key;
    this.revokeReason = '';
  }

  confirmRevoke() {
    if (!this.revokeTarget || !this.revokeReason) return;
    this.keyService.revokeKey(this.revokeTarget.keyId, this.revokeReason).subscribe({
      next: () => {
        this.notify.success('İptal edildi', this.revokeTarget!.name);
        this.revokeTarget = null;
        this.loadKeys();
      },
      error: (err) => this.notify.error('Hata', err.error?.error?.message || 'İptal başarısız')
    });
  }

  deleteKey(key: Key) {
    if (!confirm(`"${key.name}" silinsin mi? Önce iptal edilmiş olmalı.`)) return;
    this.keyService.deleteKey(key.keyId).subscribe({
      next: () => { this.notify.success('Silindi', key.name); this.loadKeys(); },
      error: (err) => this.notify.error('Hata', err.error?.error?.message || 'Silinemedi')
    });
  }

  getStatusBadgeClass(status: string): string {
    const classes: Record<string, string> = {
      Active: 'badge-success', Expired: 'badge-warning', Revoked: 'badge-danger', Archived: 'badge-secondary'
    };
    return classes[status] || 'badge-secondary';
  }

  getEnvironmentBadgeClass(env: string): string {
    const classes: Record<string, string> = {
      DEV: 'env-dev', TEST: 'env-test', UAT: 'env-uat', PROD: 'env-prod'
    };
    return classes[env] || 'env-dev';
  }
}
