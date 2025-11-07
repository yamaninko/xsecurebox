import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiClientService, ApiClient, CreateApiClientRequest } from '../../core/services/api-client.service';
import { NotificationService } from '../../core/services/notification.service';

@Component({
  selector: 'app-api-clients-page',
  templateUrl: './api-clients.page.html',
  styleUrls: ['./api-clients.page.css'],
  standalone: true,
  imports: [CommonModule, FormsModule]
})
export class ApiClientsPageComponent implements OnInit {
  clients: ApiClient[] = [];
  loading = true;
  
  // Modals
  showCreateModal = false;
  showCredentialsModal = false;
  showTokenTestModal = false;
  
  // Create Client
  newClient: CreateApiClientRequest = {
    clientName: '',
    description: '',
    scopes: [],
    rateLimitPerMinute: 60,
    rateLimitPerHour: 1000
  };
  
  availableScopes = [
    { value: 'keys:read', label: 'Keys - Okuma', description: 'Anahtarları görüntüle' },
    { value: 'keys:write', label: 'Keys - Yazma', description: 'Anahtar oluştur/güncelle' },
    { value: 'keys:delete', label: 'Keys - Silme', description: 'Anahtar sil' },
    { value: 'certs:read', label: 'Certificates - Okuma', description: 'Sertifikaları görüntüle' },
    { value: 'certs:write', label: 'Certificates - Yazma', description: 'Sertifika yükle' },
    { value: 'audit:read', label: 'Audit - Okuma', description: 'Denetim kayıtlarını görüntüle' }
  ];
  
  // Credentials Display
  currentCredentials = {
    clientId: '',
    clientSecret: '',
    apiKey: '',
    clientName: ''
  };
  
  // Token Test
  tokenTest = {
    clientId: '',
    clientSecret: '',
    scope: '',
    result: null as any,
    loading: false
  };

  constructor(
    private apiClientService: ApiClientService,
    private notificationService: NotificationService
  ) {}

  ngOnInit() {
    this.loadClients();
  }

  loadClients() {
    this.loading = true;
    this.apiClientService.getAllClients().subscribe({
      next: (response) => {
        if (response.success) {
          this.clients = response.data;
        }
        this.loading = false;
      },
      error: (error) => {
        console.error('Error loading clients:', error);
        this.notificationService.error('Hata', 'Client\'lar yüklenemedi');
        this.loading = false;
      }
    });
  }

  // Create Client
  openCreateModal() {
    this.showCreateModal = true;
    this.newClient = {
      clientName: '',
      description: '',
      scopes: [],
      rateLimitPerMinute: 60,
      rateLimitPerHour: 1000
    };
  }

  closeCreateModal() {
    this.showCreateModal = false;
  }

  toggleScope(scope: string) {
    const index = this.newClient.scopes.indexOf(scope);
    if (index > -1) {
      this.newClient.scopes.splice(index, 1);
    } else {
      this.newClient.scopes.push(scope);
    }
  }

  isScopeSelected(scope: string): boolean {
    return this.newClient.scopes.includes(scope);
  }

  createClient() {
    if (!this.newClient.clientName || this.newClient.scopes.length === 0) {
      this.notificationService.error('Gerekli Alanlar', 'Client adı ve en az bir scope seçilmelidir');
      return;
    }

    this.apiClientService.createClient(this.newClient).subscribe({
      next: (response) => {
        if (response.success) {
          this.notificationService.success('Client Oluşturuldu', `${this.newClient.clientName} başarıyla oluşturuldu!`);
          
          // Show credentials
          this.currentCredentials = {
            clientId: response.data.client.clientIdString,
            clientSecret: response.data.clientSecret,
            apiKey: response.data.client.apiKey,
            clientName: response.data.client.clientName
          };
          this.showCredentialsModal = true;
          this.closeCreateModal();
          this.loadClients();
        }
      },
      error: (error) => {
        console.error('Error creating client:', error);
        this.notificationService.error('Hata', 'Client oluşturulamadı');
      }
    });
  }

  // Credentials Modal
  closeCredentialsModal() {
    this.showCredentialsModal = false;
  }

  copyToClipboard(text: string, label: string) {
    navigator.clipboard.writeText(text).then(() => {
      this.notificationService.success('Kopyalandı', `${label} panoya kopyalandı!`);
    });
  }

  // Client Actions
  async toggleClientStatus(client: ApiClient) {
    const action = client.isActive ? 'devre dışı bırakmak' : 'aktifleştirmek';
    const confirmed = await this.notificationService.confirm({
      title: 'Durum Değiştir',
      message: `${client.clientName} client'ını ${action} istediğinizden emin misiniz?`,
      confirmText: client.isActive ? 'Devre Dışı Bırak' : 'Aktifleştir',
      type: client.isActive ? 'warning' : 'info'
    });

    if (confirmed) {
      this.apiClientService.updateClient(client.clientId, { isActive: !client.isActive }).subscribe({
        next: (response) => {
          if (response.success) {
            this.notificationService.success('Durum Değiştirildi', `${client.clientName} ${!client.isActive ? 'aktifleştirildi' : 'devre dışı bırakıldı'}`);
            this.loadClients();
          }
        },
        error: (error) => {
          console.error('Error updating client:', error);
          this.notificationService.error('Hata', 'Client güncellenemedi');
        }
      });
    }
  }

  async regenerateSecret(client: ApiClient) {
    const confirmed = await this.notificationService.confirm({
      title: 'Secret Yenile',
      message: `${client.clientName} için yeni bir Client Secret oluşturulacak. Eski secret kullanılamaz hale gelecek!`,
      confirmText: 'Yenile',
      type: 'warning'
    });

    if (confirmed) {
      this.apiClientService.regenerateSecret(client.clientId).subscribe({
        next: (response) => {
          if (response.success) {
            this.currentCredentials = {
              clientId: response.data.clientId,
              clientSecret: response.data.clientSecret,
              apiKey: '',
              clientName: client.clientName
            };
            this.showCredentialsModal = true;
            this.notificationService.success('Secret Yenilendi', 'Yeni secret oluşturuldu');
          }
        },
        error: (error) => {
          console.error('Error regenerating secret:', error);
          this.notificationService.error('Hata', 'Secret yenilenemedi');
        }
      });
    }
  }

  async regenerateApiKey(client: ApiClient) {
    const confirmed = await this.notificationService.confirm({
      title: 'API Key Yenile',
      message: `${client.clientName} için yeni bir API Key oluşturulacak. Eski key kullanılamaz hale gelecek!`,
      confirmText: 'Yenile',
      type: 'warning'
    });

    if (confirmed) {
      this.apiClientService.regenerateApiKey(client.clientId).subscribe({
        next: (response) => {
          if (response.success) {
            this.currentCredentials = {
              clientId: client.clientIdString,
              clientSecret: '',
              apiKey: response.data.apiKey,
              clientName: client.clientName
            };
            this.showCredentialsModal = true;
            this.notificationService.success('API Key Yenilendi', 'Yeni API key oluşturuldu');
          }
        },
        error: (error) => {
          console.error('Error regenerating API key:', error);
          this.notificationService.error('Hata', 'API key yenilenemedi');
        }
      });
    }
  }

  async revokeClient(client: ApiClient) {
    const confirmed = await this.notificationService.confirm({
      title: 'Client İptal Et',
      message: `${client.clientName} iptal edilecek ve tüm erişim hakları kaldırılacak. Bu işlem geri alınamaz!`,
      confirmText: 'İptal Et',
      type: 'danger'
    });

    if (confirmed) {
      this.apiClientService.revokeClient(client.clientId, 'Revoked by admin').subscribe({
        next: (response) => {
          if (response.success) {
            this.notificationService.success('Client İptal Edildi', `${client.clientName} iptal edildi`);
            this.loadClients();
          }
        },
        error: (error) => {
          console.error('Error revoking client:', error);
          this.notificationService.error('Hata', 'Client iptal edilemedi');
        }
      });
    }
  }

  async deleteClient(client: ApiClient) {
    const confirmed = await this.notificationService.confirm({
      title: 'Client Sil',
      message: `${client.clientName} kalıcı olarak silinecek! Bu işlem geri alınamaz!`,
      confirmText: 'Sil',
      type: 'danger'
    });

    if (confirmed) {
      this.apiClientService.deleteClient(client.clientId).subscribe({
        next: (response) => {
          if (response.success) {
            this.notificationService.success('Client Silindi', `${client.clientName} silindi`);
            this.loadClients();
          }
        },
        error: (error) => {
          console.error('Error deleting client:', error);
          this.notificationService.error('Hata', 'Client silinemedi');
        }
      });
    }
  }

  // Token Test
  openTokenTestModal() {
    this.showTokenTestModal = true;
    this.tokenTest = {
      clientId: '',
      clientSecret: '',
      scope: '',
      result: null,
      loading: false
    };
  }

  closeTokenTestModal() {
    this.showTokenTestModal = false;
  }

  testToken() {
    if (!this.tokenTest.clientId || !this.tokenTest.clientSecret) {
      this.notificationService.error('Gerekli Alanlar', 'Client ID ve Secret gereklidir');
      return;
    }

    this.tokenTest.loading = true;
    this.tokenTest.result = null;

    this.apiClientService.testToken({
      clientId: this.tokenTest.clientId,
      clientSecret: this.tokenTest.clientSecret,
      scope: this.tokenTest.scope
    }).subscribe({
      next: (response) => {
        this.tokenTest.result = {
          success: true,
          data: response
        };
        this.tokenTest.loading = false;
        this.notificationService.success('Token Alındı', 'Access token başarıyla oluşturuldu!');
      },
      error: (error) => {
        this.tokenTest.result = {
          success: false,
          error: error.error || error
        };
        this.tokenTest.loading = false;
        this.notificationService.error('Token Hatası', 'Token alınamadı');
      }
    });
  }

  getStatusBadgeClass(client: ApiClient): string {
    if (client.revokedAt) return 'status-revoked';
    return client.isActive ? 'status-active' : 'status-inactive';
  }

  getStatusText(client: ApiClient): string {
    if (client.revokedAt) return 'İptal Edildi';
    return client.isActive ? 'Aktif' : 'Pasif';
  }
}

