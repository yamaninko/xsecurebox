# Bileşen Diyagramı (Component Diagram)

## Sistem Mimarisi Genel Bakış

Secure Box sistemi, mikroservis benzeri bir mimari ile yüksek güvenlikli anahtar yönetim çözümü sunar. Sistem katmanlı mimari prensiplerine göre tasarlanmıştır.

---

## 1. Sistem Topolojisi

```
┌─────────────────────────────────────────────────────────────────┐
│                         EXTERNAL USERS                           │
│                  (Admin, Client, Service Accounts)               │
└──────────────────────────┬──────────────────────────────────────┘
                           │ HTTPS/TLS 1.3
                           ▼
┌─────────────────────────────────────────────────────────────────┐
│                    NGINX LOAD BALANCER                           │
│              (Reverse Proxy, SSL Termination)                    │
│                   Port: 443 (HTTPS)                              │
└─────────────┬────────────────────────────┬──────────────────────┘
              │                            │
              ▼                            ▼
┌─────────────────────────┐    ┌─────────────────────────┐
│   ANGULAR PORTAL        │    │   ASP.NET API (1..N)    │
│   Container #1          │    │   Containers            │
│   Port: 4200            │    │   Port: 5000            │
│                         │    │                         │
│ - Authentication UI     │    │ - JWT Auth Module       │
│ - Certificate Mgmt UI   │    │ - Certificate Module    │
│ - Key Management UI     │    │ - Key Management Module │
│ - User/Role Mgmt UI     │    │ - User/Role Module      │
│ - Audit Log Viewer      │    │ - Logging Module        │
│                         │    │ - Encryption Service    │
└────────────┬────────────┘    └───────┬─────────────────┘
             │                         │
             │ REST API                │
             └─────────────────────────┘
                                       │
                                       │
              ┌────────────────────────┴───────────────────────┐
              │                                                │
              ▼                                                ▼
┌─────────────────────────┐                    ┌─────────────────────────┐
│   REDIS CACHE           │                    │   RABBITMQ              │
│   Container             │                    │   Container             │
│   Port: 6379            │                    │   Port: 5672, 15672     │
│                         │                    │                         │
│ - Session Store         │                    │ - Audit Log Queue       │
│ - Token Blacklist       │                    │ - Certificate Events    │
│ - Rate Limiting         │                    │ - Key Lifecycle Events  │
│ - Temp Data Cache       │                    │ - Notification Queue    │
└─────────────────────────┘                    └───────┬─────────────────┘
                                                       │
┌──────────────────────────────────────────────────────┼──────────┐
│                                                      │          │
▼                                                      ▼          ▼
┌─────────────────────────┐    ┌─────────────────────────┐   ┌──────────────┐
│   POSTGRESQL            │    │   MONGODB               │   │ ELK / OPENSEARCH│
│   Container             │    │   Container             │   │ Container Stack │
│   Port: 5432            │    │   Port: 27017           │   │ Ports: 9200,5601│
│                         │    │                         │   │                 │
│ - Users                 │    │ - Audit Logs            │   │ - Elasticsearch │
│ - Roles & Permissions   │    │ - API Access Logs       │   │ - Logstash      │
│ - Certificates          │    │ - Error Logs            │   │ - Kibana        │
│ - Keys (encrypted)      │    │ - Performance Metrics   │   │                 │
│ - Access Control Lists  │    │                         │   │ - Log Analysis  │
│ - Audit Trail (Summary) │    │                         │   │ - Alerting      │
└─────────────────────────┘    └─────────────────────────┘   └──────────────────┘
```

---

## 2. Backend API Bileşenleri (ASP.NET 9)

### 2.1 Ana Modüller

#### **Authentication & Authorization Module**
- **Sorumluluk**: JWT token üretimi, doğrulama, yetkilendirme
- **Bağımlılıklar**: PostgreSQL (User/Role), Redis (Token cache)
- **Endpoints**: `/api/auth/login`, `/api/auth/refresh`, `/api/auth/logout`

#### **Certificate Management Module**
- **Sorumluluk**: X.509 sertifika yükleme, doğrulama, yaşam döngüsü yönetimi
- **Bağımlılıklar**: PostgreSQL (Certificate metadata), File System (Certificate store)
- **Endpoints**: `/api/certificates/*`
- **Alt Bileşenler**:
  - Certificate Upload Service
  - Certificate Validation Service
  - Certificate Rotation Service
  - Certificate Revocation Service

#### **Key Management Module**
- **Sorumluluk**: Anahtar oluşturma, şifreleme, saklama, erişim kontrolü
- **Bağımlılıklar**: PostgreSQL (Key metadata), Certificate Module (Encryption)
- **Endpoints**: `/api/keys/*`
- **Alt Bileşenler**:
  - Key Creation Service
  - Key Encryption Service (uses Certificate)
  - Key Decryption Service
  - Key Lifecycle Service (rotation, expiration, deletion)

#### **User & Role Management Module**
- **Sorumluluk**: Kullanıcı/rol CRUD, permission management
- **Bağımlılıklar**: PostgreSQL (Users, Roles, Permissions)
- **Endpoints**: `/api/users/*`, `/api/roles/*`

#### **Audit & Logging Module**
- **Sorumluluk**: Tüm kritik işlemleri loglama, audit trail oluşturma
- **Bağımlılıklar**: MongoDB (Log store), RabbitMQ (Async logging), ELK (Analysis)
- **Özellikler**:
  - Real-time audit logging
  - Tamper-proof log storage
  - Log retention policies

#### **Encryption Service (Core)**
- **Sorumluluk**: Tüm şifreleme/deşifreleme işlemleri
- **Algoritma**: AES-256-GCM + RSA (certificate-based)
- **Sertifika Gereksinimleri**: X.509 v3, min 2048-bit RSA veya 256-bit ECC
- **Bağımlılıklar**: Certificate Module

#### **Messaging Service**
- **Sorumluluk**: RabbitMQ ile asenkron mesajlaşma
- **Kuyruklar**:
  - `audit-log-queue`: Audit kayıtları
  - `certificate-events`: Sertifika yaşam döngüsü olayları
  - `key-events`: Anahtar yaşam döngüsü olayları
  - `notification-queue`: Kullanıcı bildirimleri

---

## 3. Frontend Portal Bileşenleri (Angular)

### 3.1 Ana Modüller

#### **Authentication Module**
- **Bileşenler**: Login, Logout, Token Refresh
- **Services**: AuthService, TokenService, AuthGuard
- **Routing**: Guard-protected routes

#### **Dashboard Module**
- **Bileşenler**: 
  - Admin Dashboard (system overview, metrics)
  - Client Dashboard (user keys, recent activity)
- **Services**: DashboardService

#### **Certificate Management Module**
- **Bileşenler**:
  - Certificate List (DataTable)
  - Certificate Upload
  - Certificate Details
  - Certificate Status Monitor
- **Services**: CertificateService

#### **Key Management Module**
- **Bileşenler**:
  - Key List (DataTable with filters)
  - Key Create
  - Key Details
  - Key Retrieve (with authorization check)
- **Services**: KeyService

#### **User & Role Management Module**
- **Bileşenler**:
  - User List
  - User Create/Edit
  - Role Management
  - Permission Assignment
- **Services**: UserService, RoleService

#### **Audit Log Module**
- **Bileşenler**:
  - Audit Log Viewer (searchable, filterable)
  - Audit Report Generator
- **Services**: AuditService

#### **Shared Module**
- **Bileşenler**: Header, Footer, Sidebar, Notification Toast
- **Services**: NotificationService, LoaderService
- **Directives**: Role-based directive (`*hasRole="'Admin'"`)

---

## 4. Veritabanı ve Depolama Bileşenleri

### 4.1 PostgreSQL (İlişkisel Ana Veri)
- **Kullanım**: Structured data (Users, Roles, Certificates, Keys metadata)
- **Bağlantı Havuzu**: Min 10, Max 50
- **Yedekleme**: Daily automated backups with retention 30 days
- **Encryption**: Encryption at rest (PGCRYPTO)

### 4.2 Redis (Cache & Session)
- **Kullanım**:
  - JWT token cache (blacklist için)
  - Session store
  - Rate limiting counters
  - Temporary data (certificate validation results)
- **TTL Policies**: Token blacklist (token expiry time), Session (30 min idle)
- **Persistence**: RDB snapshots (her 5 dakika)

### 4.3 MongoDB (Log Store)
- **Kullanım**:
  - Audit logs (tamper-proof collections)
  - API access logs
  - Error logs
  - Performance metrics
- **Sharding**: Zaman bazlı partitioning (aylık collections)
- **Retention**: 1 yıl, sonra arşivleme

### 4.4 RabbitMQ (Message Broker)
- **Kullanım**: Asenkron event processing
- **Exchanges**: 
  - `audit-exchange` (fanout)
  - `certificate-exchange` (topic)
  - `key-exchange` (topic)
- **Dead Letter Queue**: Failed message handling

### 4.5 ELK / OpenSearch (Log Analysis)
- **Elasticsearch**: Log indexing ve search
- **Logstash**: Log aggregation, filtering, enrichment
- **Kibana**: Visualization, dashboards, alerting
- **Use Cases**:
  - Real-time monitoring
  - Anomaly detection
  - Security alerts
  - Compliance reporting

---

## 5. Deployment Mimarisi (Docker-Compose)

### 5.1 Container'lar

| Container Name         | Image                    | Replicas | Exposed Ports |
|------------------------|--------------------------|----------|---------------|
| nginx-lb               | nginx:alpine             | 1        | 443, 80       |
| secure-box-api         | secure-box-api:latest    | 2+       | 5000          |
| secure-box-portal      | secure-box-portal:latest | 1        | 4200          |
| postgres               | postgres:16-alpine       | 1        | 5432          |
| redis                  | redis:7-alpine           | 1        | 6379          |
| mongodb                | mongo:7                  | 1        | 27017         |
| rabbitmq               | rabbitmq:3-management    | 1        | 5672, 15672   |
| elasticsearch          | elasticsearch:8.x        | 1        | 9200          |
| logstash               | logstash:8.x             | 1        | 5044          |
| kibana                 | kibana:8.x               | 1        | 5601          |

### 5.2 Network Topolojisi
- **Frontend Network**: Portal ↔ Nginx
- **Backend Network**: API ↔ All backend services
- **Data Network**: Databases (isolated)
- **Monitoring Network**: ELK Stack

### 5.3 Volume Management
- `postgres-data`: PostgreSQL data persistence
- `mongodb-data`: MongoDB data persistence
- `redis-data`: Redis RDB snapshots
- `rabbitmq-data`: RabbitMQ queue persistence
- `elasticsearch-data`: ES indices
- `certificates`: Certificate storage (encrypted volume)

---

## 6. Güvenlik Katmanları

### 6.1 Network Security
- **TLS 1.3**: Tüm dış iletişim
- **mTLS**: API ↔ Service arası iletişim (opsiyonel)
- **Firewall Rules**: Sadece gerekli portlar expose

### 6.2 Application Security
- **JWT**: Short-lived access tokens (15 min)
- **Refresh Tokens**: 7 günlük, Redis'te saklanır
- **RBAC**: Role-based access control
- **Input Validation**: Tüm API endpoints
- **Rate Limiting**: Redis-based, per-user/IP

### 6.3 Data Security
- **Encryption at Rest**: PostgreSQL (PGCRYPTO), MongoDB (encryption enabled)
- **Encryption in Transit**: TLS 1.3
- **Key Encryption**: Certificate-based AES-256-GCM
- **Sensitive Data Masking**: Logs'da sensitive data maskelenir

### 6.4 Audit & Compliance
- **Comprehensive Logging**: Tüm CRUD operations
- **Tamper-Proof Logs**: MongoDB'de immutable collections
- **Access Tracking**: Her key retrieval loglanır
- **Compliance Ready**: GDPR, SOC2, ISO 27001 uyumlu tasarım

---

## 7. Monitoring & Alerting

### 7.1 Health Checks
- **API Health Endpoint**: `/health` (200 OK check)
- **Database Connectivity**: PostgreSQL, MongoDB, Redis ping
- **Queue Health**: RabbitMQ connection status

### 7.2 Metrics Collection
- **Application Metrics**: Response times, error rates, throughput
- **System Metrics**: CPU, Memory, Disk I/O (via Docker stats)
- **Business Metrics**: Key retrievals/day, active users, certificate expirations

### 7.3 Alerting
- **Critical Alerts**: Database down, API crash, certificate expiry warning
- **Security Alerts**: Multiple failed login attempts, suspicious key access patterns
- **Notification Channels**: Email, Slack, PagerDuty

---

## 8. Scalability Considerations

### 8.1 Horizontal Scaling
- **API Containers**: 2+ replicas behind load balancer
- **Database Read Replicas**: PostgreSQL read replicas için (gelecekte)
- **Cache Cluster**: Redis Sentinel/Cluster (high-availability)

### 8.2 Vertical Scaling
- **Database**: SSD storage, optimized PostgreSQL config
- **API**: Increased memory/CPU per container

### 8.3 Performance Optimization
- **Connection Pooling**: Database bağlantıları
- **Caching Strategy**: Redis ile frequently accessed data
- **CDN**: Static assets için (portal UI)
- **Database Indexing**: Optimized queries

---

## 9. Bileşen Etkileşim Akışı

### Örnek: Key Retrieval Flow

```
1. User/Service → HTTPS Request → NGINX Load Balancer
2. NGINX → Forwards → API Container (Round-robin)
3. API → JWT Validation (AuthMiddleware)
4. API → Check Redis Cache (Key metadata cache) → [CACHE HIT/MISS]
5. [CACHE MISS] → API → PostgreSQL (Key metadata + ACL check)
6. API → Authorization Check (RBAC) → [AUTHORIZED/DENIED]
7. [AUTHORIZED] → API → Fetch Certificate → Decrypt Key
8. API → Publish Event → RabbitMQ (audit-log-queue)
9. API → Response (Decrypted Key) → User/Service
10. RabbitMQ Consumer → MongoDB (Audit Log persist)
11. MongoDB → Logstash → Elasticsearch (Real-time indexing)
12. Kibana → Dashboard Update (Metrics)
```

---

## 10. Disaster Recovery & Backup

### 10.1 Backup Strategy
- **PostgreSQL**: Daily full backup, hourly incremental
- **MongoDB**: Daily snapshots
- **Certificates**: Encrypted backup to S3/Object Storage
- **Redis**: RDB snapshots (persistent volume)

### 10.2 Recovery Plan
- **RTO (Recovery Time Objective)**: < 4 hours
- **RPO (Recovery Point Objective)**: < 1 hour
- **Backup Retention**: 30 days (compliance requirement)

---

## Sonuç

Bu component diagram, Secure Box sisteminin modüler, ölçeklenebilir, güvenli ve yüksek erişilebilirlik prensipleriyle tasarlandığını göstermektedir. Her bileşen açıkça tanımlanmış sorumluluklar ve sınırlarla birbirinden ayrıştırılmıştır, böylece bakım, test ve geliştime süreçleri kolaylaşmaktadır.

