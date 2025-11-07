import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NotificationService } from '../../core/services/notification.service';

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

  newCertificate = {
    name: '',
    description: '',
    file: null as File | null
  };

  constructor(private notificationService: NotificationService) {}

  ngOnInit() {
    this.loadCertificates();
  }

  loadCertificates() {
    this.loading = true;
    setTimeout(() => {
      this.certificates = [
        {
          certificateId: '1',
          name: 'Production SSL Certificate',
          description: 'Production encryption certificate for secure communications',
          thumbprint: 'SHA256:ABC123DEF456...',
          subject: 'CN=SecureBox Production',
          issuer: 'CN=SecureBox Certificate Authority',
          notBefore: new Date('2025-01-01'),
          notAfter: new Date('2026-01-01'),
          status: 'Active',
          isForEncryption: true,
          uploadedBy: 'admin',
          createdAt: new Date('2025-01-01')
        },
        {
          certificateId: '2',
          name: 'Development Certificate',
          description: 'Development environment certificate',
          thumbprint: 'SHA256:XYZ789GHI012...',
          subject: 'CN=SecureBox Development',
          issuer: 'CN=SecureBox Certificate Authority',
          notBefore: new Date('2025-01-01'),
          notAfter: new Date('2025-12-31'),
          status: 'Active',
          isForEncryption: true,
          uploadedBy: 'admin',
          createdAt: new Date('2025-01-15')
        }
      ];
      this.loading = false;
    }, 500);
  }

  openUploadModal() {
    this.showUploadModal = true;
  }

  closeUploadModal() {
    this.showUploadModal = false;
    this.newCertificate = {
      name: '',
      description: '',
      file: null
    };
  }

  onFileSelected(event: any) {
    const file = event.target.files[0];
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

    console.log('Uploading certificate:', this.newCertificate);
    this.notificationService.success(
      'Sertifika Yüklendi',
      `${this.newCertificate.name} başarıyla yüklendi!`
    );
    this.closeUploadModal();
    this.loadCertificates();
  }

  async revokeCertificate(cert: Certificate) {
    const confirmed = await this.notificationService.confirm({
      title: 'Sertifikayı İptal Et',
      message: `${cert.name} sertifikasını iptal etmek istediğinizden emin misiniz? Bu işlem geri alınamaz ve tüm ilişkili anahtarlar kullanılamaz hale gelecektir!`,
      confirmText: 'İptal Et',
      cancelText: 'Vazgeç',
      type: 'danger'
    });

    if (confirmed) {
      this.notificationService.success(
        'Sertifika İptal Edildi',
        `${cert.name} başarıyla iptal edildi.`
      );
      this.loadCertificates();
    }
  }

  async deleteCertificate(cert: Certificate) {
    const confirmed = await this.notificationService.confirm({
      title: 'Sertifikayı Sil',
      message: `${cert.name} sertifikasını silmek istediğinizden emin misiniz? Bu işlem geri alınamaz!`,
      confirmText: 'Sil',
      cancelText: 'Vazgeç',
      type: 'danger'
    });

    if (confirmed) {
      this.notificationService.success(
        'Sertifika Silindi',
        `${cert.name} başarıyla silindi.`
      );
      this.loadCertificates();
    }
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
