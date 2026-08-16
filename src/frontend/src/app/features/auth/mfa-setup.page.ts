import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import * as QRCode from 'qrcode';
import { AuthService } from '../../core/auth/auth.service';
import { NotificationService } from '../../core/services/notification.service';

@Component({
  selector: 'app-mfa-setup-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <section class="mfa">
      <h1>İki adımlı doğrulama (MFA)</h1>
      <p class="lead">
        XSecureBox, zaman tabanlı tek kullanımlık şifre (<strong>TOTP</strong>) kullanır.
        Desteklenen uygulama: <strong>Google Authenticator</strong>
        (Microsoft Authenticator veya Authy de çalışır).
      </p>

      <div class="apps">
        <article class="app recommended">
          <div class="badge">Önerilen</div>
          <h2>Google Authenticator</h2>
          <p>iPhone ve Android. Ücretsiz. QR okutun veya anahtarı yazın.</p>
          <div class="store">
            <a href="https://apps.apple.com/app/google-authenticator/id388497605" target="_blank" rel="noopener">App Store</a>
            <a href="https://play.google.com/store/apps/details?id=com.google.android.apps.authenticator2" target="_blank" rel="noopener">Google Play</a>
          </div>
        </article>
        <article class="app">
          <h2>Microsoft Authenticator</h2>
          <p>Kurumsal telefonlarda yaygın. Aynı QR / aynı 6 haneli kod.</p>
          <div class="store">
            <a href="https://apps.apple.com/app/microsoft-authenticator/id983156458" target="_blank" rel="noopener">App Store</a>
            <a href="https://play.google.com/store/apps/details?id=com.azure.authenticator" target="_blank" rel="noopener">Google Play</a>
          </div>
        </article>
      </div>

      <h2 class="steps-title">Google Authenticator ekran sırası</h2>
      <div class="phones">
        <figure>
          <div class="phone">
            <div class="phone-bar">Google Authenticator</div>
            <div class="phone-body">
              <p class="hint">Hesap yok</p>
              <div class="fab">+</div>
            </div>
          </div>
          <figcaption>1. Uygulamayı açın, sağ alttaki <strong>+</strong> düğmesine basın.</figcaption>
        </figure>
        <figure>
          <div class="phone">
            <div class="phone-bar">Hesap ekle</div>
            <div class="phone-body menu">
              <div class="row">QR kodu tarayın</div>
              <div class="row alt">Kurulum anahtarı girin</div>
            </div>
          </div>
          <figcaption>2. <strong>QR kodu tarayın</strong> deyin. Kamerasız telefonda “Kurulum anahtarı”.</figcaption>
        </figure>
        <figure>
          <div class="phone">
            <div class="phone-bar">XSecureBox</div>
            <div class="phone-body code">
              <div class="digits">482 193</div>
              <small>admin · 30 sn</small>
            </div>
          </div>
          <figcaption>3. Uygulamadaki <strong>6 haneli kodu</strong> aşağıdaki kutuya yazın.</figcaption>
        </figure>
      </div>

      <div class="enroll" *ngIf="qrDataUrl">
        <div class="qr-wrap">
          <img [src]="qrDataUrl" width="240" height="240" alt="XSecureBox MFA QR kodu">
          <p>Google Authenticator ile bu karekodu okutun.</p>
        </div>
        <div class="manual">
          <h3>QR çalışmazsa</h3>
          <p>Uygulamada hesap adı <code>XSecureBox ({{ username }})</code>, tür <strong>Zamana dayalı</strong>.</p>
          <label>Kurulum anahtarı
            <input readonly [value]="secret" (click)="copySecret()">
          </label>
          <button type="button" class="link" (click)="copySecret()">Anahtarı kopyala</button>

          <label>Uygulamadaki 6 haneli kod
            <input [(ngModel)]="code" maxlength="6" inputmode="numeric" autocomplete="one-time-code" placeholder="000000">
          </label>
          <button type="button" class="primary" (click)="enable()" [disabled]="code.length < 6 || loading">
            Etkinleştir
          </button>
        </div>
      </div>
    </section>
  `,
  styles: [`
    .mfa { max-width: 920px; margin: 32px auto 64px; padding: 0 16px; color: #0f172a; }
    h1 { font-size: 28px; margin: 0 0 8px; }
    .lead { color: #334155; line-height: 1.5; }
    .apps { display: grid; grid-template-columns: repeat(auto-fit, minmax(260px, 1fr)); gap: 12px; margin: 20px 0; }
    .app { border: 1px solid #e2e8f0; border-radius: 12px; padding: 16px; background: #fff; }
    .app.recommended { border-color: #0d9488; box-shadow: 0 0 0 1px #0d9488; }
    .badge { display: inline-block; background: #0d9488; color: #fff; font-size: 11px; padding: 2px 8px; border-radius: 999px; margin-bottom: 8px; }
    .app h2 { margin: 0 0 6px; font-size: 18px; }
    .store { display: flex; gap: 12px; margin-top: 10px; }
    .store a { color: #0f766e; font-weight: 600; }
    .steps-title { margin: 28px 0 12px; font-size: 20px; }
    .phones { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 16px; }
    figure { margin: 0; }
    figcaption { font-size: 13px; color: #475569; margin-top: 8px; min-height: 48px; }
    .phone { width: 180px; height: 280px; border: 10px solid #0f172a; border-radius: 24px; background: #f8fafc; overflow: hidden; }
    .phone-bar { background: #111827; color: #fff; font-size: 12px; padding: 10px 8px; text-align: center; }
    .phone-body { position: relative; height: calc(100% - 38px); padding: 16px 10px; }
    .phone-body.menu .row { background: #fff; border: 1px solid #cbd5e1; border-radius: 8px; padding: 12px; margin-bottom: 8px; font-size: 13px; }
    .phone-body.menu .row.alt { background: #ecfeff; }
    .phone-body.code { display: grid; place-items: center; text-align: center; }
    .digits { font-size: 28px; letter-spacing: 2px; font-weight: 700; color: #0f766e; }
    .hint { color: #64748b; font-size: 13px; }
    .fab { position: absolute; right: 14px; bottom: 14px; width: 40px; height: 40px; border-radius: 50%; background: #0d9488; color: #fff; display: grid; place-items: center; font-size: 24px; }
    .enroll { display: grid; grid-template-columns: 280px 1fr; gap: 24px; margin-top: 28px; align-items: start; }
    .qr-wrap { text-align: center; background: #fff; border: 1px solid #e2e8f0; border-radius: 12px; padding: 16px; }
    .qr-wrap img { display: block; margin: 0 auto 8px; }
    .manual { display: grid; gap: 10px; }
    label { display: grid; gap: 4px; font-size: 14px; }
    input { padding: 10px 12px; font-size: 16px; letter-spacing: 1px; }
    .primary { background: #0d9488; color: #fff; border: 0; padding: 12px 16px; border-radius: 8px; font-weight: 600; }
    .link { background: none; border: 0; color: #0f766e; text-align: left; padding: 0; }
    @media (max-width: 720px) { .enroll { grid-template-columns: 1fr; } }
  `]
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
          width: 240,
          margin: 2,
          errorCorrectionLevel: 'M'
        });
      },
      error: () => this.notify.error('Hata', 'MFA kurulumu başlatılamadı')
    });
  }

  copySecret(): void {
    navigator.clipboard.writeText(this.secret).then(() => {
      this.notify.success('Kopyalandı', 'Kurulum anahtarını Authenticator’a yapıştırın');
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
        this.notify.success('MFA', 'Google Authenticator ile doğrulama açıldı');
        this.router.navigate(['/dashboard']);
      },
      error: (err) => {
        this.loading = false;
        this.notify.error('Hata', err.error?.error?.message || 'Kod geçersiz');
      }
    });
  }
}
