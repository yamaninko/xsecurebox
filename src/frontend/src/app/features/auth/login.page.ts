import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule, FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-login-page',
  templateUrl: './login.page.html',
  styleUrls: ['./login.page.css'],
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    FormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatCheckboxModule,
    MatProgressSpinnerModule,
    MatSnackBarModule
  ]
})
export class LoginPageComponent implements OnInit {
  loginForm!: FormGroup;
  isLoading = false;
  hidePassword = true;
  mfaChallengeId: string | null = null;
  mfaCode = '';

  constructor(
    private fb: FormBuilder,
    private authService: AuthService,
    private router: Router,
    private route: ActivatedRoute,
    private snackBar: MatSnackBar
  ) {}

  ngOnInit(): void {
    localStorage.removeItem('saved_password');
    const savedUsername = localStorage.getItem('saved_username');
    const rememberMe = localStorage.getItem('remember_me') === 'true';

    this.loginForm = this.fb.group({
      username: [savedUsername || '', [Validators.required, Validators.minLength(3)]],
      password: ['', [Validators.required, Validators.minLength(6)]],
      rememberMe: [rememberMe]
    });
  }

  onSubmit(): void {
    if (this.loginForm.invalid) {
      this.loginForm.markAllAsTouched();
      return;
    }

    this.isLoading = true;
    const { username, password, rememberMe } = this.loginForm.value;

    // Save credentials if "Remember Me" is checked
    if (rememberMe) {
      localStorage.setItem('saved_username', username);
      localStorage.setItem('remember_me', 'true');
    } else {
      localStorage.removeItem('saved_username');
      localStorage.removeItem('saved_password');
      localStorage.removeItem('remember_me');
    }

    this.authService.login(username, password).subscribe({
      next: (response) => {
        this.isLoading = false;
        if (response.success && response.data?.requiresMfa) {
          this.mfaChallengeId = response.data.mfaChallengeId;
          this.snackBar.open('MFA kodunu girin', 'Kapat', { duration: 3000 });
        } else if (response.success) {
          this.afterLogin(response.data?.user);
        } else {
          this.showError(response.message || 'Giriş başarısız');
        }
      },
      error: (error) => {
        this.isLoading = false;
        const errorMessage = error.error?.message || 'Giriş sırasında bir hata oluştu';
        this.showError(errorMessage);
      }
    });
  }

  submitMfa(): void {
    if (!this.mfaChallengeId || this.mfaCode.length < 6) {
      return;
    }
    this.isLoading = true;
    this.authService.verifyMfa(this.mfaChallengeId, this.mfaCode).subscribe({
      next: (response) => {
        this.isLoading = false;
        if (response.success) {
          this.afterLogin(response.data?.user);
        } else {
          this.showError('MFA doğrulanamadı');
        }
      },
      error: (error) => {
        this.isLoading = false;
        this.showError(error.error?.error?.message || 'MFA doğrulanamadı');
      }
    });
  }

  private afterLogin(user: { mustChangePassword?: boolean; mustSetupMfa?: boolean } | undefined): void {
    this.snackBar.open('Giriş başarılı!', 'Kapat', { duration: 3000, horizontalPosition: 'end', verticalPosition: 'top' });
    const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
    if (user?.mustChangePassword) {
      this.router.navigate(['/auth/change-password']);
    } else if (user?.mustSetupMfa) {
      this.router.navigate(['/auth/mfa-setup']);
    } else {
      this.router.navigateByUrl(returnUrl || '/dashboard');
    }
  }

  private showError(message: string): void {
    this.snackBar.open(message, 'Kapat', {
      duration: 5000,
      horizontalPosition: 'end',
      verticalPosition: 'top',
      panelClass: ['error-snackbar']
    });
  }

  get usernameError(): string {
    const control = this.loginForm.get('username');
    if (control?.hasError('required')) {
      return 'Kullanıcı adı gereklidir';
    }
    if (control?.hasError('minlength')) {
      return 'Kullanıcı adı en az 3 karakter olmalıdır';
    }
    return '';
  }

  get passwordError(): string {
    const control = this.loginForm.get('password');
    if (control?.hasError('required')) {
      return 'Şifre gereklidir';
    }
    if (control?.hasError('minlength')) {
      return 'Şifre en az 6 karakter olmalıdır';
    }
    return '';
  }
}

