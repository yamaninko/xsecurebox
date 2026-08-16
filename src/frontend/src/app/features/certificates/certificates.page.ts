import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NotificationService } from '../../core/services/notification.service';
import { CertificateService } from '../../core/services/certificate.service';

interface Certificate {
  certificateId: string;
  name: string;
  description?: string;
  thumbprint: string;
  subject: string;
  issuer: string;
  notBefore: Date;
  notAfter: Date;
  status: string;
  isForEncryption: boolean;
  uploadedBy: string;
  createdAt: Date;
}

@Component({
  selector: 'app-certificates-page',
  templateUrl: './certificates.page.html',
  styleUrls: ['./certificates.page.css'],
  standalone: true,
  imports: [CommonModule, FormsModule]
})
export class CertificatesPageComponent implements OnInit {
  certificates: Certificate[] = [];
  loading = true;
  showUploadModal = false;
  newCertificate = { name: '', description: '', password: '', file: null as File | null };

  constructor(
    private notificationService: NotificationService,
    private certificateService: CertificateService
  ) {}

  ngOnInit() { this.loadCertificates(); }

  loadCertificates() {
    this.loading = true;
    this.certificateService.getCertificates().subscribe({
      next: (res) => { this.certificates = res.data || []; this.loading = false; },
      error: (err) => {
        this.notificationService.error('Hata', err.error?.error?.message || 'Sertifikalar yüklenemedi');
        this.loading = false;
      }
    });
  }

  openUploadModal() { this.showUploadModal = true; }
  closeUploadModal() {
    this.showUploadModal = false;
    this.newCertificate = { name: '', description: '', password: '', file: null };
  }

  onFileSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (file) {
      this.newCertificate.file = file;
      if (!this.newCertificate.name) {
        this.newCertificate.name = file.name.replace(/\.[^/.]+$/, '');
      }
    }
  }

  uploadCertificate() {
    if (!this.newCertificate.file) {
      this.notificationService.error('Dosya Gerekli', 'Lütfen bir sertifika dosyası seçin.');
      return;
    }

    const reader = new FileReader();
    reader.onload = () => {
      const bytes = new Uint8Array(reader.result as ArrayBuffer);
      const payload = {
        name: this.newCertificate.name,
        description: this.newCertificate.description,
        certificateFile: btoa(String.fromCharCode(...bytes)),
        password: this.newCertificate.password || null,
        isForEncryption: true,
        isForSigning: false
      };
      this.certificateService.upload(payload).subscribe({
        next: () => {
          this.notificationService.success('Sertifika Yüklendi', this.newCertificate.name);
          this.closeUploadModal();
          this.loadCertificates();
        },
        error: (err) => this.notificationService.error('Hata', err.error?.error?.message || 'Yükleme başarısız')
      });
    };
    reader.readAsArrayBuffer(this.newCertificate.file);
  }

  async revokeCertificate(cert: Certificate) {
    const confirmed = await this.notificationService.confirm({
      title: 'Sertifikayı İptal Et',
      message: `${cert.name} iptal edilsin mi?`,
      confirmText: 'İptal Et',
      cancelText: 'Vazgeç',
      type: 'danger'
    });
    if (!confirmed) return;
    this.certificateService.revoke(cert.certificateId, 'revoked-from-portal').subscribe({
      next: () => { this.notificationService.success('İptal edildi', cert.name); this.loadCertificates(); },
      error: (err) => this.notificationService.error('Hata', err.error?.error?.message || 'İptal başarısız')
    });
  }

  async deleteCertificate(cert: Certificate) {
    const confirmed = await this.notificationService.confirm({
      title: 'Sertifikayı Sil',
      message: `${cert.name} silinsin mi?`,
      confirmText: 'Sil',
      cancelText: 'Vazgeç',
      type: 'danger'
    });
    if (!confirmed) return;
    this.certificateService.delete(cert.certificateId).subscribe({
      next: () => { this.notificationService.success('Silindi', cert.name); this.loadCertificates(); },
      error: (err) => this.notificationService.error('Hata', err.error?.error?.message || 'Silinemedi')
    });
  }

  getStatusClass(status: string): string {
    switch (status) {
      case 'Active': return 'badge-success';
      case 'Expired': return 'badge-danger';
      case 'Revoked': return 'badge-warning';
      default: return 'badge-secondary';
    }
  }

  isExpiringSoon(notAfter: Date): boolean {
    const thirtyDaysFromNow = new Date();
    thirtyDaysFromNow.setDate(thirtyDaysFromNow.getDate() + 30);
    return new Date(notAfter) < thirtyDaysFromNow;
  }
}
