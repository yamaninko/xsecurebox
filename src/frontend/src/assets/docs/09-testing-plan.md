# Test Planı (Testing Plan)

## Genel Bakış

Secure Box sisteminin kapsamlı test stratejisi. Tüm katmanlar (Unit, Integration, E2E, Security, Performance) test edilecektir. Test coverage hedefi: **Minimum %80**.

---

## 1. Unit Tests (Birim Testler)

### 1.1 Backend Unit Tests (C# - xUnit/NUnit)

#### Test Framework
- **xUnit** (primary)
- **Moq** (mocking)
- **FluentAssertions** (assertions)
- **AutoFixture** (test data generation)

#### Coverage Areas

##### **1.1.1 Core Layer Tests**

###### Entities
```csharp
// SecureBox.Tests/Core/Entities/UserTests.cs
- User_Constructor_ShouldInitializeDefaults
- User_IsActive_DefaultTrue
- User_FailedLoginAttempts_DefaultZero
```

###### DTOs & Validation
```csharp
// SecureBox.Tests/Core/DTOs/LoginRequestTests.cs
- LoginRequest_WithValidData_ShouldPass
- LoginRequest_WithEmptyUsername_ShouldFail
- LoginRequest_WithShortPassword_ShouldFail
```

##### **1.1.2 Infrastructure Layer Tests**

###### Services
```csharp
// SecureBox.Tests/Infrastructure/Services/AuthServiceTests.cs
public class AuthServiceTests
{
    [Fact]
    public async Task LoginAsync_WithValidCredentials_ShouldReturnToken()
    {
        // Arrange
        var mockUserRepo = new Mock<IUserRepository>();
        var mockTokenService = new Mock<ITokenService>();
        var authService = new AuthService(mockUserRepo.Object, mockTokenService.Object);
        
        // Act
        var result = await authService.LoginAsync(new LoginRequest("admin", "pass123"));
        
        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().NotBeEmpty();
    }
    
    [Fact]
    public async Task LoginAsync_WithInvalidPassword_ShouldThrowUnauthorized()
    {
        // Arrange, Act, Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(...);
    }
}
```

###### Encryption Service
```csharp
// SecureBox.Tests/Infrastructure/Services/EncryptionServiceTests.cs
- EncryptAsync_WithValidCertificate_ShouldReturnEncryptedData
- DecryptAsync_WithValidData_ShouldReturnPlaintext
- DecryptAsync_WithTamperedData_ShouldThrow
- EncryptAsync_WithExpiredCertificate_ShouldThrow
```

###### Key Service
```csharp
// SecureBox.Tests/Infrastructure/Services/KeyServiceTests.cs
- CreateKeyAsync_WithValidRequest_ShouldEncryptAndStore
- RetrieveKeyAsync_WithValidAuth_ShouldDecryptAndLog
- RetrieveKeyAsync_WithExpiredKey_ShouldThrow410
- RotateKeyAsync_ShouldIncrementVersion
```

##### **1.1.3 API Layer Tests**

###### Controllers
```csharp
// SecureBox.Tests/API/Controllers/KeysControllerTests.cs
public class KeysControllerTests
{
    [Fact]
    public async Task GetKeys_WithAuthentication_ShouldReturnPaginatedKeys()
    {
        // Arrange
        var mockService = new Mock<IKeyService>();
        mockService.Setup(x => x.GetAllKeysAsync(It.IsAny<KeyQueryParams>(), ...))
                   .ReturnsAsync(new List<KeyDto> { ... });
        var controller = new KeysController(mockService.Object, Mock.Of<ILogger>());
        
        // Act
        var result = await controller.GetKeys(new KeyQueryParams());
        
        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        okResult.StatusCode.Should().Be(200);
    }
    
    [Fact]
    public async Task RetrieveKey_WithoutPermission_ShouldReturn403()
    {
        // Test authorization failure
    }
}
```

#### Coverage Metrics
- **Target**: 80% line coverage, 70% branch coverage
- **Tools**: Coverlet, ReportGenerator
- **CI Integration**: Fail build if coverage < 80%

#### Test Commands
```bash
cd src/backend
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
reportgenerator -reports:coverage.opencover.xml -targetdir:coverage-report
```

---

### 1.2 Frontend Unit Tests (Angular - Jasmine/Karma)

#### Test Framework
- **Jasmine** (test framework)
- **Karma** (test runner)
- **Angular Testing Utilities** (TestBed, ComponentFixture)

#### Coverage Areas

##### **1.2.1 Services**

###### AuthService
```typescript
// src/app/core/auth/auth.service.spec.ts
describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;
  
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [AuthService]
    });
    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });
  
  it('should login successfully with valid credentials', () => {
    const mockResponse = { success: true, data: { accessToken: 'token123' } };
    
    service.login('admin', 'pass123').subscribe(response => {
      expect(response.success).toBe(true);
      expect(response.data.accessToken).toBe('token123');
    });
    
    const req = httpMock.expectOne(`${environment.apiUrl}/api/v1/auth/login`);
    expect(req.request.method).toBe('POST');
    req.flush(mockResponse);
  });
  
  it('should handle login failure', () => {
    // Test 401 response
  });
  
  it('should store tokens in localStorage', () => {
    // Test token storage
  });
});
```

###### KeyService
```typescript
// src/app/core/services/key.service.spec.ts
- getKeys_shouldReturnKeyList
- retrieveKey_shouldDecryptKey
- createKey_shouldEncryptAndStore
```

##### **1.2.2 Components**

###### LoginComponent
```typescript
// src/app/features/auth/login/login.component.spec.ts
describe('LoginComponent', () => {
  it('should create', () => {
    expect(component).toBeTruthy();
  });
  
  it('should validate form fields', () => {
    component.loginForm.controls['username'].setValue('');
    expect(component.loginForm.invalid).toBe(true);
  });
  
  it('should call authService.login on submit', () => {
    spyOn(authService, 'login').and.returnValue(of({ success: true }));
    component.onSubmit();
    expect(authService.login).toHaveBeenCalled();
  });
});
```

###### KeyListComponent
```typescript
// src/app/features/keys/key-list/key-list.component.spec.ts
- shouldLoadKeysOnInit
- shouldFilterKeysByStatus
- shouldPaginateResults
- shouldOpenRetrieveDialog
```

##### **1.2.3 Guards**

###### AuthGuard
```typescript
// src/app/core/auth/auth.guard.spec.ts
- shouldAllowAuthenticatedUser
- shouldRedirectUnauthenticatedToLogin
- shouldCheckRoleBasedAccess
```

#### Coverage Metrics
- **Target**: 75% line coverage
- **Tools**: Istanbul/nyc
- **CI Integration**: Fail build if coverage < 75%

#### Test Commands
```bash
cd src/frontend
ng test --code-coverage --watch=false
```

---

## 2. Integration Tests

### 2.1 Backend API Integration Tests

#### Test Framework
- **xUnit**
- **WebApplicationFactory** (ASP.NET Core in-memory server)
- **Testcontainers** (Docker-based PostgreSQL, Redis for tests)

#### Test Scenarios

##### **2.1.1 Authentication Flow**
```csharp
// SecureBox.Tests.Integration/API/AuthIntegrationTests.cs
public class AuthIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    
    [Fact]
    public async Task POST_Login_WithValidCredentials_Returns200WithToken()
    {
        // Arrange
        var request = new { username = "admin", password = "Admin@123" };
        
        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", request);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<AuthResponse>();
        content.Data.AccessToken.Should().NotBeEmpty();
    }
    
    [Fact]
    public async Task POST_Login_WithInvalidPassword_Returns401()
    {
        // Test
    }
}
```

##### **2.1.2 Key Management Flow**
```csharp
// SecureBox.Tests.Integration/API/KeysIntegrationTests.cs
[Fact]
public async Task CompleteKeyLifecycle_CreateRetrieveRotateRevoke()
{
    // 1. Create key
    var createResponse = await _client.PostAsJsonAsync("/api/v1/keys", createRequest);
    createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
    var keyId = ...; // Extract from response
    
    // 2. Retrieve key
    var retrieveResponse = await _client.PostAsJsonAsync($"/api/v1/keys/{keyId}/retrieve", ...);
    retrieveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    
    // 3. Rotate key
    var rotateResponse = await _client.PostAsJsonAsync($"/api/v1/keys/{keyId}/rotate", ...);
    rotateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    
    // 4. Revoke key
    var revokeResponse = await _client.PostAsJsonAsync($"/api/v1/keys/{keyId}/revoke", ...);
    revokeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    
    // 5. Verify revoked key cannot be retrieved
    var retrieveAfterRevoke = await _client.PostAsJsonAsync($"/api/v1/keys/{keyId}/retrieve", ...);
    retrieveAfterRevoke.StatusCode.Should().Be(HttpStatusCode.Gone);
}
```

##### **2.1.3 Database Integration**
```csharp
// SecureBox.Tests.Integration/Data/DatabaseIntegrationTests.cs
- User_CRUD_Operations
- Key_WithCascadeDelete_ShouldDeleteAccessLogs
- QueryFilter_ShouldExcludeSoftDeleted
```

##### **2.1.4 Redis Integration**
```csharp
// SecureBox.Tests.Integration/Cache/RedisIntegrationTests.cs
- TokenBlacklist_AddToken_ShouldPreventAccess
- RateLimiting_ExceedLimit_ShouldReturn429
```

##### **2.1.5 RabbitMQ Integration**
```csharp
// SecureBox.Tests.Integration/Messaging/RabbitMQIntegrationTests.cs
- PublishAuditEvent_ShouldBeConsumed
- KeyRetrievalEvent_ShouldTriggerNotification
```

#### Test Environment Setup
```csharp
// Use Testcontainers for isolated test environment
public class IntegrationTestFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer;
    private readonly RedisContainer _redisContainer;
    
    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();
        await _redisContainer.StartAsync();
        // Seed test data
    }
    
    public async Task DisposeAsync()
    {
        await _postgresContainer.DisposeAsync();
        await _redisContainer.DisposeAsync();
    }
}
```

---

### 2.2 Frontend-Backend Integration Tests

#### Test Framework
- **Cypress** (E2E tool, but used for integration here)

#### Test Scenarios
```javascript
// tests/integration/api-integration.spec.ts
describe('API Integration', () => {
  it('should authenticate and retrieve key', () => {
    // Login via API
    cy.request('POST', '/api/v1/auth/login', {
      username: 'testuser',
      password: 'Test@123'
    }).then(response => {
      const token = response.body.data.accessToken;
      
      // Create key
      cy.request({
        method: 'POST',
        url: '/api/v1/keys',
        headers: { Authorization: `Bearer ${token}` },
        body: { name: 'Test Key', value: 'secret', ... }
      }).then(createResponse => {
        expect(createResponse.status).to.eq(201);
      });
    });
  });
});
```

---

## 3. End-to-End (E2E) Tests

### 3.1 Frontend E2E Tests (Cypress)

#### Test Framework
- **Cypress** (primary)
- **Page Object Model** (design pattern)

#### Test Scenarios

##### **3.1.1 User Authentication**
```javascript
// cypress/e2e/auth/login.cy.ts
describe('Login Flow', () => {
  beforeEach(() => {
    cy.visit('/auth/login');
  });
  
  it('should login successfully with valid credentials', () => {
    cy.get('[data-cy=username]').type('admin');
    cy.get('[data-cy=password]').type('Admin@123');
    cy.get('[data-cy=login-button]').click();
    
    cy.url().should('include', '/dashboard');
    cy.get('[data-cy=user-menu]').should('contain', 'admin');
  });
  
  it('should show error with invalid credentials', () => {
    cy.get('[data-cy=username]').type('admin');
    cy.get('[data-cy=password]').type('wrongpass');
    cy.get('[data-cy=login-button]').click();
    
    cy.get('[data-cy=error-message]').should('be.visible')
      .and('contain', 'Invalid credentials');
  });
  
  it('should lock account after 5 failed attempts', () => {
    // Test account lockout
  });
});
```

##### **3.1.2 Key Management (Admin)**
```javascript
// cypress/e2e/keys/key-management.cy.ts
describe('Key Management - Admin', () => {
  beforeEach(() => {
    cy.loginAsAdmin(); // Custom command
    cy.visit('/keys');
  });
  
  it('should create a new key', () => {
    cy.get('[data-cy=create-key-button]').click();
    cy.get('[data-cy=key-name]').type('Production DB Password');
    cy.get('[data-cy=key-type]').select('DATABASE_PASSWORD');
    cy.get('[data-cy=key-value]').type('MyS3cr3tP@ss');
    cy.get('[data-cy=certificate]').select('Production Cert');
    cy.get('[data-cy=submit]').click();
    
    cy.get('[data-cy=success-message]').should('contain', 'Key created successfully');
    cy.get('[data-cy=key-list]').should('contain', 'Production DB Password');
  });
  
  it('should retrieve key and display value', () => {
    cy.get('[data-cy=key-row]').first().find('[data-cy=retrieve-button]').click();
    cy.get('[data-cy=retrieve-reason]').type('Deployment');
    cy.get('[data-cy=confirm-retrieve]').click();
    
    cy.get('[data-cy=key-value]').should('be.visible').and('not.be.empty');
    cy.get('[data-cy=copy-button]').should('be.visible');
  });
  
  it('should rotate key', () => {
    // Test key rotation flow
  });
  
  it('should revoke key', () => {
    // Test key revocation flow
  });
});
```

##### **3.1.3 Certificate Management**
```javascript
// cypress/e2e/certificates/certificate-upload.cy.ts
describe('Certificate Upload', () => {
  it('should upload PEM certificate', () => {
    cy.loginAsAdmin();
    cy.visit('/certificates/upload');
    
    cy.get('[data-cy=cert-name]').type('Test Certificate');
    cy.get('[data-cy=cert-file]').selectFile('fixtures/test-cert.pem');
    cy.get('[data-cy=upload-button]').click();
    
    cy.get('[data-cy=success-message]').should('contain', 'Certificate uploaded');
  });
  
  it('should validate certificate expiry', () => {
    // Test expired certificate handling
  });
});
```

##### **3.1.4 User Management (Admin Only)**
```javascript
// cypress/e2e/users/user-management.cy.ts
describe('User Management', () => {
  it('should create new user and assign role', () => {
    cy.loginAsAdmin();
    cy.visit('/users');
    
    cy.get('[data-cy=create-user]').click();
    cy.get('[data-cy=username]').type('newuser');
    cy.get('[data-cy=email]').type('newuser@example.com');
    cy.get('[data-cy=password]').type('NewUser@123');
    cy.get('[data-cy=role-client]').check();
    cy.get('[data-cy=submit]').click();
    
    cy.get('[data-cy=user-list]').should('contain', 'newuser');
  });
});
```

##### **3.1.5 Audit Log Viewer**
```javascript
// cypress/e2e/audit/audit-logs.cy.ts
describe('Audit Logs', () => {
  it('should display key access logs', () => {
    cy.loginAsAdmin();
    cy.visit('/audit/trails');
    
    cy.get('[data-cy=filter-action]').select('Key.Retrieved');
    cy.get('[data-cy=apply-filter]').click();
    
    cy.get('[data-cy=log-table]').should('be.visible');
    cy.get('[data-cy=log-row]').should('have.length.greaterThan', 0);
  });
  
  it('should export audit logs to CSV', () => {
    // Test export functionality
  });
});
```

#### Page Object Model Example
```javascript
// cypress/support/pages/LoginPage.ts
export class LoginPage {
  visit() {
    cy.visit('/auth/login');
  }
  
  fillUsername(username: string) {
    cy.get('[data-cy=username]').type(username);
  }
  
  fillPassword(password: string) {
    cy.get('[data-cy=password]').type(password);
  }
  
  submit() {
    cy.get('[data-cy=login-button]').click();
  }
  
  login(username: string, password: string) {
    this.visit();
    this.fillUsername(username);
    this.fillPassword(password);
    this.submit();
  }
}
```

#### Test Commands
```bash
cd src/frontend
npx cypress run --spec "cypress/e2e/**/*.cy.ts"
npx cypress open  # Interactive mode
```

---

## 4. Security Tests

### 4.1 Automated Security Scanning

#### 4.1.1 OWASP ZAP (Dynamic Application Security Testing)
```bash
# Run ZAP scan against running application
docker run -t owasp/zap2docker-stable zap-baseline.py \
  -t https://securebox.local \
  -r zap-report.html
```

**Test Coverage**:
- SQL Injection
- XSS (Cross-Site Scripting)
- CSRF (Cross-Site Request Forgery)
- Insecure Direct Object References
- Security Misconfigurations
- Sensitive Data Exposure

#### 4.1.2 Dependency Scanning
```bash
# Backend (.NET)
dotnet list package --vulnerable --include-transitive

# Frontend (npm)
npm audit
npm audit fix

# Docker images
trivy image securebox-api:latest
```

#### 4.1.3 Static Application Security Testing (SAST)
```bash
# SonarQube
sonar-scanner \
  -Dsonar.projectKey=secure-box \
  -Dsonar.sources=src \
  -Dsonar.host.url=http://localhost:9000

# Snyk
snyk test --all-projects
```

### 4.2 Manual Security Testing

#### 4.2.1 Authentication & Authorization Tests

**Test Cases**:
1. **JWT Token Tampering**:
   - Modify token payload → Should return 401
   - Expired token → Should return 401
   - Invalid signature → Should return 401

2. **Brute Force Protection**:
   - 5+ failed login attempts → Account lockout
   - Rate limiting → 429 Too Many Requests

3. **Authorization Bypass**:
   - Client role accessing Admin endpoint → 403 Forbidden
   - Retrieve key without permission → 403 Forbidden

4. **Session Management**:
   - Logout → Token blacklisted
   - Concurrent sessions → Allowed but logged

#### 4.2.2 Encryption Tests

**Test Cases**:
1. **Key Encryption**:
   - Verify AES-256-GCM used
   - Unique IV per encryption
   - Authentication tag validated on decryption

2. **Certificate Validation**:
   - Expired certificate → Key retrieval fails
   - Revoked certificate → Key retrieval fails
   - Invalid certificate → Upload rejected

3. **Secure Deletion**:
   - Deleted key → EncryptedValue overwritten
   - No plaintext in memory dumps

#### 4.2.3 Input Validation Tests

**Test Cases**:
1. **SQL Injection Attempts**:
   ```
   POST /api/v1/auth/login
   { "username": "admin' OR '1'='1", "password": "x" }
   → Should return 401, not bypass auth
   ```

2. **XSS Attempts**:
   ```
   POST /api/v1/keys
   { "name": "<script>alert('XSS')</script>", ... }
   → Should sanitize/escape input
   ```

3. **File Upload Attacks**:
   - Upload .exe file as certificate → Rejected
   - Upload oversized file (>10MB) → 413 Payload Too Large
   - Path traversal in filename → Sanitized

#### 4.2.4 API Security Tests

**Test Cases**:
1. **Rate Limiting**:
   - 100 requests in 1 minute → 429 after limit
   - Key retrieval: 10/hour limit enforced

2. **CORS**:
   - Request from unauthorized origin → Blocked
   - Request from allowed origin → Allowed

3. **Security Headers**:
   - X-Frame-Options: DENY
   - X-Content-Type-Options: nosniff
   - Strict-Transport-Security: present

### 4.3 Penetration Testing Checklist

**Quarterly Tests** (Manual/Automated):
- [ ] Network penetration (port scanning, service enumeration)
- [ ] Web application penetration (OWASP Top 10)
- [ ] Social engineering (phishing simulation)
- [ ] Physical security (if applicable)
- [ ] Report vulnerabilities with CVSS scores
- [ ] Remediation plan with timelines

---

## 5. Performance & Load Tests

### 5.1 Load Testing (k6)

#### Test Framework
- **k6** (Grafana Labs)
- **Metrics**: Response time, throughput, error rate

#### Test Scenarios

##### **5.1.1 Key Retrieval Under Load**
```javascript
// tests/performance/key-retrieval-load.js
import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  stages: [
    { duration: '1m', target: 50 },   // Ramp-up to 50 users
    { duration: '3m', target: 50 },   // Stay at 50 users
    { duration: '1m', target: 100 },  // Spike to 100 users
    { duration: '3m', target: 100 },  // Stay at 100 users
    { duration: '1m', target: 0 },    // Ramp-down
  ],
  thresholds: {
    http_req_duration: ['p(95)<500'], // 95% of requests < 500ms
    http_req_failed: ['rate<0.01'],   // Error rate < 1%
  },
};

export default function () {
  const token = __ENV.AUTH_TOKEN;
  const keyId = __ENV.KEY_ID;
  
  const response = http.post(
    `http://localhost:5000/api/v1/keys/${keyId}/retrieve`,
    JSON.stringify({ reason: 'Load test' }),
    {
      headers: {
        'Authorization': `Bearer ${token}`,
        'Content-Type': 'application/json',
      },
    }
  );
  
  check(response, {
    'status is 200': (r) => r.status === 200,
    'response time < 500ms': (r) => r.timings.duration < 500,
    'key value present': (r) => JSON.parse(r.body).data.value !== undefined,
  });
  
  sleep(1);
}
```

**Run Command**:
```bash
k6 run --vus 100 --duration 5m tests/performance/key-retrieval-load.js
```

##### **5.1.2 API Throughput Test**
```javascript
// tests/performance/api-throughput.js
export const options = {
  scenarios: {
    constant_load: {
      executor: 'constant-arrival-rate',
      rate: 1000,         // 1000 requests per second
      timeUnit: '1s',
      duration: '5m',
      preAllocatedVUs: 100,
      maxVUs: 500,
    },
  },
};

// Test multiple endpoints
export default function () {
  const endpoints = [
    '/api/v1/keys',
    '/api/v1/certificates',
    '/api/v1/users',
  ];
  
  const endpoint = endpoints[Math.floor(Math.random() * endpoints.length)];
  http.get(`http://localhost:5000${endpoint}`, { headers: { ... } });
}
```

##### **5.1.3 Stress Test (Find Breaking Point)**
```javascript
// tests/performance/stress-test.js
export const options = {
  stages: [
    { duration: '2m', target: 100 },
    { duration: '5m', target: 100 },
    { duration: '2m', target: 200 },
    { duration: '5m', target: 200 },
    { duration: '2m', target: 300 },  // Beyond expected load
    { duration: '5m', target: 300 },
    { duration: '2m', target: 0 },
  ],
};

// Observe when system starts failing (CPU, memory, error rates)
```

#### Performance Benchmarks

| Scenario                | Target       | Acceptance Criteria        |
|-------------------------|--------------|----------------------------|
| Key Retrieval           | 100 req/s    | p95 < 500ms, error < 1%    |
| Key Creation            | 50 req/s     | p95 < 1s, error < 0.5%     |
| Certificate Upload      | 10 req/s     | p95 < 2s, error < 0.1%     |
| Login                   | 200 req/s    | p95 < 300ms, error < 0.5%  |
| Database Queries        | -            | < 50ms (90th percentile)   |
| Redis Cache Hits        | -            | < 10ms (95th percentile)   |

### 5.2 Database Performance Tests

#### PostgreSQL Query Performance
```sql
-- Analyze slow queries
SELECT query, mean_exec_time, calls
FROM pg_stat_statements
ORDER BY mean_exec_time DESC
LIMIT 10;

-- Test key retrieval query performance
EXPLAIN ANALYZE
SELECT k.*, c.CertificateData
FROM Keys k
INNER JOIN Certificates c ON k.CertificateId = c.CertificateId
WHERE k.KeyId = 'uuid' AND k.Status = 'Active';
```

#### Redis Performance Tests
```bash
# Benchmark Redis operations
redis-benchmark -h localhost -p 6379 -c 50 -n 10000 -d 1024 -t get,set
```

---

## 6. Test Automation & CI/CD

### 6.1 GitHub Actions Workflow

```yaml
# .github/workflows/test.yml
name: Test Suite

on:
  push:
    branches: [main, develop]
  pull_request:
    branches: [main, develop]

jobs:
  unit-tests-backend:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0.x'
      - name: Restore dependencies
        run: dotnet restore src/backend
      - name: Build
        run: dotnet build src/backend --no-restore
      - name: Run unit tests
        run: dotnet test src/backend --no-build --verbosity normal /p:CollectCoverage=true
      - name: Upload coverage
        uses: codecov/codecov-action@v3
        with:
          files: ./coverage.xml
  
  unit-tests-frontend:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Setup Node.js
        uses: actions/setup-node@v3
        with:
          node-version: '20.x'
      - name: Install dependencies
        run: npm ci
        working-directory: src/frontend
      - name: Run unit tests
        run: npm run test -- --code-coverage --watch=false
        working-directory: src/frontend
      - name: Upload coverage
        uses: codecov/codecov-action@v3
  
  integration-tests:
    runs-on: ubuntu-latest
    services:
      postgres:
        image: postgres:16-alpine
        env:
          POSTGRES_PASSWORD: testpass
        options: >-
          --health-cmd pg_isready
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5
    steps:
      - uses: actions/checkout@v3
      - name: Run integration tests
        run: dotnet test src/backend/SecureBox.Tests.Integration
  
  e2e-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Start Docker Compose
        run: docker-compose up -d
      - name: Wait for services
        run: sleep 30
      - name: Run Cypress E2E tests
        uses: cypress-io/github-action@v5
        with:
          working-directory: src/frontend
          wait-on: 'http://localhost:4200'
      - name: Stop Docker Compose
        run: docker-compose down
  
  security-scan:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Run Trivy scan
        uses: aquasecurity/trivy-action@master
        with:
          scan-type: 'fs'
          scan-ref: '.'
          format: 'sarif'
          output: 'trivy-results.sarif'
      - name: Upload to GitHub Security
        uses: github/codeql-action/upload-sarif@v2
        with:
          sarif_file: 'trivy-results.sarif'
```

### 6.2 Test Reporting

- **Unit Tests**: Coverlet → Codecov.io (badge in README)
- **Integration Tests**: Test summary in GitHub Actions
- **E2E Tests**: Cypress Dashboard (screenshots/videos on failure)
- **Security Tests**: SARIF upload to GitHub Security tab
- **Performance Tests**: k6 HTML report → Artifact upload

---

## 7. Test Data Management

### 7.1 Test Fixtures

#### Backend
```csharp
// SecureBox.Tests/Fixtures/TestDataBuilder.cs
public static class TestDataBuilder
{
    public static User CreateTestUser(string username = "testuser")
    {
        return new User
        {
            UserId = Guid.NewGuid(),
            Username = username,
            Email = $"{username}@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test@123"),
            IsActive = true,
        };
    }
    
    public static Certificate CreateTestCertificate()
    {
        return new Certificate
        {
            // ...
        };
    }
}
```

#### Frontend
```typescript
// cypress/fixtures/users.json
{
  "admin": {
    "username": "admin",
    "password": "Admin@123",
    "roles": ["Admin"]
  },
  "client": {
    "username": "testclient",
    "password": "Client@123",
    "roles": ["Client"]
  }
}
```

### 7.2 Database Seeding for Tests

```csharp
// SecureBox.Tests/DatabaseSeeder.cs
public static async Task SeedTestData(SecureBoxDbContext context)
{
    // Create roles
    var adminRole = new Role { RoleName = "Admin", IsSystem = true };
    var clientRole = new Role { RoleName = "Client", IsSystem = true };
    context.Roles.AddRange(adminRole, clientRole);
    
    // Create test users
    var adminUser = TestDataBuilder.CreateTestUser("admin");
    context.Users.Add(adminUser);
    
    // Create test certificates
    var testCert = TestDataBuilder.CreateTestCertificate();
    context.Certificates.Add(testCert);
    
    await context.SaveChangesAsync();
}
```

---

## 8. Test Metrics & Quality Gates

### 8.1 Coverage Requirements

| Layer            | Line Coverage | Branch Coverage | Status |
|------------------|---------------|-----------------|--------|
| Backend Core     | ≥ 85%         | ≥ 75%           | 🔴 Fail if below |
| Backend API      | ≥ 80%         | ≥ 70%           | 🔴 Fail if below |
| Frontend         | ≥ 75%         | ≥ 65%           | 🟡 Warning if below |
| Integration      | N/A           | N/A             | All tests pass |

### 8.2 Performance Benchmarks

| Metric                  | Target      | Action if Failed |
|-------------------------|-------------|------------------|
| API Response Time (p95) | < 500ms     | Investigate      |
| Database Query (p90)    | < 50ms      | Optimize query   |
| Key Retrieval (p99)     | < 1s        | Check encryption performance |
| Error Rate              | < 1%        | Block deployment |

### 8.3 Security Scan Thresholds

| Severity | Action           |
|----------|------------------|
| Critical | Block deployment |
| High     | Block deployment |
| Medium   | Warning          |
| Low      | Info             |

---

## 9. Test Schedule

### 9.1 Continuous (Every Commit)
- ✅ Unit tests (Backend + Frontend)
- ✅ Linting & code style checks
- ✅ Dependency vulnerability scan

### 9.2 Pull Request
- ✅ Unit tests
- ✅ Integration tests
- ✅ Code coverage report
- ✅ Static security analysis (SAST)

### 9.3 Daily (Nightly Build)
- ✅ Full integration test suite
- ✅ E2E tests (smoke tests)
- ✅ Docker image vulnerability scan

### 9.4 Weekly
- ✅ Full E2E test suite
- ✅ Performance/load tests
- ✅ OWASP ZAP security scan

### 9.5 Quarterly
- ✅ Manual penetration testing
- ✅ Security audit
- ✅ Load testing (peak scenarios)
- ✅ Disaster recovery drill

---

## 10. Test Environment

### 10.1 Local Development
- Docker-Compose (all services)
- Test databases (PostgreSQL, MongoDB)
- Mock external services

### 10.2 CI/CD Pipeline
- GitHub Actions runners
- Testcontainers (isolated DB instances)
- Headless browser (Cypress)

### 10.3 Staging Environment
- Production-like setup
- Real certificates (test CA)
- Full monitoring stack (ELK)
- Load balancer (Nginx)

### 10.4 Production
- Blue-Green deployment
- Canary releases (10% traffic)
- Rollback strategy
- Health checks before full rollout

---

## Sonuç

Bu test planı, Secure Box sisteminin **kalite, güvenlik ve performans** standartlarını garanti altına alır. Test coverage %80'in üzerinde tutulmalı, tüm kritik akışlar otomatize edilmeli ve CI/CD pipeline'a entegre edilmelidir.

**Key Metrics**:
- ✅ %80+ code coverage
- ✅ 0 critical security vulnerabilities
- ✅ < 500ms API response time (p95)
- ✅ < 1% error rate under load
- ✅ 100% critical E2E tests passing

