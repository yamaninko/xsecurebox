import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RoleService } from '../../core/services/role.service';
import { NotificationService } from '../../core/services/notification.service';

interface Role {
  roleId: string;
  roleName: string;
  description?: string;
  isSystem: boolean;
  userCount: number;
  permissionCount: number;
  createdAt: Date;
}

@Component({
  selector: 'app-roles-page',
  templateUrl: './roles.page.html',
  styleUrls: ['./roles.page.css'],
  standalone: true,
  imports: [CommonModule, FormsModule]
})
export class RolesPageComponent implements OnInit {
  roles: Role[] = [];
  loading = true;
  showCreate = false;
  newRole = { roleName: '', description: '' };

  constructor(private roleService: RoleService, private notify: NotificationService) {}

  ngOnInit() { this.loadRoles(); }

  loadRoles() {
    this.loading = true;
    this.roleService.getRoles().subscribe({
      next: (res) => { this.roles = res.data || []; this.loading = false; },
      error: () => { this.loading = false; this.notify.error('Hata', 'Roller yüklenemedi'); }
    });
  }

  createRole() {
    if (!this.newRole.roleName.trim()) return;
    this.roleService.create({ roleName: this.newRole.roleName, description: this.newRole.description }).subscribe({
      next: () => {
        this.notify.success('Rol oluşturuldu', this.newRole.roleName);
        this.showCreate = false;
        this.newRole = { roleName: '', description: '' };
        this.loadRoles();
      },
      error: (err) => this.notify.error('Hata', err.error?.error?.message || 'Oluşturulamadı')
    });
  }

  deleteRole(role: Role) {
    if (role.isSystem) {
      this.notify.error('Sistem rolü', 'Silinemez');
      return;
    }
    if (!confirm(`${role.roleName} silinsin mi?`)) return;
    this.roleService.delete(role.roleId).subscribe({
      next: () => this.loadRoles(),
      error: (err) => this.notify.error('Hata', err.error?.error?.message || 'Silinemedi')
    });
  }
}
