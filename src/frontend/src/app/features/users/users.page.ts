import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

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
    roles: [] as string[]
  };

  constructor() {}

  ngOnInit() {
    this.loadUsers();
  }

  loadUsers() {
    this.loading = true;
    setTimeout(() => {
      this.users = [
        {
          userId: '1',
          username: 'admin',
          email: 'admin@securebox.local',
          firstName: 'System',
          lastName: 'Administrator',
          isActive: true,
          roles: ['Admin'],
          createdAt: new Date('2025-01-01'),
          lastLoginAt: new Date('2025-11-07')
        },
        {
          userId: '2',
          username: 'john.doe',
          email: 'john@example.com',
          firstName: 'John',
          lastName: 'Doe',
          isActive: true,
          roles: ['Client'],
          createdAt: new Date('2025-10-15'),
          lastLoginAt: new Date('2025-11-06')
        }
      ];
      this.loading = false;
    }, 500);
  }

  openCreateModal() {
    this.showCreateModal = true;
  }

  closeCreateModal() {
    this.showCreateModal = false;
  }

  createUser() {
    console.log('Creating user:', this.newUser);
    alert('User created successfully!');
    this.closeCreateModal();
    this.loadUsers();
  }

  toggleUserStatus(user: User) {
    user.isActive = !user.isActive;
    alert(`User ${user.username} ${user.isActive ? 'activated' : 'deactivated'}`);
  }

  deleteUser(user: User) {
    if (confirm(`Delete user ${user.username}?`)) {
      alert('User deleted!');
      this.loadUsers();
    }
  }
}

