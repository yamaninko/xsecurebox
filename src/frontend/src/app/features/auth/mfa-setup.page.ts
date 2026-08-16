import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import * as QRCode from 'qrcode';
import { AuthService } from '../../core/auth/auth.service';
import { NotificationService } from '../../core/services/notification.service';

@Component({
  selector: 'app-mfa-setup-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './mfa-setup.page.html',
  styleUrls: ['./mfa-setup.page.css']
})
export class MfaSetupPageComponent implements OnInit {
  secret = '';
  otpAuthUri = '';
  qrDataUrl = '';
  code = '';
  loading = false;
  username = 'admin';

  constructor(
    private auth: AuthService,
    private router: Router,
    private notify: NotificationService
  ) {}

  ngOnInit(): void {
    this.username = this.auth.getCurrentUser()?.username || 'admin';
    this.auth.setupMfa().subscribe({
      next: async (res) => {
        this.secret = res.data.secret;
        this.otpAuthUri = res.data.otpAuthUri;
        this.qrDataUrl = await QRCode.toDataURL(this.otpAuthUri, {
          width: 220,
          margin: 1,
          errorCorrectionLevel: 'M',
          color: { dark: '#212121', light: '#ffffff' }
        });
      },
      error: () => this.notify.error('Hata', 'MFA kurulumu başlatılamadı')
    });
  }

  copySecret(): void {
    navigator.clipboard.writeText(this.secret).then(() => {
      this.notify.success('Kopyalandı', 'Anahtarı Google Authenticator’a yapıştırın');
    });
  }

  enable(): void {
    this.loading = true;
    this.auth.enableMfa(this.code).subscribe({
      next: () => {
        const user = this.auth.getCurrentUser();
        if (user) {
          user.mfaEnabled = true;
          user.mustSetupMfa = false;
        }
        this.notify.success('MFA', 'İki adımlı doğrulama açıldı');
        this.router.navigate(['/dashboard']);
      },
      error: (err) => {
        this.loading = false;
        this.notify.error('Hata', err.error?.error?.message || 'Kod geçersiz');
      }
    });
  }
}
