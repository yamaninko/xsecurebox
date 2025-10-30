# Angular Portal - UI Tasarımı ve Komponentler

## Genel Bakış

Secure Box Portal, Angular framework ile geliştirilmiş, modern ve responsive bir web uygulamasıdır. Material Design prensiplerine uygun olarak Angular Material kullanır.

**Teknolojiler**:
- Angular (latest version)
- Angular Material (UI Components)
- RxJS (Reactive programming)
- NGRX/Store (State management - optional)
- Chart.js / ngx-charts (Data visualization)

---

## 1. Uygulama Yapısı (Module-Based Architecture)

```
src/app/
├── core/                          # Singleton services, guards, interceptors
│   ├── auth/
│   │   ├── auth.service.ts
│   │   ├── token.service.ts
│   │   ├── auth.guard.ts
│   │   └── auth.interceptor.ts
│   ├── services/
│   │   ├── notification.service.ts
│   │   ├── loader.service.ts
│   │   └── error-handler.service.ts
│   └── models/
│       ├── user.model.ts
│       ├── key.model.ts
│       └── certificate.model.ts
│
├── shared/                        # Reusable components, directives, pipes
│   ├── components/
│   │   ├── header/
│   │   ├── footer/
│   │   ├── sidebar/
│   │   ├── breadcrumb/
│   │   ├── confirmation-dialog/
│   │   └── data-table/
│   ├── directives/
│   │   ├── has-role.directive.ts
│   │   └── has-permission.directive.ts
│   ├── pipes/
│   │   ├── date-format.pipe.ts
│   │   └── mask-secret.pipe.ts
│   └── shared.module.ts
│
├── features/                      # Feature modules (lazy-loaded)
│   ├── auth/
│   │   ├── login/
│   │   ├── change-password/
│   │   └── auth.module.ts
│   ├── dashboard/
│   │   ├── admin-dashboard/
│   │   ├── client-dashboard/
│   │   └── dashboard.module.ts
│   ├── certificates/
│   │   ├── certificate-list/
│   │   ├── certificate-detail/
│   │   ├── certificate-upload/
│   │   └── certificates.module.ts
│   ├── keys/
│   │   ├── key-list/
│   │   ├── key-detail/
│   │   ├── key-create/
│   │   ├── key-retrieve/
│   │   └── keys.module.ts
│   ├── users/
│   │   ├── user-list/
│   │   ├── user-detail/
│   │   ├── user-form/
│   │   └── users.module.ts
│   ├── roles/
│   │   ├── role-list/
│   │   ├── role-form/
│   │   └── roles.module.ts
│   └── audit/
│       ├── audit-log-list/
│       ├── key-access-logs/
│       └── audit.module.ts
│
├── layout/
│   ├── main-layout/
│   │   └── main-layout.component.ts
│   └── auth-layout/
│       └── auth-layout.component.ts
│
├── app-routing.module.ts
├── app.component.ts
└── app.module.ts
```

---

## 2. UI Screens ve User Flows

### 2.1 Authentication Module

#### **Login Page** (`/login`)

**Komponentler**:
- `LoginComponent`

**UI Elementi**:
```
┌─────────────────────────────────────┐
│         SECURE BOX LOGO             │
│                                     │
│  ┌───────────────────────────────┐ │
│  │ Username                      │ │
│  │ [____________________]        │ │
│  └───────────────────────────────┘ │
│                                     │
│  ┌───────────────────────────────┐ │
│  │ Password                      │ │
│  │ [____________________] 👁      │ │
│  └───────────────────────────────┘ │
│                                     │
│  [ ] Remember me                   │
│                                     │
│  [        LOGIN        ]           │
│                                     │
│  Forgot password?                  │
└─────────────────────────────────────┘
```

**Fonksiyonellik**:
- Username/password validation
- Show/hide password toggle
- Error messages (invalid credentials, account locked)
- Remember me (optional)
- Loading indicator
- JWT token storage

**Services**: `AuthService.login()`

---

#### **Change Password Page** (`/auth/change-password`)

**Komponentler**:
- `ChangePasswordComponent`

**UI Elementi**:
- Current Password input
- New Password input (with strength indicator)
- Confirm Password input
- Password requirements checklist
- Submit button

**Validation Rules**:
- Min 8 characters
- At least 1 uppercase, 1 lowercase, 1 number, 1 special char
- Passwords must match

---

### 2.2 Dashboard Module

#### **Admin Dashboard** (`/dashboard/admin`)

**Komponentler**:
- `AdminDashboardComponent`
- `StatisticsCardComponent` (reusable)
- `RecentActivityComponent`
- `CertificateExpiryWidgetComponent`

**UI Layout**:
```
┌────────────────────────────────────────────────────────┐
│ Dashboard > Admin                                      │
├────────────────────────────────────────────────────────┤
│                                                        │
│  ┌─────────┐  ┌─────────┐  ┌─────────┐  ┌─────────┐ │
│  │ Users   │  │ Keys    │  │ Certs   │  │ Accesses│ │
│  │  142    │  │  1,234  │  │   45    │  │  5,678  │ │
│  └─────────┘  └─────────┘  └─────────┘  └─────────┘ │
│                                                        │
│  ┌──────────────────────┐  ┌──────────────────────┐  │
│  │ Key Retrievals       │  │ Certificate Expiry   │  │
│  │ (Chart - Last 7 days)│  │ (Expiring soon list) │  │
│  │                      │  │                      │  │
│  │  [Line Chart]        │  │ • Cert A (30 days)  │  │
│  │                      │  │ • Cert B (15 days)  │  │
│  └──────────────────────┘  └──────────────────────┘  │
│                                                        │
│  ┌──────────────────────────────────────────────────┐ │
│  │ Recent Activity                                  │ │
│  │ • User john@example.com retrieved key "DB Pass" │ │
│  │ • Admin uploaded certificate "Prod Cert"        │ │
│  │ • User jane@example.com created key "API Key"   │ │
│  └──────────────────────────────────────────────────┘ │
└────────────────────────────────────────────────────────┘
```

**Data Sources**:
- Total users, keys, certificates (API: `GET /metrics`)
- Key retrieval chart (last 7 days)
- Certificate expiry warnings
- Recent audit logs

**Refresh**: Auto-refresh every 30 seconds

---

#### **Client Dashboard** (`/dashboard/client`)

**Komponentler**:
- `ClientDashboardComponent`
- `MyKeysWidgetComponent`
- `RecentAccessesWidgetComponent`

**UI Layout**:
```
┌────────────────────────────────────────────────────────┐
│ Dashboard > My Overview                                │
├────────────────────────────────────────────────────────┤
│                                                        │
│  ┌─────────┐  ┌─────────┐  ┌─────────┐              │
│  │ My Keys │  │ Accesses│  │ Expires │              │
│  │   12    │  │   45    │  │    2    │              │
│  └─────────┘  └─────────┘  └─────────┘              │
│                                                        │
│  ┌──────────────────────────────────────────────────┐ │
│  │ My Keys                                          │ │
│  │ ┌──────────────────────────────────────────────┐│ │
│  │ │ Name        │ Type    │ Status │ Actions    ││ │
│  │ ├──────────────────────────────────────────────┤│ │
│  │ │ DB Password │ SECRET  │ Active │ [Retrieve] ││ │
│  │ │ API Key     │ API_KEY │ Active │ [Retrieve] ││ │
│  │ └──────────────────────────────────────────────┘│ │
│  └──────────────────────────────────────────────────┘ │
│                                                        │
│  ┌──────────────────────────────────────────────────┐ │
│  │ Recent Access History                            │ │
│  │ • Retrieved "DB Password" - 2 hours ago         │ │
│  │ • Retrieved "API Key" - Yesterday 10:30 AM      │ │
│  └──────────────────────────────────────────────────┘ │
└────────────────────────────────────────────────────────┘
```

**Fonksiyonellik**:
- Quick access to own keys
- Recent access history
- Expiring keys warning

---

### 2.3 Certificate Management Module

#### **Certificate List** (`/certificates`)

**Komponentler**:
- `CertificateListComponent`
- `DataTableComponent` (shared)

**UI Layout**:
```
┌────────────────────────────────────────────────────────┐
│ Certificates                         [+ Upload Certificate] │
├────────────────────────────────────────────────────────┤
│                                                        │
│ Search: [______________] Status: [All ▼] [Filter]    │
│                                                        │
│ ┌────────────────────────────────────────────────────┐│
│ │ Name          │ Thumbprint │ Expires    │ Status  ││
│ ├────────────────────────────────────────────────────┤│
│ │ Prod Cert     │ sha256...  │ 2026-01-01 │ ✅ Active││
│ │ Test Cert     │ sha256...  │ 2025-12-01 │ ⚠️ Soon  ││
│ │ Old Cert      │ sha256...  │ 2024-01-01 │ ❌ Expired││
│ │               │            │            │         ││
│ │ [View] [Edit] [Revoke] [Delete]                   ││
│ └────────────────────────────────────────────────────┘│
│                                                        │
│ Pagination: < 1 2 3 4 5 >                             │
└────────────────────────────────────────────────────────┘
```

**Fonksiyonellik**:
- Sortable columns
- Search by name/thumbprint
- Status filter (Active, Expired, Revoked, Pending)
- Pagination
- Actions: View, Edit, Revoke, Delete (role-based visibility)
- Color-coded status badges
- Expiry warnings (< 30 days)

**Services**: `CertificateService.list()`

---

#### **Certificate Upload** (`/certificates/upload`)

**Komponentler**:
- `CertificateUploadComponent`
- `FileUploadComponent` (shared)

**UI Layout**:
```
┌────────────────────────────────────────────────────────┐
│ Upload Certificate                           [< Back]  │
├────────────────────────────────────────────────────────┤
│                                                        │
│ Certificate Name *                                     │
│ [______________________________]                       │
│                                                        │
│ Description                                            │
│ [______________________________]                       │
│ [______________________________]                       │
│                                                        │
│ Certificate File * (.pem, .cer, .pfx)                 │
│ ┌─────────────────────────────┐                       │
│ │  Drag & drop file here      │                       │
│ │  or [Browse...]             │                       │
│ └─────────────────────────────┘                       │
│                                                        │
│ Password (for PFX files)                              │
│ [______________________________]                       │
│                                                        │
│ [ ] Use for signing                                   │
│ [✓] Use for encryption                                │
│                                                        │
│ [Cancel]                          [Upload]            │
└────────────────────────────────────────────────────────┘
```

**Validation**:
- Required fields
- File type check (.pem, .cer, .pfx)
- File size limit (5MB)
- Certificate validation (X.509 format)

**Services**: `CertificateService.upload()`

---

#### **Certificate Detail** (`/certificates/:id`)

**Komponentler**:
- `CertificateDetailComponent`
- `ConfirmationDialogComponent` (shared)

**UI Layout**:
```
┌────────────────────────────────────────────────────────┐
│ Certificate Details                   [Edit] [Revoke]  │
├────────────────────────────────────────────────────────┤
│                                                        │
│ Name: Production Encryption Certificate               │
│ Status: ✅ Active                                      │
│                                                        │
│ ┌────────────────────────────────────────────────────┐│
│ │ Certificate Information                            ││
│ │                                                    ││
│ │ Thumbprint:  sha256:abcdef123456...                ││
│ │ Subject:     CN=SecureBox Production               ││
│ │ Issuer:      CN=SecureBox CA                       ││
│ │ Serial No:   1234567890                            ││
│ │ Algorithm:   RSA                                   ││
│ │ Key Size:    2048 bits                             ││
│ │ Valid From:  2025-01-01 00:00:00                   ││
│ │ Valid Until: 2026-01-01 00:00:00                   ││
│ │ Uploaded By: admin                                 ││
│ │ Uploaded At: 2025-10-01 10:00:00                   ││
│ └────────────────────────────────────────────────────┘│
│                                                        │
│ ┌────────────────────────────────────────────────────┐│
│ │ Associated Keys (12)                               ││
│ │                                                    ││
│ │ • Production DB Password                           ││
│ │ • API Gateway Key                                  ││
│ │ • ... (View all)                                   ││
│ └────────────────────────────────────────────────────┘│
│                                                        │
│ [< Back to List]                                       │
└────────────────────────────────────────────────────────┘
```

**Fonksiyonellik**:
- View all certificate details
- Associated keys list (hyperlinks)
- Edit metadata (name, description)
- Revoke action (confirmation dialog)
- Delete action (if no associated keys)

---

### 2.4 Key Management Module

#### **Key List** (`/keys`)

**Komponentler**:
- `KeyListComponent`
- `DataTableComponent` (shared)

**UI Layout**:
```
┌────────────────────────────────────────────────────────┐
│ Keys                                    [+ Create Key] │
├────────────────────────────────────────────────────────┤
│                                                        │
│ Search: [__________] Status: [All▼] Type: [All▼]     │
│                                                        │
│ ┌────────────────────────────────────────────────────┐│
│ │Name        │Type  │Status │Owner│Accessed│Actions ││
│ ├────────────────────────────────────────────────────┤│
│ │DB Password │SECRET│✅Active│john │2h ago │[Retrieve]││
│ │API Key     │API   │✅Active│jane │5d ago │[Retrieve]││
│ │Old Secret  │SECRET│❌Revoked│admin│-     │[View]   ││
│ │                                                    ││
│ │ [View Details] [Edit] [Rotate] [Revoke] [Delete] ││
│ └────────────────────────────────────────────────────┘│
│                                                        │
│ Pagination: < 1 2 3 4 5 >                             │
└────────────────────────────────────────────────────────┘
```

**Fonksiyonellik**:
- Search by name
- Filter by status (Active, Expired, Revoked, Archived)
- Filter by type (API_KEY, DATABASE_PASSWORD, SECRET, etc.)
- Sort by name, last accessed, access count
- Quick retrieve action (opens dialog)
- Role-based action visibility

**Services**: `KeyService.list()`

---

#### **Key Create** (`/keys/create`)

**Komponentler**:
- `KeyCreateComponent`

**UI Layout**:
```
┌────────────────────────────────────────────────────────┐
│ Create New Key                              [< Back]   │
├────────────────────────────────────────────────────────┤
│                                                        │
│ Key Name *                                             │
│ [______________________________]                       │
│                                                        │
│ Description                                            │
│ [______________________________]                       │
│ [______________________________]                       │
│                                                        │
│ Key Type *                                             │
│ [API_KEY ▼]                                            │
│  - API_KEY                                             │
│  - DATABASE_PASSWORD                                   │
│  - SECRET                                              │
│  - OTHER                                               │
│                                                        │
│ Key Value * (will be encrypted)                        │
│ [______________________________] 👁                    │
│                                                        │
│ Certificate (for encryption) *                         │
│ [Production Cert ▼]                                    │
│                                                        │
│ Expiration Date (optional)                            │
│ [📅 __/__/____]                                        │
│                                                        │
│ Owner (Admin only)                                     │
│ [Current User ▼]                                       │
│                                                        │
│ [Cancel]                          [Create & Encrypt]   │
└────────────────────────────────────────────────────────┘
```

**Validation**:
- Required fields
- Key value strength check (if applicable)
- Certificate must be Active
- Expiration date (if set) must be future date

**Services**: `KeyService.create()`

**Security**:
- Key value never sent in plain text to backend (use HTTPS)
- Backend encrypts immediately
- Success message: "Key created and encrypted successfully"

---

#### **Key Retrieve Dialog** (Modal)

**Komponentler**:
- `KeyRetrieveDialogComponent`

**UI Layout**:
```
┌──────────────────────────────────────┐
│ ⚠️  Retrieve Key: "DB Password"      │
├──────────────────────────────────────┤
│                                      │
│ This action will decrypt and reveal  │
│ the key value. It will be logged.    │
│                                      │
│ Reason (optional):                   │
│ [____________________________]       │
│ [____________________________]       │
│                                      │
│ [Cancel]            [Retrieve]       │
└──────────────────────────────────────┘

After retrieve:
┌──────────────────────────────────────┐
│ ✅ Key Retrieved Successfully        │
├──────────────────────────────────────┤
│                                      │
│ Key Value:                           │
│ ┌──────────────────────────────────┐│
│ │ MyS3cr3tP@ssw0rd!         [Copy] ││
│ └──────────────────────────────────┘│
│                                      │
│ ⚠️ This value will not be shown again.│
│                                      │
│ Expires: 2026-01-01 00:00:00        │
│                                      │
│ [Close]                              │
└──────────────────────────────────────┘
```

**Fonksiyonellik**:
- Warning message
- Optional reason field
- Retrieve button triggers API call
- Show decrypted value with copy button
- Auto-clear value after dialog close
- Audit log entry created

**Services**: `KeyService.retrieve(keyId, reason)`

---

#### **Key Detail** (`/keys/:id`)

**Komponentler**:
- `KeyDetailComponent`
- `KeyAccessLogsComponent`

**UI Layout**:
```
┌────────────────────────────────────────────────────────┐
│ Key Details                      [Edit] [Rotate] [Revoke]│
├────────────────────────────────────────────────────────┤
│                                                        │
│ Name: Production DB Password                          │
│ Status: ✅ Active                                      │
│                                                        │
│ ┌────────────────────────────────────────────────────┐│
│ │ Key Information                                    ││
│ │                                                    ││
│ │ Type:         DATABASE_PASSWORD                    ││
│ │ Version:      1                                    ││
│ │ Owner:        john@example.com                     ││
│ │ Certificate:  Production Cert (sha256:abc...)      ││
│ │ Created:      2025-10-01 10:00:00                  ││
│ │ Created By:   admin                                ││
│ │ Last Accessed: 2025-10-30 08:00:00                 ││
│ │ Access Count: 150                                  ││
│ │ Expires:      2026-01-01 00:00:00                  ││
│ └────────────────────────────────────────────────────┘│
│                                                        │
│ ┌────────────────────────────────────────────────────┐│
│ │ Access History (Last 10)                           ││
│ │ ┌────────────────────────────────────────────────┐││
│ │ │Date       │User │Method│IP Address│Status     │││
│ │ ├────────────────────────────────────────────────┤││
│ │ │2h ago     │john │API   │192.168.1.100│✅Success│││
│ │ │Yesterday  │jane │Portal│192.168.1.101│✅Success│││
│ │ │2 days ago │john │API   │192.168.1.100│✅Success│││
│ │ └────────────────────────────────────────────────┘││
│ │ [View Full History]                                ││
│ └────────────────────────────────────────────────────┘│
│                                                        │
│ [< Back to List]                    [Retrieve Key]     │
└────────────────────────────────────────────────────────┘
```

**Fonksiyonellik**:
- View all metadata
- Access history (paginated)
- Edit metadata
- Rotate key (new version)
- Revoke key
- Retrieve key (button)

---

### 2.5 User Management Module

#### **User List** (`/users`)

**Komponentler**:
- `UserListComponent`
- Admin-only access

**UI Layout**:
```
┌────────────────────────────────────────────────────────┐
│ Users                                   [+ Create User]│
├────────────────────────────────────────────────────────┤
│                                                        │
│ Search: [__________] Role: [All▼] Status: [All▼]     │
│                                                        │
│ ┌────────────────────────────────────────────────────┐│
│ │Username │Email         │Roles  │Status│Last Login ││
│ ├────────────────────────────────────────────────────┤││
│ │admin    │admin@ex.com  │Admin  │✅Active│Today     ││
│ │johndoe  │john@ex.com   │Client │✅Active│2h ago    ││
│ │janedoe  │jane@ex.com   │Client │❌Inactive│-       ││
│ │                                                    ││
│ │ [View] [Edit] [Deactivate] [Delete]               ││
│ └────────────────────────────────────────────────────┘│
│                                                        │
│ Pagination: < 1 2 3 4 5 >                             │
└────────────────────────────────────────────────────────┘
```

---

#### **User Form** (`/users/create` or `/users/:id/edit`)

**Komponentler**:
- `UserFormComponent`

**UI Layout**:
```
┌────────────────────────────────────────────────────────┐
│ Create User                                 [< Back]   │
├────────────────────────────────────────────────────────┤
│                                                        │
│ Username *                                             │
│ [______________________________]                       │
│                                                        │
│ Email *                                                │
│ [______________________________]                       │
│                                                        │
│ Password *                                             │
│ [______________________________] 👁                    │
│                                                        │
│ First Name                                             │
│ [______________________________]                       │
│                                                        │
│ Last Name                                              │
│ [______________________________]                       │
│                                                        │
│ Roles * (select multiple)                             │
│ ┌────────────────────────────┐                        │
│ │ ☐ Admin                    │                        │
│ │ ☑ Client                   │                        │
│ │ ☐ Service                  │                        │
│ └────────────────────────────┘                        │
│                                                        │
│ [ ] Email Verified                                    │
│ [ ] Must Change Password                              │
│                                                        │
│ [Cancel]                          [Create User]        │
└────────────────────────────────────────────────────────┘
```

---

### 2.6 Audit & Logging Module

#### **Audit Log List** (`/audit/trails`)

**Komponentler**:
- `AuditLogListComponent`
- Admin-only access

**UI Layout**:
```
┌────────────────────────────────────────────────────────┐
│ Audit Logs                                             │
├────────────────────────────────────────────────────────┤
│                                                        │
│ Date Range: [From: __/__/__] [To: __/__/__] [Apply]  │
│ User: [All▼] Action: [All▼] Resource: [All▼]         │
│ Severity: [All▼] [Reset Filters]                     │
│                                                        │
│ ┌────────────────────────────────────────────────────┐│
│ │Timestamp│User  │Action        │Resource│Severity  ││
│ ├────────────────────────────────────────────────────┤││
│ │2h ago   │john  │Key.Retrieved │Key     │ℹ️ Info   ││
│ │Yesterday│admin │Cert.Uploaded │Cert    │ℹ️ Info   ││
│ │2 days   │jane  │User.Updated  │User    │⚠️ Warning││
│ │3 days   │john  │Key.Failed    │Key     │🔴Critical││
│ │                                                    ││
│ │ [View Details]                                     ││
│ └────────────────────────────────────────────────────┘│
│                                                        │
│ Pagination: < 1 2 3 ... 50 >                          │
│                                                        │
│ [Export to CSV]                                        │
└────────────────────────────────────────────────────────┘
```

**Fonksiyonellik**:
- Date range filter
- User, action, resource, severity filters
- Export to CSV
- View details (shows full JSONB details field)
- Color-coded severity

---

## 3. Shared Components

### 3.1 Header Component

**Komponentler**: `HeaderComponent`

**UI Layout**:
```
┌────────────────────────────────────────────────────────┐
│ [☰] SECURE BOX          🔔(3)      👤 John Doe ▼      │
└────────────────────────────────────────────────────────┘
```

**Fonksiyonellik**:
- Hamburger menu (toggle sidebar)
- Logo/brand
- Notifications badge (unread count)
- User profile dropdown:
  - My Profile
  - Change Password
  - Logout

---

### 3.2 Sidebar Component

**Komponentler**: `SidebarComponent`

**UI Layout**:
```
┌─────────────────────┐
│ 🏠 Dashboard        │
│ 🔑 Keys             │
│ 📜 Certificates     │
│ 👥 Users (Admin)    │
│ 🛡️ Roles (Admin)    │
│ 📊 Audit Logs       │
│ ⚙️ Settings         │
└─────────────────────┘
```

**Fonksiyonellik**:
- Role-based menu items
- Active route highlighting
- Collapsible (mobile)
- Icons with labels

---

### 3.3 Confirmation Dialog Component

**Komponentler**: `ConfirmationDialogComponent`

**Usage**: Reusable for critical actions (delete, revoke, etc.)

```
┌──────────────────────────────────┐
│ ⚠️  Confirm Action               │
├──────────────────────────────────┤
│                                  │
│ Are you sure you want to revoke  │
│ the certificate "Production Cert"?│
│                                  │
│ This action cannot be undone.    │
│                                  │
│ [Cancel]            [Confirm]    │
└──────────────────────────────────┘
```

---

### 3.4 Data Table Component

**Komponentler**: `DataTableComponent`

**Features**:
- Sortable columns
- Pagination
- Row selection (checkboxes)
- Custom column templates
- Loading skeleton
- Empty state
- Action buttons column

---

## 4. Directives

### 4.1 Has Role Directive

**Usage**: `*hasRole="'Admin'"`

**Fonksiyonellik**: Show/hide element based on user role

```html
<button *hasRole="'Admin'" (click)="deleteUser()">Delete</button>
```

---

### 4.2 Has Permission Directive

**Usage**: `*hasPermission="'Key.Retrieve'"`

**Fonksiyonellik**: Show/hide element based on user permission

```html
<button *hasPermission="'Key.Retrieve'" (click)="retrieveKey()">
  Retrieve Key
</button>
```

---

## 5. Services

### 5.1 Core Services

#### **AuthService**
- `login(username, password)`: Observable<AuthResponse>
- `logout()`: void
- `refreshToken()`: Observable<TokenResponse>
- `getCurrentUser()`: Observable<User>
- `isAuthenticated()`: boolean

#### **TokenService**
- `getAccessToken()`: string
- `getRefreshToken()`: string
- `setTokens(access, refresh)`: void
- `clearTokens()`: void
- `isTokenExpired()`: boolean

#### **NotificationService**
- `success(message)`: void
- `error(message)`: void
- `warning(message)`: void
- `info(message)`: void

#### **LoaderService**
- `show()`: void
- `hide()`: void
- `isLoading$`: Observable<boolean>

---

### 5.2 Feature Services

#### **CertificateService**
- `list(params)`: Observable<Certificate[]>
- `getById(id)`: Observable<Certificate>
- `upload(formData)`: Observable<Certificate>
- `update(id, data)`: Observable<Certificate>
- `revoke(id, reason)`: Observable<void>
- `delete(id)`: Observable<void>

#### **KeyService**
- `list(params)`: Observable<Key[]>
- `getById(id)`: Observable<Key>
- `create(data)`: Observable<Key>
- `retrieve(id, reason)`: Observable<KeyValue>
- `update(id, data)`: Observable<Key>
- `rotate(id, newValue, reason)`: Observable<Key>
- `revoke(id, reason)`: Observable<void>
- `delete(id)`: Observable<void>

#### **UserService**
- `list(params)`: Observable<User[]>
- `getById(id)`: Observable<User>
- `create(data)`: Observable<User>
- `update(id, data)`: Observable<User>
- `delete(id)`: Observable<void>

#### **AuditService**
- `listTrails(params)`: Observable<AuditTrail[]>
- `getKeyAccessLogs(keyId, params)`: Observable<KeyAccessLog[]>

---

## 6. User Flows

### 6.1 Admin User Flow

1. **Login** → Admin Dashboard
2. **View System Metrics** (users, keys, certificates, activity)
3. **Manage Users**:
   - Create new user
   - Assign roles
   - Deactivate user
4. **Manage Certificates**:
   - Upload certificate
   - View expiring certificates
   - Revoke certificate
5. **View Audit Logs**:
   - Filter by date, user, action
   - Export reports
6. **Logout**

---

### 6.2 Client User Flow

1. **Login** → Client Dashboard
2. **View My Keys**
3. **Create New Key**:
   - Enter key details
   - Select certificate
   - Save (encrypted)
4. **Retrieve Key**:
   - Navigate to key details
   - Click "Retrieve"
   - Enter reason (optional)
   - View/copy decrypted value
5. **View Access History** (own keys)
6. **Logout**

---

### 6.3 Service Account Flow (API-Only)

Service accounts typically don't use the portal, but if needed:
- Limited dashboard
- API key retrieval only
- No user/role management access

---

## 7. Responsive Design

**Breakpoints**:
- **Mobile**: < 768px (stacked layout, collapsible sidebar)
- **Tablet**: 768px - 1024px (adjusted columns)
- **Desktop**: > 1024px (full layout)

**Mobile Optimizations**:
- Hamburger menu
- Bottom navigation (alternative)
- Touch-friendly buttons (min 44x44px)
- Swipe gestures for tables

---

## 8. Theming

**Primary Color**: Blue (#1976D2) - Trust, security
**Accent Color**: Orange (#FF9800) - Warnings, important actions
**Warn Color**: Red (#F44336) - Errors, critical alerts

**Dark Mode**: Optional (toggle in settings)

---

## 9. Accessibility (A11y)

- ARIA labels for all interactive elements
- Keyboard navigation support (Tab, Enter, Esc)
- Screen reader compatible
- Color contrast ratio ≥ 4.5:1 (WCAG AA)
- Focus indicators

---

## 10. Performance Optimizations

- **Lazy Loading**: Feature modules loaded on-demand
- **Virtual Scrolling**: Large tables (>100 rows)
- **Change Detection**: OnPush strategy for components
- **Memoization**: Expensive computations cached
- **Image Optimization**: Compressed assets
- **Service Worker**: Offline support (PWA - optional)

---

## Sonuç

Bu UI tasarımı:
- ✅ Modern ve kullanıcı dostu
- ✅ Material Design prensipleri
- ✅ Role-based visibility
- ✅ Responsive (mobile-first)
- ✅ Accessibility ready
- ✅ Performance optimized
- ✅ Comprehensive user flows
- ✅ Reusable component architecture

