import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { NotificationService } from '../../core/services/notification.service';

@Component({
  selector: 'app-change-password-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <section class="change-password">
      <h1>Şifre Değiştir</h1>
      <p>Devam etmek için yeni bir şifre belirleyin.</p>
      <form [formGroup]="form" (ngSubmit)="submit()">
        <label>Mevcut şifre
          <input type="password" formControlName="currentPassword">
        </label>
        <label>Yeni şifre
          <input type="password" formControlName="newPassword">
        </label>
        <label>Yeni şifre (tekrar)
          <input type="password" formControlName="confirmPassword">
        </label>
        <button type="submit" [disabled]="form.invalid || loading">Kaydet</button>
      </form>
    </section>
  `,
  styles: [`
    .change-password { max-width: 420px; margin: 48px auto; display: grid; gap: 16px; }
    form { display: grid; gap: 12px; }
    label { display: grid; gap: 4px; font-size: 14px; }
    input, button { padding: 10px 12px; }
  `]
})
export class ChangePasswordPageComponent {
  loading = false;
  form = this.fb.group({
    currentPassword: ['', Validators.required],
    newPassword: ['', [Validators.required, Validators.minLength(8)]],
    confirmPassword: ['', Validators.required]
  });

  constructor(
    private fb: FormBuilder,
    private auth: AuthService,
    private router: Router,
    private notify: NotificationService
  ) {}

  submit(): void {
    if (this.form.invalid) {
      return;
    }
    this.loading = true;
    const { currentPassword, newPassword, confirmPassword } = this.form.value;
    this.auth.changePassword(currentPassword!, newPassword!, confirmPassword!).subscribe({
      next: () => {
        const user = this.auth.getCurrentUser();
        if (user) {
          user.mustChangePassword = false;
        }
        this.notify.success('Şifre değişti', 'Yeni şifreniz kaydedildi.');
        this.router.navigate(['/dashboard']);
      },
      error: (err) => {
        this.loading = false;
        this.notify.error('Hata', err.error?.error?.message || 'Şifre değiştirilemedi');
      }
    });
  }
}
