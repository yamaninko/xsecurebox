import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';

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
  imports: [CommonModule]
})
export class CertificatesPageComponent implements OnInit {
  certificates: Certificate[] = [];
  loading = true;

  ngOnInit() {
    this.loadCertificates();
  }

  loadCertificates() {
    this.loading = true;
    setTimeout(() => {
      this.certificates = [
        {
          certificateId: '1',
          name: 'Prod Cert 2025',
          description: 'Production encryption certificate',
          thumbprint: 'SHA256:ABC123...',
          subject: 'CN=SecureBox Production',
          issuer: 'CN=SecureBox CA',
          notBefore: new Date('2025-01-01'),
          notAfter: new Date('2026-01-01'),
          status: 'Active',
          isForEncryption: true,
          uploadedBy: 'admin',
          createdAt: new Date('2025-01-01')
        }
      ];
      this.loading = false;
    }, 500);
  }

  uploadCertificate() {
    alert('Certificate upload modal - TODO');
  }

  revokeCertificate(cert: Certificate) {
    if (confirm(`Revoke certificate ${cert.name}?`)) {
      alert('Certificate revoked!');
    }
  }
}
