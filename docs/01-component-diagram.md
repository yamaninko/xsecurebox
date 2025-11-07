# Secure Box - Component Diagram

## System Overview

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         Client Applications                              │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐                  │
│  │ Web Portal   │  │ Mobile App   │  │ Service/SDK  │                  │
│  │  (Angular)   │  │   (Future)   │  │   Clients    │                  │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘                  │
└─────────┼──────────────────┼──────────────────┼──────────────────────────┘
          │                  │                  │
          └──────────────────┼──────────────────┘
                             ▼
                    ┌────────────────┐
                    │  Nginx LB      │  (Load Balancer + TLS Termination)
                    │   (Port 80/443)│
                    └───────┬────────┘
                            │
          ┌─────────────────┴─────────────────┐
          ▼                                   ▼
┌─────────────────┐                  ┌─────────────────┐
│   API Instance 1│                  │   API Instance 2│
│  ASP.NET Core 9 │                  │  ASP.NET Core 9 │
│   (Port 5000)   │                  │   (Port 5000)   │
└────────┬────────┘                  └────────┬────────┘
         │                                    │
         └────────────────┬───────────────────┘
                          │
         ┌────────────────┴────────────────┬────────────────┬──────────────┐
         ▼                                 ▼                ▼              ▼
┌─────────────────┐           ┌─────────────────┐  ┌─────────────┐  ┌──────────┐
│   PostgreSQL    │           │     Redis       │  │   MongoDB   │  │ RabbitMQ │
│  (Core Data)    │           │ (Cache/Session) │  │(Audit Logs) │  │(Messages)│
│   Port 5432     │           │   Port 6379     │  │ Port 27017  │  │Port 5672 │
└─────────────────┘           └─────────────────┘  └─────────────┘  └──────────┘
                                                            │
                                                            ▼
                                               ┌────────────────────┐
                                               │  Elasticsearch     │
                                               │ (Log Search/Index) │
                                               │     Port 9200      │
                                               └──────────┬─────────┘
                                                          │
                                          ┌───────────────┴───────────────┐
                                          ▼                               ▼
                                   ┌─────────────┐                ┌─────────────┐
                                   │  Logstash   │                │   Kibana    │
                                   │(Log Pipeline│                │(Log Visualiz│
                                   │  Port 5044) │                │  Port 5601) │
                                   └─────────────┘                └─────────────┘
```

## Component Details

### 1. **Frontend Layer**

#### Web Portal (Angular 18)
- **Technology**: Angular 18+ with Standalone Components
- **Responsibilities**:
  - User authentication and session management
  - Dashboard with system statistics
  - Key management UI (CRUD, retrieve, rotate, revoke)
  - Certificate management UI (upload, generate, revoke)
  - User & Role management UI (Admin only)
  - Audit log viewer
- **Security**:
  - JWT token storage in localStorage/sessionStorage
  - Role-based route guards
  - HTTPS only in production
  - XSS protection via Angular sanitization

#### Service/SDK Clients
- **Purpose**: Direct API integration for services
- **Authentication**: Token + Certificate + Password (Triple Auth)
- **Use Cases**: Automated key retrieval, CI/CD pipelines, service-to-service

---

### 2. **API Layer (Backend)**

#### API Instances (ASP.NET Core 9)
- **Deployment**: 2+ replicas behind Nginx load balancer
- **Port**: Internal 5000, External via Nginx (80/443)
- **Controllers**:
  - `AuthController`: Login, logout, token refresh, password change
  - `UsersController`: User CRUD, role assignment
  - `RolesController`: Role CRUD, permission assignment
  - `KeysController`: Key CRUD, retrieve (decrypt), rotate, revoke
  - `CertificatesController`: Certificate CRUD, upload, revoke
  - `AuditController`: Audit trail and access log queries

#### Authentication & Authorization
- **JWT Tokens**: Access token (15 min) + Refresh token (7 days)
- **Roles**: Admin, Client, Service
- **Permissions**: Resource-Action based (e.g., `Key.Read`, `Certificate.Create`)
- **Triple Authentication**: Token + Certificate + Password for sensitive operations

#### Services
- **AuthService**: User authentication, token generation/validation
- **UserService**: User management, role assignment
- **RoleService**: Role & permission management
- **KeyService**: Key lifecycle (create, retrieve, rotate, revoke)
- **CertificateService**: Certificate management
- **EncryptionService**: AES256/RSA/ECC encryption/decryption
- **AuditService**: Audit trail logging
- **MessageBrokerService**: RabbitMQ integration for async operations

---

### 3. **Data Layer**

#### PostgreSQL (Primary Database)
- **Purpose**: Core transactional data
- **Tables**:
  - Users, Roles, Permissions
  - UserRoles, RolePermissions
  - Keys (encrypted values)
  - Certificates
  - KeyAccessLogs
  - AuditTrails
- **Features**: ACID compliance, referential integrity, soft deletes

#### Redis (Cache & Session Store)
- **Purpose**: 
  - Session data caching
  - Token blacklist (for logout)
  - Frequently accessed metadata
- **TTL**: Configurable per key type

#### MongoDB (Audit & Log Store)
- **Purpose**: High-volume log storage
- **Collections**:
  - audit_logs: Detailed operation logs
  - key_access_logs: Key retrieval history
  - system_logs: Application logs
- **Retention**: 90-day default policy

---

### 4. **Messaging & Observability**

#### RabbitMQ
- **Queues**:
  - `key_rotation_queue`: Async key rotation tasks
  - `certificate_expiry_queue`: Certificate expiry notifications
  - `audit_event_queue`: Async audit logging
- **Pattern**: Publisher-Subscriber with dead-letter queue

#### ELK Stack (Elasticsearch, Logstash, Kibana)
- **Elasticsearch**: Centralized log indexing and search
- **Logstash**: Log aggregation pipeline (from MongoDB/files)
- **Kibana**: Log visualization dashboards
- **Use Cases**: 
  - Security incident analysis
  - Performance monitoring
  - Compliance reporting

---

### 5. **Infrastructure & Networking**

#### Nginx (Reverse Proxy & Load Balancer)
- **Functions**:
  - TLS termination (HTTPS)
  - Load balancing (round-robin across API instances)
  - Rate limiting
  - Static file serving (frontend portal)
- **Config**:
  - `/api/*` → API instances (5001, 5002)
  - `/*` → Frontend portal (port 80)

#### Docker Compose
- **Services**: 11 containers (2 API, 2 Portal, 1 Nginx, 6 infrastructure)
- **Networks**: 
  - `backend-network`: API ↔ Databases
  - `frontend-network`: Portal ↔ Nginx
  - `monitoring-network`: ELK stack
- **Volumes**: Persistent storage for databases and logs

---

## Data Flow Examples

### 1. User Login Flow
```
User (Portal) → Nginx → API → AuthService → PostgreSQL
                                     ↓
                            JWT Token Generated
                                     ↓
                         Return to User (Portal)
```

### 2. Key Retrieval Flow (Triple Authentication)
```
Client → API + [JWT Token + Certificate + Password]
             ↓
       AuthService validates JWT
             ↓
       CertificateService validates Certificate
             ↓
       AuthService validates Password
             ↓
       KeyService retrieves encrypted key
             ↓
       EncryptionService decrypts using Certificate
             ↓
       AuditService logs access → MongoDB
             ↓
       Return decrypted key value
```

### 3. Key Creation Flow
```
User (Portal) → API + Create Key Request
                      ↓
                KeyService validates
                      ↓
         EncryptionService encrypts value using selected Certificate
                      ↓
              Store in PostgreSQL (Keys table)
                      ↓
          RabbitMQ publishes event → audit_event_queue
                      ↓
              Return Key metadata
```

---

## Security Boundaries

1. **Network Segmentation**: Frontend, Backend, and Data layers isolated
2. **TLS Everywhere**: All external and inter-service communication encrypted
3. **Zero-Trust**: Every request validated (JWT + permissions)
4. **Encryption at Rest**: Keys encrypted with certificate-based encryption
5. **Audit All the Things**: Every operation logged to MongoDB + ELK

---

## Scalability & High Availability

- **API**: Horizontal scaling (add more instances behind Nginx)
- **Database**: PostgreSQL replication, Redis clustering
- **Load Balancer**: Nginx can be replaced with cloud LB (AWS ALB, Azure Gateway)
- **Async Processing**: RabbitMQ enables decoupled, scalable workloads

---

**Last Updated**: 2025-11-07
