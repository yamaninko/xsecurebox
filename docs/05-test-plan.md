# Secure Box - Test Plan

## Test Strategy

### Test Levels
1. **Unit Tests**: Individual components, services, methods
2. **Integration Tests**: API endpoint tests, service interaction
3. **End-to-End Tests**: Full user workflows
4. **Performance Tests**: Load and stress testing
5. **Security Tests**: Penetration testing, vulnerability scanning

---

## 1. Unit Tests

### Backend (C# .NET)

#### Framework: xUnit + Moq

#### Test Coverage Targets
- **Services**: 80%+ code coverage
- **Controllers**: 70%+ code coverage
- **Utilities**: 90%+ code coverage

#### Test Suites

**AuthService Tests**
- ✅ `LoginAsync_ValidCredentials_ReturnsAuthResponse`
- ✅ `LoginAsync_InvalidPassword_ThrowsUnauthorizedAccessException`
- ✅ `LoginAsync_InactiveUser_ThrowsUnauthorizedAccessException`
- ✅ `RefreshTokenAsync_ValidToken_ReturnsNewAccessToken`
- ✅ `RefreshTokenAsync_ExpiredToken_ThrowsException`
- ✅ `ChangePasswordAsync_ValidCurrentPassword_ReturnsTrue`
- ✅ `ChangePasswordAsync_InvalidCurrentPassword_ThrowsFalse`

**KeyService Tests**
- ✅ `CreateKeyAsync_ValidRequest_CreatesKey`
- ✅ `CreateKeyAsync_DuplicateName_ThrowsException`
- ✅ `RetrieveKeyAsync_ValidCredentials_ReturnsDecryptedValue`
- ✅ `RetrieveKeyAsync_RevokedKey_ThrowsException`
- ✅ `RotateKeyAsync_ValidRequest_CreatesNewVersion`
- ✅ `RevokeKeyAsync_ActiveKey_MarksAsRevoked`
- ✅ `GetAllKeysAsync_FilterByEnvironment_ReturnsFilteredKeys`
- ✅ `GetAllKeysAsync_FilterByTag_ReturnsFilteredKeys`

**EncryptionService Tests**
- ✅ `EncryptAsync_ValidPlaintext_ReturnsEncryptedData`
- ✅ `DecryptAsync_ValidCiphertext_ReturnsPlaintext`
- ✅ `DecryptAsync_InvalidCertificate_ThrowsException`
- ✅ `DecryptAsync_TamperedData_ThrowsException`

**RoleService Tests**
- ✅ `GetAllRolesAsync_ReturnsAllRoles`
- ✅ `CreateRoleAsync_ValidRequest_CreatesRole`
- ✅ `CreateRoleAsync_DuplicateName_ThrowsException`
- ✅ `UpdateRoleAsync_SystemRole_ThrowsException`
- ✅ `DeleteRoleAsync_SystemRole_ThrowsException`
- ✅ `AssignPermissionToRoleAsync_ValidIds_AssignsPermission`

---

### Frontend (Angular/TypeScript)

#### Framework: Jasmine + Karma

#### Test Coverage Targets
- **Components**: 70%+ code coverage
- **Services**: 80%+ code coverage
- **Guards/Interceptors**: 90%+ code coverage

#### Test Suites

**AuthService Tests**
- ✅ `login() should call API and store token`
- ✅ `logout() should clear token and redirect`
- ✅ `isAuthenticated() should return true if token exists`
- ✅ `refreshToken() should refresh access token`

**KeyService Tests**
- ✅ `getKeys() should fetch keys from API`
- ✅ `createKey() should post to API`
- ✅ `retrieveKey() should require triple auth`
- ✅ `rotateKey() should create new version`

**AuthGuard Tests**
- ✅ `canActivate() should allow if authenticated`
- ✅ `canActivate() should redirect to login if not authenticated`
- ✅ `canActivate() should check role permissions`

---

## 2. Integration Tests

### Backend API Tests

#### Framework: WebApplicationFactory + xUnit

#### Test Scenarios

**Authentication Flow**
```csharp
[Fact]
public async Task LoginAndAccessProtectedEndpoint_Success()
{
    // Arrange
    var client = _factory.CreateClient();
    var loginRequest = new { username = "admin", password = "Admin@123" };
    
    // Act - Login
    var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
    var authData = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
    
    // Assert - Login success
    Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
    Assert.NotNull(authData.AccessToken);
    
    // Act - Access protected endpoint
    client.DefaultRequestHeaders.Authorization = 
        new AuthenticationHeaderValue("Bearer", authData.AccessToken);
    var keysResponse = await client.GetAsync("/api/v1/keys");
    
    // Assert - Protected endpoint access success
    Assert.Equal(HttpStatusCode.OK, keysResponse.StatusCode);
}
```

**Key Lifecycle Test**
```csharp
[Fact]
public async Task KeyLifecycle_CreateRetrieveRotateRevoke_Success()
{
    // 1. Create key
    // 2. Retrieve key (verify decryption)
    // 3. Rotate key (verify new version)
    // 4. Revoke key (verify status)
    // 5. Attempt retrieval (verify failure)
}
```

**Role-Based Access Control Test**
```csharp
[Fact]
public async Task AdminOnlyEndpoint_ClientRole_Forbidden()
{
    // Login as Client user
    // Attempt to access /api/v1/roles
    // Assert 403 Forbidden
}
```

---

## 3. End-to-End Tests

### Framework: Cypress or Playwright

#### User Workflows

**Workflow 1: User Login**
```javascript
describe('User Login', () => {
  it('should login with valid credentials', () => {
    cy.visit('/login');
    cy.get('#username').type('admin');
    cy.get('#password').type('Admin@123');
    cy.get('button[type=submit]').click();
    cy.url().should('include', '/dashboard');
    cy.contains('Welcome, admin');
  });
});
```

**Workflow 2: Create and Retrieve Key**
```javascript
describe('Key Management', () => {
  beforeEach(() => {
    cy.login('admin', 'Admin@123'); // Custom command
  });

  it('should create a new key', () => {
    cy.visit('/keys');
    cy.get('[data-testid=create-key-btn]').click();
    cy.get('#name').type('TEST_API_KEY');
    cy.get('#keyType').select('ApiKey');
    cy.get('#value').type('secret-value-123');
    cy.get('#certificateId').select('Prod Cert 2025');
    cy.get('#environmentTag').select('DEV');
    cy.get('button[type=submit]').click();
    
    cy.contains('Key created successfully');
    cy.contains('TEST_API_KEY');
  });

  it('should retrieve key with triple auth', () => {
    cy.visit('/keys');
    cy.contains('TEST_API_KEY').parent().find('[data-testid=retrieve-btn]').click();
    
    // Modal opens
    cy.get('#password').type('Admin@123');
    cy.get('#certificate').select('Admin Certificate');
    cy.get('#reason').type('Testing key retrieval');
    cy.get('[data-testid=confirm-retrieve]').click();
    
    cy.contains('secret-value-123'); // Key value displayed
    cy.contains('Key retrieved successfully');
  });
});
```

**Workflow 3: User Management (Admin)**
```javascript
describe('User Management', () => {
  it('admin should create new user', () => {
    cy.login('admin', 'Admin@123');
    cy.visit('/users');
    cy.get('[data-testid=create-user-btn]').click();
    
    cy.get('#username').type('john.doe');
    cy.get('#email').type('john@example.com');
    cy.get('#password').type('SecurePass123!');
    cy.get('#firstName').type('John');
    cy.get('#lastName').type('Doe');
    cy.get('#roles').check('Client');
    cy.get('button[type=submit]').click();
    
    cy.contains('User created successfully');
    cy.contains('john.doe');
  });
});
```

---

## 4. Performance Tests

### Framework: k6 or JMeter

#### Test Scenarios

**Load Test: Concurrent Key Retrievals**
```javascript
import http from 'k6/http';
import { check } from 'k6';

export let options = {
  stages: [
    { duration: '2m', target: 100 }, // Ramp up to 100 users
    { duration: '5m', target: 100 }, // Stay at 100 users
    { duration: '2m', target: 0 },   // Ramp down
  ],
  thresholds: {
    http_req_duration: ['p(95)<500'], // 95% of requests < 500ms
    http_req_failed: ['rate<0.01'],   // <1% failure rate
  },
};

export default function () {
  let loginRes = http.post('http://localhost/api/v1/auth/login', 
    JSON.stringify({ username: 'admin', password: 'Admin@123' }),
    { headers: { 'Content-Type': 'application/json' } }
  );
  
  check(loginRes, { 'login succeeded': (r) => r.status === 200 });
  
  let token = loginRes.json().data.accessToken;
  
  let keysRes = http.get('http://localhost/api/v1/keys', {
    headers: { 'Authorization': `Bearer ${token}` }
  });
  
  check(keysRes, { 'keys fetched': (r) => r.status === 200 });
}
```

**Stress Test: API Throughput**
- **Target**: 1000 req/s
- **Duration**: 10 minutes
- **Success Criteria**: <1% error rate, p95 latency <1s

**Spike Test: Traffic Surge**
- **Scenario**: Sudden spike from 100 → 1000 users in 1 minute
- **Success Criteria**: No service downtime, auto-scaling works

---

## 5. Security Tests

### Vulnerability Scanning

#### Tools
- **OWASP ZAP**: Web application vulnerability scanner
- **Burp Suite**: Manual penetration testing
- **Nikto**: Web server scanner
- **Nmap**: Network scanning

#### Test Cases

**SQL Injection**
- ✅ Test all input fields with SQL injection payloads
- ✅ Verify parameterized queries prevent injection

**XSS (Cross-Site Scripting)**
- ✅ Test all form inputs with XSS payloads
- ✅ Verify Angular sanitization works

**Authentication Bypass**
- ✅ Attempt to access protected endpoints without token
- ✅ Test with expired/invalid tokens
- ✅ Test with tampered JWT tokens

**Authorization Bypass**
- ✅ Client user attempting admin endpoints
- ✅ User accessing another user's keys

**Brute Force Protection**
- ✅ Multiple failed login attempts
- ✅ Account lockout mechanism (if implemented)

**Certificate Validation**
- ✅ Use invalid/expired certificate for encryption
- ✅ Tamper with encrypted data

---

## 6. Manual Testing

### Exploratory Testing Charters

**Charter 1: Dashboard Navigation**
- **Goal**: Verify all dashboard links work
- **Time**: 30 minutes
- **Focus Areas**: Stats accuracy, chart rendering, responsive design

**Charter 2: Key Management Edge Cases**
- **Goal**: Test key creation with edge case inputs
- **Time**: 1 hour
- **Focus Areas**: Max length names, special characters, empty fields

**Charter 3: Multi-User Concurrency**
- **Goal**: Multiple users editing same resource
- **Time**: 45 minutes
- **Focus Areas**: Race conditions, optimistic locking

---

## Test Execution Schedule

### Sprint Testing (Every 2 weeks)
- Unit tests: Continuous (on every commit)
- Integration tests: Daily (CI/CD pipeline)
- E2E tests: Before sprint demo
- Performance tests: Once per sprint (if major changes)

### Release Testing (Before Production)
- Full regression suite
- Security vulnerability scan
- Performance baseline test
- Manual exploratory testing (4 hours)
- UAT (User Acceptance Testing) with stakeholders

### Post-Release
- Smoke tests in production
- Monitor error rates and performance
- Review audit logs for anomalies

---

## Test Environment Setup

### Local Development
```bash
docker-compose up -d
npm run test:unit          # Frontend unit tests
dotnet test                # Backend unit tests
npm run test:e2e           # Cypress E2E tests
```

### CI/CD Pipeline (GitHub Actions / Azure DevOps)
```yaml
steps:
  - name: Backend Unit Tests
    run: dotnet test --collect:"XPlat Code Coverage"
  
  - name: Frontend Unit Tests
    run: npm run test:ci
  
  - name: Integration Tests
    run: dotnet test --filter "Category=Integration"
  
  - name: E2E Tests
    run: npm run cypress:run
  
  - name: Code Coverage Report
    run: reportgenerator -reports:**/coverage.xml -targetdir:coverage
```

---

## Defect Management

### Bug Severity Levels
- **Critical**: System crash, data loss, security breach
- **High**: Major feature broken, performance degradation
- **Medium**: Minor feature issue, UI glitch
- **Low**: Cosmetic issue, typo

### Bug Lifecycle
1. **New**: Bug reported
2. **Triaged**: Severity assigned
3. **In Progress**: Developer working on fix
4. **Code Review**: PR submitted
5. **Testing**: QA verifying fix
6. **Closed**: Fix deployed and verified

---

## Metrics & KPIs

### Test Metrics
- **Code Coverage**: Target 80%
- **Test Pass Rate**: Target 95%
- **Defect Density**: <5 defects per 1000 LOC
- **Mean Time to Detect (MTTD)**: <1 day
- **Mean Time to Resolve (MTTR)**: <3 days

### Performance Metrics
- **Response Time**: p95 <500ms
- **Throughput**: 1000 req/s
- **Error Rate**: <0.1%
- **Availability**: 99.9% (3 nines)

---

**Last Updated**: 2025-11-07
**Test Lead**: [qa-lead@securebox.local]

