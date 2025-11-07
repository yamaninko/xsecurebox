import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

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

  ngOnInit() {
    this.loadRoles();
  }

  loadRoles() {
    this.loading = true;
    setTimeout(() => {
      this.roles = [
        {
          roleId: '1',
          roleName: 'Admin',
          description: 'Full system access',
          isSystem: true,
          userCount: 2,
          permissionCount: 18,
          createdAt: new Date('2025-01-01')
        },
        {
          roleId: '2',
          roleName: 'Client',
          description: 'Standard client access',
          isSystem: true,
          userCount: 10,
          permissionCount: 8,
          createdAt: new Date('2025-01-01')
        }
      ];
      this.loading = false;
    }, 500);
  }

  deleteRole(role: Role) {
    if (role.isSystem) {
      alert('Cannot delete system roles');
      return;
    }
    if (confirm(`Delete role ${role.roleName}?`)) {
      alert('Role deleted!');
    }
  }
}
