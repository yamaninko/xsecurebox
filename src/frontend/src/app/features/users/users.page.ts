import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NotificationService } from '../../core/services/notification.service';
import { UserService } from '../../core/services/user.service';
import { RoleService } from '../../core/services/role.service';

interface User {
  userId: string;
  username: string;
  email: string;
  firstName?: string;
  lastName?: string;
  isActive: boolean;
  roles: string[];
  createdAt: Date;
  lastLoginAt?: Date;
}

@Component({
  selector: 'app-users-page',
  templateUrl: './users.page.html',
  styleUrls: ['./users.page.css'],
  standalone: true,
  imports: [CommonModule, FormsModule]
})
export class UsersPageComponent implements OnInit {
  users: User[] = [];
  loading = true;
  showCreateModal = false;
  
  newUser = {
    username: '',
    email: '',
    password: '',
    firstName: '',
    lastName: '',
    roles: [] as string[],
    roleIds: [] as string[]
  };

  roles: { roleId: string; roleName: string }[] = [];

  constructor(
    private notificationService: NotificationService,
    private userService: UserService,
    private roleService: RoleService
  ) {}

  ngOnInit() {
    this.loadUsers();
    this.roleService.getRoles().subscribe({
      next: (res) => this.roles = res.data || [],
      error: () => undefined
    });
  }

  loadUsers() {
    this.loading = true;
    this.userService.getUsers().subscribe({
      next: (res) => { this.users = res.data || []; this.loading = false; },
      error: () => { this.loading = false; this.notificationService.error('Hata', 'Kullanıcılar yüklenemedi'); }
    });
  }

  toggleRole(roleId: string, event: Event) {
    const checked = (event.target as HTMLInputElement).checked;
    if (checked) {
      this.newUser.roleIds = [...this.newUser.roleIds, roleId];
    } else {
      this.newUser.roleIds = this.newUser.roleIds.filter(id => id !== roleId);
    }
  }

  openCreateModal() {
    this.showCreateModal = true;
  }

  closeCreateModal() {
    this.showCreateModal = false;
  }

  createUser() {
    this.userService.create({
      username: this.newUser.username,
      email: this.newUser.email,
      password: this.newUser.password,
      firstName: this.newUser.firstName,
      lastName: this.newUser.lastName,
      roleIds: this.newUser.roleIds
    }).subscribe({
      next: () => {
        this.notificationService.success('Kullanıcı Oluşturuldu', this.newUser.username);
        this.closeCreateModal();
        this.loadUsers();
      },
      error: (err) => this.notificationService.error('Hata', err.error?.error?.message || 'Oluşturulamadı')
    });
  }

  async toggleUserStatus(user: User) {
    const action = user.isActive ? 'pasifleştirmek' : 'aktifleştirmek';
    const confirmed = await this.notificationService.confirm({
      title: 'Kullanıcı Durumunu Değiştir',
      message: `${user.username} kullanıcısını ${action} istediğinizden emin misiniz?`,
      confirmText: user.isActive ? 'Pasifleştir' : 'Aktifleştir',
      type: user.isActive ? 'warning' : 'info'
    });

    if (confirmed) {
      this.userService.update(user.userId, { isActive: !user.isActive }).subscribe({
        next: () => { this.notificationService.success('Durum Değiştirildi', user.username); this.loadUsers(); },
        error: (err) => this.notificationService.error('Hata', err.error?.error?.message || 'Güncellenemedi')
      });
    }
  }

  async deleteUser(user: User) {
    const confirmed = await this.notificationService.confirm({
      title: 'Kullanıcıyı Sil',
      message: `${user.username} kullanıcısını silmek istediğinizden emin misiniz? Bu işlem geri alınamaz!`,
      confirmText: 'Sil',
      cancelText: 'İptal',
      type: 'danger'
    });

    if (confirmed) {
      this.userService.delete(user.userId).subscribe({
        next: () => { this.notificationService.success('Kullanıcı Silindi', user.username); this.loadUsers(); },
        error: (err) => this.notificationService.error('Hata', err.error?.error?.message || 'Silinemedi')
      });
    }
  }

  generateStrongPassword(): string {
    const length = 12;
    const uppercase = 'ABCDEFGHJKLMNPQRSTUVWXYZ'; // Exclude I, O for clarity
    const lowercase = 'abcdefghijkmnopqrstuvwxyz'; // Exclude l for clarity
    const numbers = '23456789'; // Exclude 0, 1 for clarity
    const symbols = '!@#$%&*-_+=?';
    
    // Ensure at least one character from each category
    let password = '';
    password += uppercase[Math.floor(Math.random() * uppercase.length)];
    password += lowercase[Math.floor(Math.random() * lowercase.length)];
    password += numbers[Math.floor(Math.random() * numbers.length)];
    password += symbols[Math.floor(Math.random() * symbols.length)];
    
    // Fill the rest randomly from all categories
    const allChars = uppercase + lowercase + numbers + symbols;
    for (let i = password.length; i < length; i++) {
      password += allChars[Math.floor(Math.random() * allChars.length)];
    }
    
    // Shuffle the password to avoid predictable pattern
    return password.split('').sort(() => Math.random() - 0.5).join('');
  }

  suggestPassword() {
    this.newUser.password = this.generateStrongPassword();
  }

  copyPassword() {
    if (this.newUser.password) {
      navigator.clipboard.writeText(this.newUser.password).then(() => {
        this.notificationService.success(
          'Kopyalandı',
          'Şifre panoya kopyalandı!'
        );
      }).catch(err => {
        console.error('Kopyalama hatası:', err);
        this.notificationService.error(
          'Kopyalama Hatası',
          'Şifre kopyalanamadı. Lütfen manuel olarak kopyalayın.'
        );
      });
    }
  }

  getPasswordStrength(): string {
    const password = this.newUser.password;
    if (!password) return 'strength-none';
    
    let strength = 0;
    
    // Length check
    if (password.length >= 8) strength++;
    if (password.length >= 12) strength++;
    
    // Character variety checks
    if (/[a-z]/.test(password)) strength++;
    if (/[A-Z]/.test(password)) strength++;
    if (/[0-9]/.test(password)) strength++;
    if (/[^a-zA-Z0-9]/.test(password)) strength++;
    
    if (strength <= 2) return 'strength-weak';
    if (strength <= 4) return 'strength-medium';
    return 'strength-strong';
  }

  getPasswordStrengthText(): string {
    const strength = this.getPasswordStrength();
    switch (strength) {
      case 'strength-weak': return '🔴 Zayıf';
      case 'strength-medium': return '🟡 Orta';
      case 'strength-strong': return '🟢 Güçlü';
      default: return '';
    }
  }
}

