import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { NotificationService } from '../../core/services/notification.service';

@Component({
  selector: 'app-mfa-setup-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <section class="mfa">
      <h1>MFA kurulumu</h1>
      <p>Authenticator uygulamasına bu anahtarı ekleyin, sonra 6 haneli kodu girin.</p>
      <pre *ngIf="secret">{{ secret }}</pre>
      <p class="uri" *ngIf="otpAuthUri">{{ otpAuthUri }}</p>
      <label>Kod
        <input [(ngModel)]="code" maxlength="6" autocomplete="one-time-code">
      </label>
      <button type="button" (click)="enable()" [disabled]="code.length < 6 || loading">Etkinleştir</button>
    </section>
  `,
  styles: [`
    .mfa { max-width: 480px; margin: 48px auto; display: grid; gap: 12px; }
    pre, .uri { word-break: break-all; background: #f4f4f4; padding: 8px; font-size: 13px; }
    input, button { padding: 10px 12px; }
  `]
})
export class MfaSetupPageComponent implements OnInit {
  secret = '';
  otpAuthUri = '';
  code = '';
  loading = false;

  constructor(
    private auth: AuthService,
    private router: Router,
    private notify: NotificationService
  ) {}

  ngOnInit(): void {
    this.auth.setupMfa().subscribe({
      next: (res) => {
        this.secret = res.data.secret;
        this.otpAuthUri = res.data.otpAuthUri;
      },
      error: () => this.notify.error('Hata', 'MFA kurulumu başlatılamadı')
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
